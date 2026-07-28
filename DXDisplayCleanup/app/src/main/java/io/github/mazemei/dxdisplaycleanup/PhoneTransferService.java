package io.github.mazemei.dxdisplaycleanup;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.ClipData;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.IBinder;
import android.os.PowerManager;
import android.os.SystemClock;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

public final class PhoneTransferService extends Service {
    private static final String ACTION_SEND =
            "io.github.mazemei.dxdisplaycleanup.SEND_TO_PC";
    private static final String ACTION_CANCEL =
            "io.github.mazemei.dxdisplaycleanup.CANCEL_PC_TRANSFER";
    private static final String EXTRA_URIS = "uris";
    private static final String CHANNEL_ID = "phone_to_pc_transfer";
    private static final int NOTIFICATION_ID = 1301;
    private static final int MAGIC = 0x44584D52;
    private static final int VERSION = 1;
    private static final int CHUNK_SIZE = 64 * 1024;

    private final ExecutorService executor =
            Executors.newSingleThreadExecutor();
    private final AtomicBoolean canceled = new AtomicBoolean();
    private final AtomicInteger pendingTransfers = new AtomicInteger();
    private volatile Socket currentSocket;
    private long lastNotificationAt;
    private int lastNotifiedCompleted = -1;

    static void start(Context context, ArrayList<Uri> uris) {
        Intent intent = new Intent(context, PhoneTransferService.class)
                .setAction(ACTION_SEND)
                .putParcelableArrayListExtra(EXTRA_URIS, uris)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        if (uris != null && !uris.isEmpty()) {
            ClipData clipData = ClipData.newRawUri(
                    "DX Manager transfer", uris.get(0));
            for (int index = 1; index < uris.size(); index++) {
                clipData.addItem(new ClipData.Item(uris.get(index)));
            }
            intent.setClipData(clipData);
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.startForegroundService(intent);
        } else {
            context.startService(intent);
        }
    }

    @Override
    public void onCreate() {
        super.onCreate();
        createNotificationChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_CANCEL.equals(intent.getAction())) {
            cancelTransfer();
            return START_NOT_STICKY;
        }
        ArrayList<Uri> uris = intent == null ? null :
                intent.getParcelableArrayListExtra(EXTRA_URIS);
        if (uris == null || uris.isEmpty()) {
            stopSelf(startId);
            return START_NOT_STICKY;
        }

        pendingTransfers.incrementAndGet();
        startForeground(NOTIFICATION_ID, buildNotification(
                getString(R.string.transfer_preparing), 0, 0, true, false));
        executor.execute(() -> runTransfer(uris, startId));
        return START_NOT_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        cancelTransfer();
        executor.shutdownNow();
        super.onDestroy();
    }

    private void runTransfer(ArrayList<Uri> uris, int startId) {
        PowerManager.WakeLock wakeLock = null;
        try {
            canceled.set(false);
            lastNotificationAt = 0;
            lastNotifiedCompleted = -1;
            PowerManager powerManager =
                    (PowerManager) getSystemService(POWER_SERVICE);
            wakeLock = powerManager.newWakeLock(
                    PowerManager.PARTIAL_WAKE_LOCK,
                    "DXCompanion:PhoneTransfer");
            wakeLock.acquire(60 * 60 * 1000L);

            TransferSessionStore.Session session =
                    TransferSessionStore.load(this);
            if (!session.isReady()) {
                throw new IOException(getString(
                        R.string.transfer_pc_not_ready));
            }
            PhoneTransferPlanner.Plan plan =
                    PhoneTransferPlanner.create(this, uris);
            send(session, plan);
            showTerminalNotification(
                    getString(R.string.transfer_complete), false);
        } catch (Exception exception) {
            String message = canceled.get()
                    ? getString(R.string.transfer_canceled)
                    : getString(R.string.transfer_failed,
                    readableMessage(exception));
            showTerminalNotification(message, true);
        } finally {
            if (wakeLock != null && wakeLock.isHeld()) {
                wakeLock.release();
            }
            currentSocket = null;
            if (pendingTransfers.decrementAndGet() <= 0) {
                pendingTransfers.set(0);
                stopForeground(false);
                stopSelfResult(startId);
            }
        }
    }

    private void send(TransferSessionStore.Session session,
            PhoneTransferPlanner.Plan plan) throws Exception {
        try (Socket socket = new Socket()) {
            currentSocket = socket;
            socket.connect(new InetSocketAddress(
                    InetAddress.getLoopbackAddress(), session.port), 10000);
            socket.setTcpNoDelay(true);
            socket.setSoTimeout(30000);
            try (DataOutputStream output = new DataOutputStream(
                    new BufferedOutputStream(socket.getOutputStream(),
                            CHUNK_SIZE));
                 DataInputStream input = new DataInputStream(
                         new BufferedInputStream(socket.getInputStream(),
                                 CHUNK_SIZE))) {
                output.writeInt(MAGIC);
                output.writeInt(VERSION);
                writeString(output, session.token);
                writeString(output, UUID.randomUUID().toString());
                output.writeInt(plan.entries.size());
                output.writeLong(plan.totalBytes);
                output.flush();
                readResponse(input);

                long sentBytes = 0;
                int completed = 0;
                for (PhoneTransferEntry entry : plan.entries) {
                    throwIfCanceled();
                    output.writeByte(entry.directory ? 0 : 1);
                    output.writeInt(entry.rootId);
                    writeString(output, entry.relativePath);
                    output.writeLong(entry.size);
                    output.writeLong(entry.lastModified);
                    if (!entry.directory) {
                        try (InputStream file = getContentResolver()
                                .openInputStream(entry.uri)) {
                            if (file == null) {
                                throw new IOException(
                                        "Could not open " +
                                                entry.relativePath);
                            }
                            byte[] buffer = new byte[CHUNK_SIZE];
                            while (true) {
                                throwIfCanceled();
                                int read = file.read(buffer);
                                if (read < 0) {
                                    break;
                                }
                                if (read == 0) {
                                    continue;
                                }
                                output.writeInt(read);
                                output.write(buffer, 0, read);
                                sentBytes += read;
                                updateProgress(entry.relativePath,
                                        completed, plan.entries.size(),
                                        sentBytes, plan.totalBytes, false);
                            }
                            output.writeInt(0);
                        }
                    }
                    completed++;
                    output.flush();
                    updateProgress(entry.relativePath, completed,
                            plan.entries.size(), sentBytes,
                            plan.totalBytes, true);
                }
                output.flush();
                readResponse(input);
            }
        } finally {
            currentSocket = null;
        }
    }

    private void updateProgress(String name, int completed, int total,
            long sentBytes, long totalBytes, boolean force) {
        long now = SystemClock.elapsedRealtime();
        if (!force && completed == lastNotifiedCompleted
                && now - lastNotificationAt < 250) {
            return;
        }
        lastNotificationAt = now;
        lastNotifiedCompleted = completed;
        int maximum = totalBytes > 0 ? 100 : Math.max(1, total);
        int progress = totalBytes > 0
                ? (int) Math.min(100, sentBytes * 100L / totalBytes)
                : completed;
        boolean indeterminate = totalBytes < 0;
        String text = getString(R.string.transfer_progress,
                completed, total, name);
        getSystemService(NotificationManager.class).notify(
                NOTIFICATION_ID,
                buildNotification(text, maximum, progress,
                        indeterminate, false));
    }

    private void showTerminalNotification(String text, boolean failed) {
        getSystemService(NotificationManager.class).notify(
                NOTIFICATION_ID,
                buildNotification(text, 100, failed ? 0 : 100,
                        false, true));
    }

    private Notification buildNotification(String text, int maximum,
            int progress, boolean indeterminate, boolean terminal) {
        Intent openIntent = new Intent(this, MainActivity.class);
        PendingIntent open = PendingIntent.getActivity(
                this, 0, openIntent,
                PendingIntent.FLAG_UPDATE_CURRENT |
                        PendingIntent.FLAG_IMMUTABLE);
        Notification.Builder builder = Build.VERSION.SDK_INT >= 26
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        builder.setSmallIcon(R.drawable.ic_tile_cleanup)
                .setContentTitle(getString(R.string.transfer_notification_title))
                .setContentText(text)
                .setContentIntent(open)
                .setOnlyAlertOnce(!terminal)
                .setOngoing(!terminal)
                .setAutoCancel(terminal);
        if (!terminal) {
            Intent cancelIntent = new Intent(this,
                    PhoneTransferService.class).setAction(ACTION_CANCEL);
            PendingIntent cancel = PendingIntent.getService(
                    this, 1, cancelIntent,
                    PendingIntent.FLAG_UPDATE_CURRENT |
                            PendingIntent.FLAG_IMMUTABLE);
            builder.addAction(0, getString(R.string.transfer_cancel), cancel);
            builder.setProgress(maximum, progress, indeterminate);
        }
        return builder.build();
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT < 26) {
            return;
        }
        NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                getString(R.string.transfer_channel_name),
                NotificationManager.IMPORTANCE_LOW);
        getSystemService(NotificationManager.class)
                .createNotificationChannel(channel);
    }

    private void cancelTransfer() {
        canceled.set(true);
        Socket socket = currentSocket;
        if (socket != null) {
            try {
                socket.close();
            } catch (IOException ignored) {
            }
        }
    }

    private void throwIfCanceled() throws IOException {
        if (canceled.get() || Thread.currentThread().isInterrupted()) {
            throw new IOException(getString(R.string.transfer_canceled));
        }
    }

    private static void writeString(DataOutputStream output, String value)
            throws IOException {
        byte[] bytes = (value == null ? "" : value)
                .getBytes(StandardCharsets.UTF_8);
        output.writeInt(bytes.length);
        output.write(bytes);
    }

    private static void readResponse(DataInputStream input)
            throws IOException {
        int status = input.read();
        if (status < 0) {
            throw new EOFException();
        }
        int length = input.readInt();
        if (length < 0 || length > 1024 * 1024) {
            throw new IOException("Invalid response length.");
        }
        byte[] bytes = new byte[length];
        input.readFully(bytes);
        if (status != 1) {
            throw new IOException(new String(bytes, StandardCharsets.UTF_8));
        }
    }

    private static String readableMessage(Exception exception) {
        String message = exception.getMessage();
        return message == null || message.trim().isEmpty()
                ? exception.getClass().getSimpleName() : message;
    }
}

package io.github.mazemei.dxdisplaycleanup;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.IntentFilter;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.util.Log;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.EOFException;
import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;

public final class SessionGuardianService extends Service {
    static final String ACTION_START =
            "io.github.mazemei.dxdisplaycleanup.START_SESSION_GUARDIAN";

    private static final String TAG = "DXSessionGuardian";
    private static final String CHANNEL_ID = "dx_manager_session_guardian";
    private static final int NOTIFICATION_ID = 1401;
    private static final int MAGIC = 0x44584744;
    private static final int VERSION = 1;
    private static final int COMMAND_PING = 1;
    private static final int COMMAND_WINDOWS_SHUTDOWN = 2;
    private static final int COMMAND_STOP_MONITORING = 3;
    private static final long RECONNECT_DELAY_MS = 2000L;
    private static final int MAX_STRING_BYTES = 64 * 1024;
    private static final String ACTION_USB_STATE =
            "android.hardware.usb.action.USB_STATE";
    private static final String EXTRA_USB_CONNECTED = "connected";

    private final ExecutorService executor =
            Executors.newSingleThreadExecutor();
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final AtomicInteger generation = new AtomicInteger();
    private volatile Socket currentSocket;
    private volatile boolean connected;
    private volatile boolean stopRequested;
    private volatile Runnable pendingCleanup;

    @Override
    public void onCreate() {
        super.onCreate();
        createNotificationChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_START.equals(intent.getAction())) {
            int port = intent.getIntExtra(
                    "port", GuardianSessionStore.DEFAULT_PORT);
            String token = intent.getStringExtra("token");
            String transport = intent.getStringExtra("transport");
            if (port > 0 && port <= 65535 && token != null
                    && !token.isEmpty()) {
                GuardianSessionStore.save(
                        this, port, token, transport);
            }
        }

        GuardianSessionStore.Session session =
                GuardianSessionStore.load(this);
        if (!session.isReady()) {
            stopSelf(startId);
            return START_NOT_STICKY;
        }

        stopRequested = false;
        startForeground(NOTIFICATION_ID, buildNotification(false));
        restartConnectionLoop(session);
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        stopRequested = true;
        generation.incrementAndGet();
        cancelPendingCleanup();
        closeCurrentSocket();
        executor.shutdownNow();
        super.onDestroy();
    }

    private void restartConnectionLoop(
            GuardianSessionStore.Session session) {
        int currentGeneration = generation.incrementAndGet();
        cancelPendingCleanup();
        closeCurrentSocket();
        executor.execute(() -> connectionLoop(
                session, currentGeneration));
    }

    private void connectionLoop(
            GuardianSessionStore.Session session,
            int currentGeneration) {
        while (!stopRequested
                && generation.get() == currentGeneration
                && session.matches(GuardianSessionStore.load(this))) {
            try (Socket socket = new Socket()) {
                currentSocket = socket;
                socket.connect(new InetSocketAddress(
                        InetAddress.getLoopbackAddress(), session.port), 2000);
                socket.setTcpNoDelay(true);
                socket.setSoTimeout(10000);
                try (DataOutputStream output = new DataOutputStream(
                        new BufferedOutputStream(socket.getOutputStream()));
                     DataInputStream input = new DataInputStream(
                             new BufferedInputStream(socket.getInputStream()))) {
                    output.writeInt(MAGIC);
                    output.writeInt(VERSION);
                    writeString(output, session.token);
                    output.flush();
                    if (input.read() != 1) {
                        throw new IOException(
                                "DX Manager rejected the guardian session.");
                    }

                    markConnected(true, session);
                    while (!stopRequested
                            && generation.get() == currentGeneration) {
                        int command = input.read();
                        if (command < 0) throw new EOFException();
                        if (command == COMMAND_PING) {
                            continue;
                        }
                        if (command == COMMAND_WINDOWS_SHUTDOWN) {
                            boolean removeOverlay = input.read() == 1;
                            boolean restoreStayAwake = input.read() == 1;
                            String originalStayAwake = readString(input);
                            handleWindowsShutdown(
                                    removeOverlay,
                                    restoreStayAwake,
                                    originalStayAwake);
                            return;
                        }
                        if (command == COMMAND_STOP_MONITORING) {
                            stopMonitoring();
                            return;
                        }
                        throw new IOException(
                                "Unknown guardian command: " + command);
                    }
                }
            } catch (IOException | RuntimeException exception) {
                if (!stopRequested
                        && generation.get() == currentGeneration) {
                    Log.d(TAG, "DX Manager guardian connection lost.",
                            exception);
                }
            } finally {
                currentSocket = null;
                if (!stopRequested
                        && generation.get() == currentGeneration) {
                    markConnected(false, session);
                }
            }

            if (stopRequested || generation.get() != currentGeneration) {
                return;
            }
            try {
                Thread.sleep(RECONNECT_DELAY_MS);
            } catch (InterruptedException interrupted) {
                Thread.currentThread().interrupt();
                return;
            }
        }
    }

    private void markConnected(boolean value,
                               GuardianSessionStore.Session session) {
        connected = value;
        if (value) {
            cancelPendingCleanup();
        } else if (pendingCleanup == null
                && !isConfiguredTransportConnected(session)) {
            scheduleDisconnectCleanup(session);
        }
        getSystemService(NotificationManager.class).notify(
                NOTIFICATION_ID, buildNotification(value));
    }

    private void scheduleDisconnectCleanup(
            GuardianSessionStore.Session scheduledSession) {
        cancelPendingCleanup();
        int delaySeconds = GuardianPreferences.loadDelaySeconds(this);
        if (delaySeconds == GuardianPreferences.DISABLED) {
            return;
        }
        if (!scheduledSession.isReady()) {
            return;
        }
        pendingCleanup = () -> {
            pendingCleanup = null;
            if (connected || stopRequested
                    || isConfiguredTransportConnected(scheduledSession)
                    || !scheduledSession.matches(
                    GuardianSessionStore.load(this))) {
                return;
            }
            CleanupCoordinator.cleanup(this, true, true);
            CleanupWidgetProvider.updateAll(this);
            CleanupTileService.requestRefresh(this);
            GuardianSessionStore.clear(this);
            stopRequested = true;
            generation.incrementAndGet();
            closeCurrentSocket();
            stopForeground(true);
            stopSelf();
        };
        mainHandler.postDelayed(
                pendingCleanup, delaySeconds * 1000L);
    }

    private boolean isConfiguredTransportConnected(
            GuardianSessionStore.Session session) {
        if (session == null) {
            return true;
        }
        if (GuardianSessionStore.TRANSPORT_USB.equals(session.transport)) {
            Intent state = registerReceiver(
                    null, new IntentFilter(ACTION_USB_STATE));
            return state != null
                    && state.getBooleanExtra(EXTRA_USB_CONNECTED, false);
        }
        if (GuardianSessionStore.TRANSPORT_WIRELESS.equals(
                session.transport)) {
            ConnectivityManager manager =
                    (ConnectivityManager) getSystemService(
                            CONNECTIVITY_SERVICE);
            if (manager == null) {
                return true;
            }
            for (Network network : manager.getAllNetworks()) {
                NetworkCapabilities capabilities =
                        manager.getNetworkCapabilities(network);
                if (capabilities != null && capabilities.hasTransport(
                        NetworkCapabilities.TRANSPORT_WIFI)) {
                    return true;
                }
            }
            return false;
        }
        // Unknown transport is kept conservatively. A manual cleanup or an
        // authenticated Windows-shutdown command can still remove it.
        return true;
    }

    private void handleWindowsShutdown(
            boolean removeOverlay,
            boolean restoreStayAwake,
            String originalStayAwake) {
        stopRequested = true;
        generation.incrementAndGet();
        cancelPendingCleanup();
        if (removeOverlay) {
            OverlayDisplayRepository.cleanup(this);
        }
        if (restoreStayAwake) {
            StayAwakeRepository.restore(this, originalStayAwake);
        }
        CleanupWidgetProvider.updateAll(this);
        CleanupTileService.requestRefresh(this);
        GuardianSessionStore.clear(this);
        stopForeground(true);
        stopSelf();
    }

    private void stopMonitoring() {
        stopRequested = true;
        generation.incrementAndGet();
        cancelPendingCleanup();
        GuardianSessionStore.clear(this);
        stopForeground(true);
        stopSelf();
    }

    private void cancelPendingCleanup() {
        Runnable cleanup = pendingCleanup;
        pendingCleanup = null;
        if (cleanup != null) {
            mainHandler.removeCallbacks(cleanup);
        }
    }

    private void closeCurrentSocket() {
        Socket socket = currentSocket;
        currentSocket = null;
        if (socket != null) {
            try {
                socket.close();
            } catch (IOException ignored) {
            }
        }
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }
        NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                getString(R.string.guardian_channel_name),
                NotificationManager.IMPORTANCE_LOW);
        channel.setDescription(getString(
                R.string.guardian_channel_description));
        getSystemService(NotificationManager.class)
                .createNotificationChannel(channel);
    }

    private Notification buildNotification(boolean ready) {
        Intent launchIntent = new Intent(this, MainActivity.class);
        PendingIntent launch = PendingIntent.getActivity(
                this,
                1401,
                launchIntent,
                PendingIntent.FLAG_UPDATE_CURRENT |
                        PendingIntent.FLAG_IMMUTABLE);
        Notification.Builder builder = Build.VERSION.SDK_INT >=
                Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        return builder
                .setSmallIcon(R.drawable.ic_tile_cleanup)
                .setContentTitle(getString(
                        R.string.guardian_notification_title))
                .setContentText(getString(ready
                        ? R.string.guardian_notification_connected
                        : R.string.guardian_notification_waiting))
                .setContentIntent(launch)
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .build();
    }

    private static void writeString(DataOutputStream output, String value)
            throws IOException {
        byte[] bytes = (value == null ? "" : value)
                .getBytes(StandardCharsets.UTF_8);
        output.writeInt(bytes.length);
        output.write(bytes);
    }

    private static String readString(DataInputStream input)
            throws IOException {
        int length = input.readInt();
        if (length < 0 || length > MAX_STRING_BYTES) {
            throw new IOException("Invalid guardian string length.");
        }
        byte[] bytes = new byte[length];
        input.readFully(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }
}

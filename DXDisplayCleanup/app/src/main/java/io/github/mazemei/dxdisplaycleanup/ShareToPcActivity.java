package io.github.mazemei.dxdisplaycleanup;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.widget.Toast;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class ShareToPcActivity extends Activity {
    private static final int TRANSFER_PROBE_TIMEOUT_MS = 1500;
    private final ExecutorService probeExecutor =
            Executors.newSingleThreadExecutor();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        TransferSessionStore.Session session =
                TransferSessionStore.load(this);
        if (!session.isReady()) {
            Toast.makeText(this, R.string.transfer_pc_not_ready,
                    Toast.LENGTH_LONG).show();
            finish();
            return;
        }

        ArrayList<Uri> uris = collectUris(getIntent());
        if (uris.isEmpty()) {
            Toast.makeText(this, R.string.transfer_no_items,
                    Toast.LENGTH_LONG).show();
            finish();
            return;
        }

        probeExecutor.execute(() -> {
            boolean ready = TransferSessionProbe.isReceiverReady(
                    session, TRANSFER_PROBE_TIMEOUT_MS);
            runOnUiThread(() -> {
                if (!ready) {
                    Toast.makeText(this, R.string.transfer_pc_not_ready,
                            Toast.LENGTH_LONG).show();
                } else if (PhoneTransferService.start(this, uris)) {
                    Toast.makeText(this, R.string.transfer_queued,
                            Toast.LENGTH_SHORT).show();
                } else {
                    Toast.makeText(this, R.string.transfer_queue_failed,
                            Toast.LENGTH_LONG).show();
                }
                finish();
            });
        });
    }

    @Override
    protected void onDestroy() {
        probeExecutor.shutdownNow();
        super.onDestroy();
    }

    static ArrayList<Uri> collectUris(Intent intent) {
        Set<Uri> result = new LinkedHashSet<>();
        if (intent == null) {
            return new ArrayList<>();
        }
        if (Intent.ACTION_SEND_MULTIPLE.equals(intent.getAction())) {
            ArrayList<Uri> multiple =
                    intent.getParcelableArrayListExtra(Intent.EXTRA_STREAM);
            if (multiple != null) {
                result.addAll(multiple);
            }
        } else {
            Uri single = intent.getParcelableExtra(Intent.EXTRA_STREAM);
            if (single != null) {
                result.add(single);
            }
        }
        ClipData clip = intent.getClipData();
        if (clip != null) {
            for (int index = 0; index < clip.getItemCount(); index++) {
                Uri uri = clip.getItemAt(index).getUri();
                if (uri != null) {
                    result.add(uri);
                }
            }
        }
        result.remove(null);
        return new ArrayList<>(result);
    }
}

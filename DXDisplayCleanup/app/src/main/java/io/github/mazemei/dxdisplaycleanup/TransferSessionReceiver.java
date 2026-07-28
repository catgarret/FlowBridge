package io.github.mazemei.dxdisplaycleanup;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public final class TransferSessionReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        if (intent == null
                || !TransferSessionStore.ACTION_CONFIGURE.equals(
                intent.getAction())) {
            return;
        }

        boolean enabled = intent.getBooleanExtra("enabled", false);
        int port = intent.getIntExtra(
                "port", TransferSessionStore.DEFAULT_PORT);
        String token = intent.getStringExtra("token");
        TransferSessionStore.save(context, enabled, port, token);
    }
}

package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;

final class TransferSessionStore {
    static final String ACTION_CONFIGURE =
            "io.github.mazemei.dxdisplaycleanup.CONFIGURE_PC_TRANSFER";
    static final int DEFAULT_PORT = 37123;

    static final class Session {
        final boolean enabled;
        final int port;
        final String token;

        Session(boolean enabled, int port, String token) {
            this.enabled = enabled;
            this.port = port;
            this.token = token == null ? "" : token;
        }

        boolean isReady() {
            return enabled && port > 0 && port <= 65535
                    && !token.isEmpty();
        }
    }

    private static final String PREFS = "pc_transfer_session";
    private static final String KEY_ENABLED = "enabled";
    private static final String KEY_PORT = "port";
    private static final String KEY_TOKEN = "token";

    private TransferSessionStore() {
    }

    static Session load(Context context) {
        SharedPreferences preferences = context.getSharedPreferences(
                PREFS, Context.MODE_PRIVATE);
        return new Session(
                preferences.getBoolean(KEY_ENABLED, false),
                preferences.getInt(KEY_PORT, DEFAULT_PORT),
                preferences.getString(KEY_TOKEN, ""));
    }

    static void save(Context context, boolean enabled, int port, String token) {
        SharedPreferences.Editor editor = context.getSharedPreferences(
                PREFS, Context.MODE_PRIVATE).edit();
        if (enabled && port > 0 && port <= 65535 && token != null
                && !token.isEmpty()) {
            editor.putBoolean(KEY_ENABLED, true)
                    .putInt(KEY_PORT, port)
                    .putString(KEY_TOKEN, token);
        } else {
            editor.clear();
        }
        editor.apply();
    }
}

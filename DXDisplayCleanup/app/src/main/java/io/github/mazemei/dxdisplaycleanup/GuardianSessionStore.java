package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;

final class GuardianSessionStore {
    static final int DEFAULT_PORT = 37124;
    static final String TRANSPORT_USB = "usb";
    static final String TRANSPORT_WIRELESS = "wireless";
    static final String TRANSPORT_UNKNOWN = "unknown";

    static final class Session {
        final boolean enabled;
        final int port;
        final String token;
        final String transport;

        Session(boolean enabled, int port, String token, String transport) {
            this.enabled = enabled;
            this.port = port;
            this.token = token == null ? "" : token;
            this.transport = normalizeTransport(transport);
        }

        boolean isReady() {
            return enabled && port > 0 && port <= 65535 && !token.isEmpty();
        }

        boolean matches(Session other) {
            return other != null && enabled == other.enabled
                    && port == other.port && token.equals(other.token)
                    && transport.equals(other.transport);
        }
    }

    private static final String PREFS = "guardian_session";
    private static final String KEY_ENABLED = "enabled";
    private static final String KEY_PORT = "port";
    private static final String KEY_TOKEN = "token";
    private static final String KEY_TRANSPORT = "transport";

    private GuardianSessionStore() {
    }

    static Session load(Context context) {
        SharedPreferences preferences = context.getSharedPreferences(
                PREFS, Context.MODE_PRIVATE);
        return new Session(
                preferences.getBoolean(KEY_ENABLED, false),
                preferences.getInt(KEY_PORT, DEFAULT_PORT),
                preferences.getString(KEY_TOKEN, ""),
                preferences.getString(KEY_TRANSPORT, TRANSPORT_UNKNOWN));
    }

    static void save(Context context, int port, String token,
                     String transport) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putBoolean(KEY_ENABLED, true)
                .putInt(KEY_PORT, port)
                .putString(KEY_TOKEN, token == null ? "" : token)
                .putString(KEY_TRANSPORT, normalizeTransport(transport))
                .apply();
    }

    static void clear(Context context) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .clear()
                .apply();
    }

    private static String normalizeTransport(String transport) {
        if (TRANSPORT_USB.equals(transport)
                || TRANSPORT_WIRELESS.equals(transport)) {
            return transport;
        }
        return TRANSPORT_UNKNOWN;
    }
}

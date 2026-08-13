package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;

final class GuardianPreferences {
    static final int DISABLED = -1;
    static final int DEFAULT_DELAY_SECONDS = 180;
    static final int[] DELAY_VALUES = {
            DISABLED, 30, 60, 180, 300, 600
    };

    private static final String FILE_NAME = "guardian_preferences";
    private static final String KEY_DELAY_SECONDS =
            "disconnect_cleanup_delay_seconds";

    private GuardianPreferences() {
    }

    static int loadDelaySeconds(Context context) {
        SharedPreferences preferences = context.getSharedPreferences(
                FILE_NAME, Context.MODE_PRIVATE);
        int value = preferences.getInt(
                KEY_DELAY_SECONDS, DEFAULT_DELAY_SECONDS);
        for (int candidate : DELAY_VALUES) {
            if (candidate == value) {
                return value;
            }
        }
        return DEFAULT_DELAY_SECONDS;
    }

    static void saveDelaySeconds(Context context, int seconds) {
        boolean supported = false;
        for (int candidate : DELAY_VALUES) {
            if (candidate == seconds) {
                supported = true;
                break;
            }
        }
        if (!supported) {
            throw new IllegalArgumentException(
                    "Unsupported guardian cleanup delay.");
        }
        context.getSharedPreferences(FILE_NAME, Context.MODE_PRIVATE)
                .edit()
                .putInt(KEY_DELAY_SECONDS, seconds)
                .apply();
    }
}

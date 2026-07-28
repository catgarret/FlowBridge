package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;

final class CleanupPreferences {
    private static final String FILE_NAME = "cleanup_preferences";
    private static final String KEY_OVERLAY = "tile_widget_cleanup_overlay";
    private static final String KEY_STAY_AWAKE = "tile_widget_cleanup_stay_awake";

    static final class Targets {
        final boolean overlay;
        final boolean stayAwake;

        Targets(boolean overlay, boolean stayAwake) {
            this.overlay = overlay;
            this.stayAwake = stayAwake;
        }

        boolean any() {
            return overlay || stayAwake;
        }
    }

    private CleanupPreferences() {
    }

    static Targets load(Context context) {
        SharedPreferences preferences = context.getSharedPreferences(
                FILE_NAME, Context.MODE_PRIVATE);
        boolean overlay = preferences.getBoolean(KEY_OVERLAY, true);
        boolean stayAwake = preferences.getBoolean(KEY_STAY_AWAKE, true);
        if (!overlay && !stayAwake) {
            return new Targets(true, true);
        }
        return new Targets(overlay, stayAwake);
    }

    static void save(Context context, boolean overlay, boolean stayAwake) {
        if (!overlay && !stayAwake) {
            throw new IllegalArgumentException("At least one cleanup target is required.");
        }
        context.getSharedPreferences(FILE_NAME, Context.MODE_PRIVATE)
                .edit()
                .putBoolean(KEY_OVERLAY, overlay)
                .putBoolean(KEY_STAY_AWAKE, stayAwake)
                .apply();
    }
}

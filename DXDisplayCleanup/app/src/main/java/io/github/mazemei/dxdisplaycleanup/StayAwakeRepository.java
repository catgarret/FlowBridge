package io.github.mazemei.dxdisplaycleanup;

import android.content.ContentResolver;
import android.content.Context;
import android.provider.Settings;

final class StayAwakeRepository {
    static final String SETTING_NAME = "stay_on_while_plugged_in";

    enum Status {
        ACTIVE,
        INACTIVE,
        PERMISSION_REQUIRED,
        ERROR
    }

    static final class Snapshot {
        final Status status;
        final boolean stayAwakeActive;
        final String rawValue;
        final String error;

        Snapshot(Status status, boolean stayAwakeActive, String rawValue, String error) {
            this.status = status;
            this.stayAwakeActive = stayAwakeActive;
            this.rawValue = rawValue;
            this.error = error;
        }
    }

    private StayAwakeRepository() {
    }

    static Snapshot inspect(Context context) {
        try {
            String raw = Settings.Global.getString(
                    context.getContentResolver(), SETTING_NAME);
            boolean active = isStayAwakeActive(raw);
            if (!OverlayDisplayRepository.hasWritePermission(context)) {
                return new Snapshot(Status.PERMISSION_REQUIRED, active, raw, null);
            }
            return new Snapshot(active ? Status.ACTIVE : Status.INACTIVE,
                    active, raw, null);
        } catch (NumberFormatException invalidValue) {
            return new Snapshot(Status.ERROR, false, null,
                    "Invalid stay-awake setting value.");
        } catch (RuntimeException exception) {
            return new Snapshot(Status.ERROR, false, null,
                    exception.getClass().getSimpleName() + ": " + exception.getMessage());
        }
    }

    static Snapshot cleanup(Context context) {
        if (!OverlayDisplayRepository.hasWritePermission(context)) {
            return inspect(context);
        }

        ContentResolver resolver = context.getContentResolver();
        try {
            if (!Settings.Global.putInt(resolver, SETTING_NAME, 0)) {
                return new Snapshot(Status.ERROR, true, null,
                        "The Settings provider rejected the update request.");
            }
        } catch (SecurityException denied) {
            return new Snapshot(Status.PERMISSION_REQUIRED, true, null,
                    denied.getMessage());
        } catch (RuntimeException exception) {
            return new Snapshot(Status.ERROR, true, null,
                    exception.getClass().getSimpleName() + ": " + exception.getMessage());
        }

        Snapshot verified = inspect(context);
        if (verified.status == Status.INACTIVE) {
            return verified;
        }
        if (verified.status == Status.ACTIVE) {
            return new Snapshot(Status.ERROR, true, verified.rawValue,
                    "The stay-awake setting remained enabled after the update.");
        }
        return verified;
    }

    static boolean isStayAwakeActive(String value) {
        if (value == null || value.trim().isEmpty()) {
            return false;
        }
        return Long.parseLong(value.trim()) != 0L;
    }
}

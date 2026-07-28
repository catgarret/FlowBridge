package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;

final class CleanupCoordinator {
    static final class Snapshot {
        final OverlayDisplayRepository.Snapshot overlay;
        final StayAwakeRepository.Snapshot stayAwake;

        Snapshot(OverlayDisplayRepository.Snapshot overlay,
                 StayAwakeRepository.Snapshot stayAwake) {
            this.overlay = overlay;
            this.stayAwake = stayAwake;
        }

        boolean permissionRequired() {
            return overlay.status == OverlayDisplayRepository.Status.PERMISSION_REQUIRED
                    || stayAwake.status == StayAwakeRepository.Status.PERMISSION_REQUIRED;
        }

        boolean hasError() {
            return overlay.status == OverlayDisplayRepository.Status.ERROR
                    || stayAwake.status == StayAwakeRepository.Status.ERROR;
        }

        boolean anyActive() {
            return overlay.overlayActive || stayAwake.stayAwakeActive;
        }

        boolean selectedActive(CleanupPreferences.Targets targets) {
            return (targets.overlay && overlay.overlayActive)
                    || (targets.stayAwake && stayAwake.stayAwakeActive);
        }
    }

    private CleanupCoordinator() {
    }

    static Snapshot inspect(Context context) {
        return new Snapshot(
                OverlayDisplayRepository.inspect(context),
                StayAwakeRepository.inspect(context));
    }

    static Snapshot cleanup(Context context, boolean overlay, boolean stayAwake) {
        if (overlay) {
            OverlayDisplayRepository.cleanup(context);
        }
        if (stayAwake) {
            StayAwakeRepository.cleanup(context);
        }
        return inspect(context);
    }

    static Snapshot cleanupSelected(Context context) {
        CleanupPreferences.Targets targets = CleanupPreferences.load(context);
        return cleanup(context, targets.overlay, targets.stayAwake);
    }
}

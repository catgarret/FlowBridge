package io.github.mazemei.dxdisplaycleanup;

import android.annotation.SuppressLint;
import android.app.PendingIntent;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.service.quicksettings.Tile;
import android.service.quicksettings.TileService;

public final class CleanupTileService extends TileService {
    @Override
    public void onStartListening() {
        super.onStartListening();
        refreshTile();
    }

    @Override
    public void onClick() {
        super.onClick();
        CleanupCoordinator.Snapshot before = CleanupCoordinator.inspect(this);
        if (before.permissionRequired()) {
            openMainActivity();
            return;
        }
        CleanupPreferences.Targets targets = CleanupPreferences.load(this);
        if (before.selectedActive(targets)) {
            CleanupCoordinator.Snapshot after =
                    CleanupCoordinator.cleanupSelected(this);
            SessionGuardianService.stopAfterManualCleanupIfIdle(this, after);
        }
        CleanupWidgetProvider.updateAll(this);
        refreshTile();
    }

    private void refreshTile() {
        Tile tile = getQsTile();
        if (tile == null) {
            return;
        }

        CleanupCoordinator.Snapshot snapshot = CleanupCoordinator.inspect(this);
        CleanupPreferences.Targets targets = CleanupPreferences.load(this);
        tile.setLabel(getString(R.string.tile_label));
        if (snapshot.permissionRequired()) {
            tile.setState(Tile.STATE_UNAVAILABLE);
            setSubtitle(tile, getString(R.string.widget_permission));
        } else if (snapshot.hasError()) {
            tile.setState(Tile.STATE_UNAVAILABLE);
            setSubtitle(tile, getString(R.string.widget_error));
        } else if (snapshot.selectedActive(targets)) {
            tile.setState(Tile.STATE_ACTIVE);
            setSubtitle(tile, getString(R.string.tile_active));
        } else {
            tile.setState(Tile.STATE_INACTIVE);
            setSubtitle(tile, getString(R.string.tile_inactive));
        }
        tile.updateTile();
    }

    private static void setSubtitle(Tile tile, CharSequence subtitle) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            tile.setSubtitle(subtitle);
        }
    }

    @SuppressLint("StartActivityAndCollapseDeprecated")
    private void openMainActivity() {
        Intent intent = new Intent(this, MainActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        if (Build.VERSION.SDK_INT >= 34) {
            PendingIntent pendingIntent = PendingIntent.getActivity(
                    this,
                    0,
                    intent,
                    PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            startActivityAndCollapse(pendingIntent);
        } else {
            startActivityAndCollapse(intent);
        }
    }

    static void requestRefresh(Context context) {
        requestListeningState(
                context,
                new ComponentName(context, CleanupTileService.class));
    }
}

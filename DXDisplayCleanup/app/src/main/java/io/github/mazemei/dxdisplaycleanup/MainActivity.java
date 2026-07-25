package io.github.mazemei.dxdisplaycleanup;

import android.app.Activity;
import android.database.ContentObserver;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

public final class MainActivity extends Activity {
    private ImageView statusIcon;
    private TextView statusTitle;
    private TextView statusDetail;
    private TextView detectedValue;
    private TextView permissionCommand;
    private Button cleanupButton;
    private ContentObserver settingObserver;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        statusIcon = findViewById(R.id.status_icon);
        statusTitle = findViewById(R.id.status_title);
        statusDetail = findViewById(R.id.status_detail);
        detectedValue = findViewById(R.id.detected_value);
        permissionCommand = findViewById(R.id.permission_command);
        cleanupButton = findViewById(R.id.cleanup_button);
        Button refreshButton = findViewById(R.id.refresh_button);

        permissionCommand.setText(getString(
                R.string.permission_command,
                getPackageName(),
                OverlayDisplayRepository.REQUIRED_PERMISSION));
        cleanupButton.setOnClickListener(view -> cleanup());
        refreshButton.setOnClickListener(view -> refresh());

        settingObserver = new ContentObserver(new Handler(Looper.getMainLooper())) {
            @Override
            public void onChange(boolean selfChange) {
                refresh();
            }

            @Override
            public void onChange(boolean selfChange, Uri uri) {
                refresh();
            }
        };
        refresh();
    }

    @Override
    protected void onStart() {
        super.onStart();
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(OverlayDisplayRepository.SETTING_NAME),
                false,
                settingObserver);
        refresh();
    }

    @Override
    protected void onStop() {
        getContentResolver().unregisterContentObserver(settingObserver);
        super.onStop();
    }

    private void cleanup() {
        OverlayDisplayRepository.Snapshot snapshot =
                OverlayDisplayRepository.cleanup(this);
        render(snapshot);
        CleanupWidgetProvider.updateAll(this);
        CleanupTileService.requestRefresh(this);

        if (snapshot.status == OverlayDisplayRepository.Status.INACTIVE) {
            Toast.makeText(this, R.string.cleanup_success, Toast.LENGTH_SHORT).show();
        } else if (snapshot.status == OverlayDisplayRepository.Status.PERMISSION_REQUIRED) {
            Toast.makeText(this, R.string.permission_required_short,
                    Toast.LENGTH_LONG).show();
        } else if (snapshot.status == OverlayDisplayRepository.Status.ERROR) {
            Toast.makeText(this, R.string.cleanup_failed, Toast.LENGTH_LONG).show();
        }
    }

    private void refresh() {
        OverlayDisplayRepository.Snapshot snapshot =
                OverlayDisplayRepository.inspect(this);
        render(snapshot);
        CleanupWidgetProvider.updateAll(this);
        CleanupTileService.requestRefresh(this);
    }

    private void render(OverlayDisplayRepository.Snapshot snapshot) {
        detectedValue.setText(snapshot.rawValue == null
                ? getString(R.string.setting_not_present)
                : snapshot.rawValue);

        switch (snapshot.status) {
            case ACTIVE:
                statusIcon.setImageResource(R.drawable.dx_manager_icon);
                statusIcon.clearColorFilter();
                statusTitle.setText(R.string.status_active);
                statusDetail.setText(R.string.status_active_detail);
                cleanupButton.setEnabled(true);
                break;
            case INACTIVE:
                statusIcon.setImageResource(R.drawable.dx_manager_icon_mono);
                statusIcon.clearColorFilter();
                statusTitle.setText(R.string.status_inactive);
                statusDetail.setText(R.string.status_inactive_detail);
                cleanupButton.setEnabled(false);
                break;
            case PERMISSION_REQUIRED:
                statusIcon.setImageResource(R.drawable.ic_warning);
                statusIcon.setColorFilter(Color.rgb(245, 158, 11));
                statusTitle.setText(R.string.status_permission_required);
                statusDetail.setText(snapshot.overlayActive
                        ? R.string.status_permission_required_active
                        : R.string.status_permission_required_detail);
                cleanupButton.setEnabled(false);
                break;
            default:
                statusIcon.setImageResource(R.drawable.ic_warning);
                statusIcon.setColorFilter(Color.rgb(220, 38, 38));
                statusTitle.setText(R.string.status_error);
                statusDetail.setText(snapshot.error == null
                        ? getString(R.string.status_error_detail)
                        : snapshot.error);
                cleanupButton.setEnabled(false);
                break;
        }
    }
}

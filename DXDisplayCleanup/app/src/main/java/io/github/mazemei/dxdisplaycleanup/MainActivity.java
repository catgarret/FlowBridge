package io.github.mazemei.dxdisplaycleanup;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.database.ContentObserver;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

public final class MainActivity extends Activity {
    private static final int REQUEST_FILES = 101;
    private static final int REQUEST_FOLDER = 102;
    private static final int REQUEST_NOTIFICATIONS = 103;

    private ImageView statusIcon;
    private TextView statusTitle;
    private TextView statusDetail;
    private TextView overlayStatus;
    private TextView overlayValue;
    private TextView stayAwakeStatus;
    private TextView stayAwakeValue;
    private Button cleanupOverlayButton;
    private Button cleanupStayAwakeButton;
    private Button cleanupBothButton;
    private CheckBox tileWidgetOverlay;
    private CheckBox tileWidgetStayAwake;
    private TextView pcTransferStatus;
    private Button sendFilesButton;
    private Button sendFolderButton;
    private ContentObserver settingObserver;
    private boolean updatingPreferences;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        statusIcon = findViewById(R.id.status_icon);
        statusTitle = findViewById(R.id.status_title);
        statusDetail = findViewById(R.id.status_detail);
        overlayStatus = findViewById(R.id.overlay_status);
        overlayValue = findViewById(R.id.overlay_value);
        stayAwakeStatus = findViewById(R.id.stay_awake_status);
        stayAwakeValue = findViewById(R.id.stay_awake_value);
        cleanupOverlayButton = findViewById(R.id.cleanup_overlay_button);
        cleanupStayAwakeButton = findViewById(R.id.cleanup_stay_awake_button);
        cleanupBothButton = findViewById(R.id.cleanup_both_button);
        tileWidgetOverlay = findViewById(R.id.tile_widget_overlay);
        tileWidgetStayAwake = findViewById(R.id.tile_widget_stay_awake);
        pcTransferStatus = findViewById(R.id.pc_transfer_status);
        sendFilesButton = findViewById(R.id.send_files_button);
        sendFolderButton = findViewById(R.id.send_folder_button);
        Button refreshButton = findViewById(R.id.refresh_button);

        cleanupOverlayButton.setOnClickListener(view -> cleanup(true, false));
        cleanupStayAwakeButton.setOnClickListener(view -> cleanup(false, true));
        cleanupBothButton.setOnClickListener(view -> cleanup(true, true));
        refreshButton.setOnClickListener(view -> refresh());
        sendFilesButton.setOnClickListener(view -> chooseFiles());
        sendFolderButton.setOnClickListener(view -> chooseFolder());

        CleanupPreferences.Targets targets = CleanupPreferences.load(this);
        setPreferenceChecks(targets);
        tileWidgetOverlay.setOnCheckedChangeListener((button, checked) ->
                savePreferenceChecks(button.getId()));
        tileWidgetStayAwake.setOnCheckedChangeListener((button, checked) ->
                savePreferenceChecks(button.getId()));

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
        requestNotificationPermissionIfNeeded();
        refresh();
    }

    @Override
    protected void onStart() {
        super.onStart();
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(OverlayDisplayRepository.SETTING_NAME),
                false,
                settingObserver);
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(StayAwakeRepository.SETTING_NAME),
                false,
                settingObserver);
        refresh();
    }

    @Override
    protected void onStop() {
        getContentResolver().unregisterContentObserver(settingObserver);
        super.onStop();
    }

    private void cleanup(boolean overlay, boolean stayAwake) {
        CleanupCoordinator.Snapshot snapshot =
                CleanupCoordinator.cleanup(this, overlay, stayAwake);
        render(snapshot);
        notifySurfaces();

        if (snapshot.permissionRequired()) {
            Toast.makeText(this, R.string.permission_required_short,
                    Toast.LENGTH_LONG).show();
        } else if (snapshot.hasError()
                || (overlay && snapshot.overlay.overlayActive)
                || (stayAwake && snapshot.stayAwake.stayAwakeActive)) {
            Toast.makeText(this, R.string.cleanup_failed, Toast.LENGTH_LONG).show();
        } else {
            int message = overlay && stayAwake
                    ? R.string.cleanup_both_success
                    : overlay
                    ? R.string.cleanup_overlay_success
                    : R.string.cleanup_stay_awake_success;
            Toast.makeText(this, message, Toast.LENGTH_SHORT).show();
        }
    }

    private void refresh() {
        render(CleanupCoordinator.inspect(this));
        renderTransferSession();
        notifySurfaces();
    }

    private void renderTransferSession() {
        boolean ready = TransferSessionStore.load(this).isReady();
        pcTransferStatus.setText(ready
                ? R.string.transfer_pc_ready
                : R.string.transfer_pc_not_ready_short);
        sendFilesButton.setEnabled(ready);
        sendFolderButton.setEnabled(ready);
    }

    private void chooseFiles() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT)
                .addCategory(Intent.CATEGORY_OPENABLE)
                .setType("*/*")
                .putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_FILES);
    }

    private void requestNotificationPermissionIfNeeded() {
        if (android.os.Build.VERSION.SDK_INT >= 33
                && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(
                    new String[]{Manifest.permission.POST_NOTIFICATIONS},
                    REQUEST_NOTIFICATIONS);
        }
    }

    private void chooseFolder() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION
                        | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_FOLDER);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode,
            Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if ((requestCode != REQUEST_FILES && requestCode != REQUEST_FOLDER)
                || resultCode != RESULT_OK || data == null) {
            return;
        }
        java.util.ArrayList<Uri> uris =
                ShareToPcActivity.collectUris(data);
        if (data.getData() != null && !uris.contains(data.getData())) {
            uris.add(data.getData());
        }
        if (uris.isEmpty()) {
            Toast.makeText(this, R.string.transfer_no_items,
                    Toast.LENGTH_LONG).show();
            return;
        }
        for (Uri uri : uris) {
            try {
                getContentResolver().takePersistableUriPermission(
                        uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } catch (SecurityException ignored) {
            }
        }
        PhoneTransferService.start(this, uris);
        Toast.makeText(this, R.string.transfer_queued,
                Toast.LENGTH_SHORT).show();
    }

    private void notifySurfaces() {
        CleanupWidgetProvider.updateAll(this);
        CleanupTileService.requestRefresh(this);
    }

    private void render(CleanupCoordinator.Snapshot snapshot) {
        overlayValue.setText(snapshot.overlay.rawValue == null
                ? getString(R.string.setting_not_present)
                : snapshot.overlay.rawValue);
        stayAwakeValue.setText(snapshot.stayAwake.rawValue == null
                ? "0"
                : snapshot.stayAwake.rawValue);

        overlayStatus.setText(snapshot.overlay.overlayActive
                ? R.string.target_status_active
                : R.string.target_status_inactive);
        stayAwakeStatus.setText(snapshot.stayAwake.stayAwakeActive
                ? R.string.target_status_active
                : R.string.target_status_inactive);

        boolean permissionGranted = !snapshot.permissionRequired();
        boolean valid = permissionGranted && !snapshot.hasError();
        cleanupOverlayButton.setEnabled(valid && snapshot.overlay.overlayActive);
        cleanupStayAwakeButton.setEnabled(valid && snapshot.stayAwake.stayAwakeActive);
        cleanupBothButton.setEnabled(valid && snapshot.anyActive());

        if (snapshot.permissionRequired()) {
            statusIcon.setImageResource(R.drawable.ic_warning);
            statusIcon.setColorFilter(Color.rgb(245, 158, 11));
            statusTitle.setText(R.string.status_permission_required);
            statusDetail.setText(R.string.status_permission_required_detail);
        } else if (snapshot.hasError()) {
            statusIcon.setImageResource(R.drawable.ic_warning);
            statusIcon.setColorFilter(Color.rgb(220, 38, 38));
            statusTitle.setText(R.string.status_error);
            String error = snapshot.overlay.error != null
                    ? snapshot.overlay.error
                    : snapshot.stayAwake.error;
            statusDetail.setText(error == null
                    ? getString(R.string.status_error_detail)
                    : error);
        } else if (snapshot.anyActive()) {
            statusIcon.setImageResource(R.drawable.dx_manager_icon);
            statusIcon.clearColorFilter();
            statusTitle.setText(R.string.status_cleanup_needed);
            if (snapshot.overlay.overlayActive && snapshot.stayAwake.stayAwakeActive) {
                statusDetail.setText(R.string.status_both_active_detail);
            } else if (snapshot.overlay.overlayActive) {
                statusDetail.setText(R.string.status_overlay_active_detail);
            } else {
                statusDetail.setText(R.string.status_stay_awake_active_detail);
            }
        } else {
            statusIcon.setImageResource(R.drawable.dx_manager_icon_mono);
            statusIcon.clearColorFilter();
            statusTitle.setText(R.string.status_clean);
            statusDetail.setText(R.string.status_clean_detail);
        }
    }

    private void setPreferenceChecks(CleanupPreferences.Targets targets) {
        updatingPreferences = true;
        tileWidgetOverlay.setChecked(targets.overlay);
        tileWidgetStayAwake.setChecked(targets.stayAwake);
        updatingPreferences = false;
    }

    private void savePreferenceChecks(int changedViewId) {
        if (updatingPreferences) {
            return;
        }

        boolean overlay = tileWidgetOverlay.isChecked();
        boolean stayAwake = tileWidgetStayAwake.isChecked();
        if (!overlay && !stayAwake) {
            updatingPreferences = true;
            if (changedViewId == R.id.tile_widget_overlay) {
                tileWidgetOverlay.setChecked(true);
            } else {
                tileWidgetStayAwake.setChecked(true);
            }
            updatingPreferences = false;
            Toast.makeText(this, R.string.cleanup_target_required,
                    Toast.LENGTH_SHORT).show();
            return;
        }

        CleanupPreferences.save(this, overlay, stayAwake);
        notifySurfaces();
    }
}

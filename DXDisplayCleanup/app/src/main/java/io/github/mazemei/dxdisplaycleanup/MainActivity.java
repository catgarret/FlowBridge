package io.github.mazemei.dxdisplaycleanup;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.database.ContentObserver;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowInsets;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.ImageView;
import android.widget.AdapterView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class MainActivity extends Activity {
    private static final int REQUEST_FILES = 101;
    private static final int REQUEST_FOLDER = 102;
    private static final int REQUEST_NOTIFICATIONS = 103;
    private static final int TRANSFER_PROBE_TIMEOUT_MS = 1000;
    private static final long TRANSFER_PROBE_INTERVAL_MS = 2000L;

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
    private Spinner guardianCleanupDelay;
    private TextView pcTransferStatus;
    private Button sendFilesButton;
    private Button sendFolderButton;
    private View pageHome;
    private View pageQuickSettings;
    private View pageFileTransfer;
    private TextView navHome;
    private TextView navQuickSettings;
    private TextView navFileTransfer;
    private View pageContainer;
    private ContentObserver settingObserver;
    private boolean updatingPreferences;
    private Page currentPage = Page.HOME;
    private float swipeStartX;
    private float swipeStartY;
    private boolean pageDragging;
    private Page dragTargetPage;
    private View dragTargetView;
    private final Handler transferStatusHandler =
            new Handler(Looper.getMainLooper());
    private final ExecutorService transferStatusExecutor =
            Executors.newSingleThreadExecutor();
    private boolean transferStatusMonitoring;
    private boolean transferProbeRunning;
    private int transferProbeGeneration;
    private final Runnable transferStatusPoll = new Runnable() {
        @Override
        public void run() {
            if (!transferStatusMonitoring) {
                return;
            }
            requestTransferSessionProbe();
            transferStatusHandler.postDelayed(
                    this, TRANSFER_PROBE_INTERVAL_MS);
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        View rootLayout = findViewById(R.id.root_layout);
        rootLayout.setOnApplyWindowInsetsListener((view, insets) -> {
            int statusBarInset;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                statusBarInset = insets.getInsets(
                        WindowInsets.Type.statusBars()).top;
            } else {
                statusBarInset = insets.getSystemWindowInsetTop();
            }
            view.setPadding(
                    view.getPaddingLeft(),
                    statusBarInset,
                    view.getPaddingRight(),
                    view.getPaddingBottom());
            return insets;
        });
        rootLayout.requestApplyInsets();

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
        guardianCleanupDelay = findViewById(R.id.guardian_cleanup_delay);
        pcTransferStatus = findViewById(R.id.pc_transfer_status);
        sendFilesButton = findViewById(R.id.send_files_button);
        sendFolderButton = findViewById(R.id.send_folder_button);
        pageHome = findViewById(R.id.page_home);
        pageQuickSettings = findViewById(R.id.page_quick_settings);
        pageFileTransfer = findViewById(R.id.page_file_transfer);
        navHome = findViewById(R.id.nav_home);
        navQuickSettings = findViewById(R.id.nav_quick_settings);
        navFileTransfer = findViewById(R.id.nav_file_transfer);
        pageContainer = findViewById(R.id.page_container);
        Button refreshButton = findViewById(R.id.refresh_button);

        cleanupOverlayButton.setOnClickListener(view -> cleanup(true, false));
        cleanupStayAwakeButton.setOnClickListener(view -> cleanup(false, true));
        cleanupBothButton.setOnClickListener(view -> cleanup(true, true));
        refreshButton.setOnClickListener(view -> refresh());
        sendFilesButton.setOnClickListener(view -> chooseFiles());
        sendFolderButton.setOnClickListener(view -> chooseFolder());
        navHome.setOnClickListener(view -> selectPage(Page.HOME));
        navQuickSettings.setOnClickListener(view -> selectPage(Page.QUICK_SETTINGS));
        navFileTransfer.setOnClickListener(view -> selectPage(Page.FILE_TRANSFER));
        selectPage(Page.HOME);

        CleanupPreferences.Targets targets = CleanupPreferences.load(this);
        setPreferenceChecks(targets);
        tileWidgetOverlay.setOnCheckedChangeListener((button, checked) ->
                savePreferenceChecks(button.getId()));
        tileWidgetStayAwake.setOnCheckedChangeListener((button, checked) ->
                savePreferenceChecks(button.getId()));
        configureGuardianCleanupDelay();

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
        transferStatusMonitoring = true;
        setTransferSessionReady(false);
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(OverlayDisplayRepository.SETTING_NAME),
                false,
                settingObserver);
        getContentResolver().registerContentObserver(
                Settings.Global.getUriFor(StayAwakeRepository.SETTING_NAME),
                false,
                settingObserver);
        refresh();
        transferStatusHandler.removeCallbacks(transferStatusPoll);
        transferStatusHandler.post(transferStatusPoll);
    }

    private void configureGuardianCleanupDelay() {
        int saved = GuardianPreferences.loadDelaySeconds(this);
        int selected = 0;
        for (int index = 0;
             index < GuardianPreferences.DELAY_VALUES.length;
             index++) {
            if (GuardianPreferences.DELAY_VALUES[index] == saved) {
                selected = index;
                break;
            }
        }
        guardianCleanupDelay.setSelection(selected, false);
        guardianCleanupDelay.setOnItemSelectedListener(
                new AdapterView.OnItemSelectedListener() {
                    @Override
                    public void onItemSelected(AdapterView<?> parent,
                                               View view,
                                               int position,
                                               long id) {
                        if (position >= 0 && position <
                                GuardianPreferences.DELAY_VALUES.length) {
                            GuardianPreferences.saveDelaySeconds(
                                    MainActivity.this,
                                    GuardianPreferences.DELAY_VALUES[position]);
                        }
                    }

                    @Override
                    public void onNothingSelected(AdapterView<?> parent) {
                    }
                });
    }

    @Override
    protected void onStop() {
        transferStatusMonitoring = false;
        transferProbeGeneration++;
        transferStatusHandler.removeCallbacks(transferStatusPoll);
        getContentResolver().unregisterContentObserver(settingObserver);
        super.onStop();
    }

    @Override
    protected void onDestroy() {
        transferStatusExecutor.shutdownNow();
        super.onDestroy();
    }

    private void cleanup(boolean overlay, boolean stayAwake) {
        CleanupCoordinator.Snapshot snapshot =
                CleanupCoordinator.cleanup(this, overlay, stayAwake);
        SessionGuardianService.stopAfterManualCleanupIfIdle(this, snapshot);
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
        if (!TransferSessionStore.load(this).isReady()) {
            setTransferSessionReady(false);
        }
        requestTransferSessionProbe();
    }

    private void requestTransferSessionProbe() {
        if (!transferStatusMonitoring || transferProbeRunning) {
            return;
        }
        TransferSessionStore.Session session =
                TransferSessionStore.load(this);
        if (!session.isReady()) {
            setTransferSessionReady(false);
            return;
        }

        transferProbeRunning = true;
        int generation = ++transferProbeGeneration;
        transferStatusExecutor.execute(() -> {
            boolean ready = TransferSessionProbe.isReceiverReady(
                    session, TRANSFER_PROBE_TIMEOUT_MS);
            transferStatusHandler.post(() -> {
                transferProbeRunning = false;
                if (!transferStatusMonitoring
                        || generation != transferProbeGeneration) {
                    return;
                }
                TransferSessionStore.Session current =
                        TransferSessionStore.load(this);
                setTransferSessionReady(ready && session.matches(current));
            });
        });
    }

    private void setTransferSessionReady(boolean ready) {
        pcTransferStatus.setText(ready
                ? R.string.transfer_pc_ready
                : R.string.transfer_pc_not_ready_short);
        pcTransferStatus.setBackgroundResource(ready
                ? R.drawable.bg_connection_status
                : R.drawable.bg_connection_pending);
        sendFilesButton.setEnabled(ready);
        sendFolderButton.setEnabled(ready);
    }

    private void selectPage(Page selectedPage) {
        currentPage = selectedPage;
        resetPageTransforms();
        pageHome.setVisibility(selectedPage == Page.HOME ? View.VISIBLE : View.GONE);
        pageQuickSettings.setVisibility(
                selectedPage == Page.QUICK_SETTINGS ? View.VISIBLE : View.GONE);
        pageFileTransfer.setVisibility(
                selectedPage == Page.FILE_TRANSFER ? View.VISIBLE : View.GONE);

        updateNavigationItem(navHome, selectedPage == Page.HOME);
        updateNavigationItem(navQuickSettings,
                selectedPage == Page.QUICK_SETTINGS);
        updateNavigationItem(navFileTransfer,
                selectedPage == Page.FILE_TRANSFER);
    }

    private void updateNavigationItem(TextView view, boolean selected) {
        view.setBackgroundResource(selected
                ? R.drawable.bg_nav_selected
                : android.R.color.transparent);
        view.setTextColor(getColor(selected
                ? R.color.accent
                : R.color.text_secondary));
        view.setTypeface(null, selected ? Typeface.BOLD : Typeface.NORMAL);
        view.setSelected(selected);
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent event) {
        int action = event.getActionMasked();
        if (action == MotionEvent.ACTION_DOWN) {
            swipeStartX = event.getX();
            swipeStartY = event.getY();
            pageDragging = false;
            dragTargetPage = null;
            dragTargetView = null;
            return super.dispatchTouchEvent(event);
        }

        float deltaX = event.getX() - swipeStartX;
        float deltaY = event.getY() - swipeStartY;
        if (action == MotionEvent.ACTION_MOVE) {
            float startThreshold = 10f * getResources().getDisplayMetrics().density;
            if (!pageDragging
                    && Math.abs(deltaX) >= startThreshold
                    && Math.abs(deltaX) > Math.abs(deltaY) * 1.35f) {
                beginPageDrag(deltaX);
                MotionEvent cancel = MotionEvent.obtain(event);
                cancel.setAction(MotionEvent.ACTION_CANCEL);
                super.dispatchTouchEvent(cancel);
                cancel.recycle();
            }
            if (pageDragging) {
                updatePageDrag(deltaX);
                return true;
            }
            return super.dispatchTouchEvent(event);
        }

        if (action == MotionEvent.ACTION_UP && pageDragging) {
            finishPageDrag(deltaX, false);
            return true;
        }

        if (action == MotionEvent.ACTION_CANCEL && pageDragging) {
            finishPageDrag(deltaX, true);
            return true;
        }

        return super.dispatchTouchEvent(event);
    }

    private void beginPageDrag(float deltaX) {
        pageDragging = true;
        configureDragTarget(deltaX);
    }

    private void updatePageDrag(float deltaX) {
        Page desiredTarget = deltaX < 0f ? nextPage(currentPage) : previousPage(currentPage);
        if (desiredTarget != dragTargetPage) {
            if (dragTargetView != null) {
                dragTargetView.setVisibility(View.GONE);
                dragTargetView.setTranslationX(0f);
            }
            configureDragTarget(deltaX);
        }

        View currentView = pageView(currentPage);
        int width = Math.max(1, pageContainer.getWidth());
        if (dragTargetView == null) {
            currentView.setTranslationX(deltaX * 0.18f);
            return;
        }

        currentView.setTranslationX(deltaX);
        float targetStart = deltaX < 0f ? width : -width;
        dragTargetView.setTranslationX(targetStart + deltaX);
    }

    private void configureDragTarget(float deltaX) {
        dragTargetPage = deltaX < 0f ? nextPage(currentPage) : previousPage(currentPage);
        dragTargetView = pageView(dragTargetPage);
        if (dragTargetView == null) {
            return;
        }

        int width = Math.max(1, pageContainer.getWidth());
        dragTargetView.setVisibility(View.VISIBLE);
        dragTargetView.bringToFront();
        pageView(currentPage).bringToFront();
        dragTargetView.setTranslationX(deltaX < 0f ? width : -width);
    }

    private void finishPageDrag(float deltaX, boolean cancelled) {
        final View currentView = pageView(currentPage);
        final View targetView = dragTargetView;
        final Page targetPage = dragTargetPage;
        int width = Math.max(1, pageContainer.getWidth());
        float completionThreshold = Math.min(
                width * 0.22f,
                96f * getResources().getDisplayMetrics().density);
        boolean complete = !cancelled
                && targetView != null
                && Math.abs(deltaX) >= completionThreshold;

        if (complete) {
            float exitX = deltaX < 0f ? -width : width;
            currentView.animate()
                    .translationX(exitX)
                    .setDuration(180L)
                    .start();
            targetView.animate()
                    .translationX(0f)
                    .setDuration(180L)
                    .withEndAction(() -> selectPage(targetPage))
                    .start();
        } else {
            float targetRest = deltaX < 0f ? width : -width;
            currentView.animate()
                    .translationX(0f)
                    .setDuration(160L)
                    .start();
            if (targetView != null) {
                targetView.animate()
                        .translationX(targetRest)
                        .setDuration(160L)
                        .withEndAction(() -> {
                            targetView.setVisibility(View.GONE);
                            targetView.setTranslationX(0f);
                        })
                        .start();
            }
        }

        pageDragging = false;
        dragTargetPage = null;
        dragTargetView = null;
    }

    private Page nextPage(Page page) {
        if (page == Page.HOME) {
            return Page.QUICK_SETTINGS;
        }
        if (page == Page.QUICK_SETTINGS) {
            return Page.FILE_TRANSFER;
        }
        return null;
    }

    private Page previousPage(Page page) {
        if (page == Page.FILE_TRANSFER) {
            return Page.QUICK_SETTINGS;
        }
        if (page == Page.QUICK_SETTINGS) {
            return Page.HOME;
        }
        return null;
    }

    private View pageView(Page page) {
        if (page == null) {
            return null;
        }
        if (page == Page.HOME) {
            return pageHome;
        }
        if (page == Page.QUICK_SETTINGS) {
            return pageQuickSettings;
        }
        return pageFileTransfer;
    }

    private void resetPageTransforms() {
        View[] pages = {pageHome, pageQuickSettings, pageFileTransfer};
        for (View page : pages) {
            page.animate().cancel();
            page.setTranslationX(0f);
        }
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
        if (PhoneTransferService.start(this, uris)) {
            Toast.makeText(this, R.string.transfer_queued,
                    Toast.LENGTH_SHORT).show();
        } else {
            Toast.makeText(this, R.string.transfer_queue_failed,
                    Toast.LENGTH_LONG).show();
        }
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

    private enum Page {
        HOME,
        QUICK_SETTINGS,
        FILE_TRANSFER
    }
}

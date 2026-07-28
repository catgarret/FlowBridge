using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private void CaptureCoordinator_ExitHotkeyPressed(object sender, EventArgs e) { RunOnUi(ExitApplication); }
        private void AutoHideService_IdleHideRequested(object sender, EventArgs e) { RunOnUi(HideApplicationForIdle); }

        private void FileTransferCoordinator_ProgressChanged(
            object sender,
            FileTransferProgressEventArgs e)
        {
            if (e == null || e.Progress == null) return;
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed) return;
                if (e.Progress.Sequence <= _lastFileTransferProgressSequence)
                    return;
                _lastFileTransferProgressSequence = e.Progress.Sequence;
                if (_fileTransferStatusForm == null ||
                    _fileTransferStatusForm.IsDisposed)
                {
                    _fileTransferStatusForm = new FileTransferStatusForm(
                        _fileTransferCoordinator,
                        _settings.Theme);
                }
                _fileTransferStatusForm.UpdateProgress(e.Progress);
            });
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _deviceMonitor.StateChanged -= DeviceMonitor_StateChanged;
            _deviceMonitor.DeviceConnected -= DeviceMonitor_DeviceConnected;
            _deviceMonitor.DeviceDisconnected -= DeviceMonitor_DeviceDisconnected;
            _scrcpyService.RunningChanged -= ScrcpyService_RunningChanged;
            _singleWindowService.RunningChanged -=
                SingleWindowService_RunningChanged;
            _captureCoordinator.ExitHotkeyPressed -=
                CaptureCoordinator_ExitHotkeyPressed;
            _autoHideService.IdleHideRequested -=
                AutoHideService_IdleHideRequested;
            _fileTransferCoordinator.ProgressChanged -=
                FileTransferCoordinator_ProgressChanged;
            _phoneScreenWakeTimer.Tick -= PhoneScreenWakeTimer_Tick;

            _orchestrator.RequestShutdown();
            _singleWindowService.RequestShutdown();
            _screenOffService.RequestShutdown();
            _fileTransferCoordinator.RequestShutdown();
            var cleanupStillRunning = _exitCleanupTask != null &&
                !_exitCleanupTask.IsCompleted;
            if (!cleanupStillRunning)
                TryCleanup("device monitor", _deviceMonitor.Dispose);
            TryCleanup(
                "mini control bar",
                _miniControlBarManager.Dispose);
            TryCleanup("capture coordinator", _captureCoordinator.Dispose);
            TryCleanup("automatic hide", _autoHideService.Dispose);
            TryCleanup("key mapping", _keyMappingService.Dispose);
            TryCleanup("phone screen timer", _phoneScreenWakeTimer.Dispose);
            TryCleanup("app profile menu", _appProfileMenu.Dispose);
            if (_fileTransferStatusForm != null)
                TryCleanup(
                    "file transfer window",
                    _fileTransferStatusForm.Dispose);
            if (!cleanupStillRunning)
            {
                TryCleanup("screen-off service", _screenOffService.Dispose);
                TryCleanup(
                    "single-window service",
                    _singleWindowService.Dispose);
                TryCleanup("scrcpy service", _scrcpyService.Dispose);
                TryCleanup(
                    "file transfer service",
                    _fileTransferCoordinator.Dispose);
                TryCleanup("DeX finalization", delegate
                {
                    _orchestrator.ShutdownAsync()
                        .GetAwaiter()
                        .GetResult();
                });
            }
            TryCleanup("tray service", _trayService.Dispose);
            if (_logForm != null)
                TryCleanup("log window", _logForm.Dispose);
            if (_settingsForm != null)
                TryCleanup("settings window", _settingsForm.Dispose);
            if (_environmentCheckForm != null)
                TryCleanup(
                    "environment check window",
                    _environmentCheckForm.Dispose);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowExit) return;
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (!_exitInProgress) HideToTray();
                return;
            }

            _exitInProgress = true;
            _allowExit = true;
            _orchestrator.RequestShutdown();
            _singleWindowService.RequestShutdown();
            _screenOffService.RequestShutdown();
            _fileTransferCoordinator.RequestShutdown();
            TryCleanup(
                "mini control bar",
                _miniControlBarManager.Dispose);
            TryCleanup("capture coordinator", _captureCoordinator.Stop);
            TryCleanup("key mapping", _keyMappingService.Stop);
            BeginPhoneScreenWakeSuppression();
            _phoneScreenWakeTimer.Stop();

            if (_exitCleanupTask == null)
            {
                var wakeSerials = CaptureWakeSerials();
                _exitCleanupTask = RunExitCleanupAsync(wakeSerials);
            }
            try
            {
                if (!_exitCleanupTask.Wait(5000))
                {
                    e.Cancel = true;
                    _allowExit = false;
                    _logService.Warning(LocalizationService.Get(
                        "Log.Main.CleanupDeferredForShutdown"));
                    ScheduleCloseAfterExitCleanup();
                }
            }
            catch (AggregateException ex)
            {
                _logService.Error(
                    LocalizationService.Format(
                        "Log.Main.CleanupFailed",
                        "system shutdown"),
                    ex.GetBaseException());
            }
        }

        private void ScheduleCloseAfterExitCleanup()
        {
            if (_forcedCloseContinuationScheduled ||
                _exitCleanupTask == null)
            {
                return;
            }
            _forcedCloseContinuationScheduled = true;
            _exitCleanupTask.ContinueWith(delegate
            {
                RunOnUi(delegate
                {
                    _allowExit = true;
                    if (!IsDisposed) Close();
                });
            }, TaskScheduler.Default);
        }

        private void TryCleanup(string operation, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Format(
                        "Log.Main.CleanupFailed",
                        operation),
                    ex);
            }
        }

        private void UpdateRunningState()
        {
            var running = IsSelectedModeRunning();
            _scrcpyStatusValue.Text = running
                ? LocalizationService.Get("Status.Running")
                : LocalizationService.Get("Status.Stopped");
            _dexStatusValue.Text = running
                ? LocalizationService.Get("Status.Running")
                : LocalizationService.Get("Status.Idle");
            _startButton.Enabled = !running;
            _stopButton.Enabled = running;
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            UpdateApplySettingsLink();
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (running && _selectedMode == 0)
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Get("Main.DexRunning"),
                    LocalizationService.Get("Main.DexRunningDetail"));
            else if (running)
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Format(
                        "Main.SingleRunning",
                        _selectedMode),
                    GetSingleWindowStatusDetail(_selectedMode));
            else if (_selectedMode > 0)
                UpdateSingleWindowIndicator(_selectedMode);
            else
                UpdateIndicatorForDevice(_lastDeviceState);
        }

        private void SetOperationState(bool operationRunning, string status)
        {
            var running = IsSelectedModeRunning();
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            _startButton.Enabled = !operationRunning && !running;
            _stopButton.Enabled = !operationRunning && running;
            _applySettingsLink.Enabled = !operationRunning;
            _dexStatusValue.Text = status;
        }

        private void UpdateIndicatorForDevice(DeviceState state)
        {
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Device)
            {
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Get("Main.PhoneConnected"),
                    LocalizationService.Get("Main.WaitingDex"));
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Unauthorized)
            {
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.AuthorizationRequired"),
                    LocalizationService.Get("Main.AuthorizationDetail"));
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Offline)
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Main.DeviceOffline"),
                    LocalizationService.Get("Main.DeviceOfflineDetail"));
                return;
            }
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.Waiting"),
                LocalizationService.Get("Main.WaitingPhone"));
        }

        private void SetConnectionIndicator(Color color, string status, string detail)
        {
            _indicatorDot.StatusColor = color;
            var argb = color.ToArgb();
            _indicatorDot.Complete =
                argb == Color.ForestGreen.ToArgb() ||
                argb == Color.Green.ToArgb() ||
                argb == Color.DarkGreen.ToArgb();
            _indicatorStatus.Text = status;
            var device = _lastDeviceState;
            _indicatorDetail.Text =
                _settings.Features.ShowConnectedDeviceInfo &&
                device != null &&
                device.Status == AdbDeviceStatus.Device &&
                !string.IsNullOrWhiteSpace(_deviceInfoLabel.Text)
                    ? detail + "  ·  " + _deviceInfoLabel.Text
                    : detail;
        }

        private void ShowError(string message, Exception exception)
        {
            _logService.Error(message, exception);
            _connectionError = message + ": " + exception.Message;
            SetConnectionIndicator(
                Color.Firebrick,
                LocalizationService.Get("Status.Error"),
                _connectionError);
            MessageBox.Show(
                this,
                message + Environment.NewLine +
                    Environment.NewLine + exception.Message,
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

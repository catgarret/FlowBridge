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
            var coordinator = sender as FileTransferCoordinator;
            if (coordinator == null) return;
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed) return;
                if (!ReferenceEquals(
                        _fileTransferStatusSource,
                        coordinator))
                {
                    if (_fileTransferStatusForm != null)
                        _fileTransferStatusForm.Dispose();
                    _fileTransferStatusForm = null;
                    _fileTransferStatusSource = coordinator;
                    _lastFileTransferProgressSequence = 0;
                }
                if (e.Progress.Sequence <= _lastFileTransferProgressSequence)
                    return;
                _lastFileTransferProgressSequence = e.Progress.Sequence;
                if (_fileTransferStatusForm == null ||
                    _fileTransferStatusForm.IsDisposed)
                {
                    _fileTransferStatusForm = new FileTransferStatusForm(
                        coordinator,
                        _settings.Theme);
                }
                var shouldShow = !_fileTransferStatusForm.Visible;
                _fileTransferStatusForm.UpdateProgress(e.Progress);
                if (shouldShow)
                {
                    var target = PositionTransferStatusWindow(
                        _fileTransferStatusForm,
                        _phoneTransferStatusForm,
                        coordinator.GetWindowHandle(
                            e.Progress.SessionId));
                    ShowTransferStatusWindow(
                        _fileTransferStatusForm,
                        target);
                }
            });
        }

        private void PhoneTransferReceiver_ProgressChanged(
            object sender,
            PhoneTransferProgressEventArgs e)
        {
            if (e == null || e.Progress == null) return;
            var receiver = sender as PhoneTransferReceiver;
            if (receiver == null) return;
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed) return;
                if (!ReferenceEquals(_phoneTransferStatusSource, receiver))
                {
                    if (_phoneTransferStatusForm != null)
                        _phoneTransferStatusForm.Dispose();
                    _phoneTransferStatusForm = null;
                    _phoneTransferStatusSource = receiver;
                    _lastPhoneTransferProgressSequence = 0;
                }
                if (e.Progress.Sequence <=
                    _lastPhoneTransferProgressSequence)
                {
                    return;
                }
                _lastPhoneTransferProgressSequence = e.Progress.Sequence;
                if (_phoneTransferStatusForm == null ||
                    _phoneTransferStatusForm.IsDisposed)
                {
                    _phoneTransferStatusForm =
                        new PhoneTransferStatusForm(_settings.Theme);
                }
                var shouldShow = !_phoneTransferStatusForm.Visible;
                _phoneTransferStatusForm.UpdateProgress(e.Progress);
                if (shouldShow)
                {
                    var target = PositionTransferStatusWindow(
                        _phoneTransferStatusForm,
                        _fileTransferStatusForm,
                        IntPtr.Zero);
                    ShowTransferStatusWindow(
                        _phoneTransferStatusForm,
                        target);
                }
            });
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _deviceMonitor.StateChanged -= DeviceMonitor_StateChanged;
            _physicalDeviceRegistry.SnapshotChanged -=
                PhysicalDeviceRegistry_SnapshotChanged;
            _scrcpyService.RunningChanged -= ScrcpyService_RunningChanged;
            _singleWindowService.RunningChanged -=
                SingleWindowService_RunningChanged;
            _captureCoordinator.ExitHotkeyPressed -=
                CaptureCoordinator_ExitHotkeyPressed;
            _autoHideService.IdleHideRequested -=
                AutoHideService_IdleHideRequested;
            _fileTransferCoordinator.ProgressChanged -=
                FileTransferCoordinator_ProgressChanged;
            _phoneTransferReceiver.ProgressChanged -=
                PhoneTransferReceiver_ProgressChanged;
            _phoneScreenWakeTimer.Tick -= PhoneScreenWakeTimer_Tick;

            RequestAllRuntimeShutdown(_systemShutdownInProgress);
            if (_systemShutdownInProgress)
            {
                // Windows owns process termination from this point. Do not
                // dispose runtime services here: several normal Dispose paths
                // stop child processes or send additional ADB commands.
                TryCleanup("phone screen timer", _phoneScreenWakeTimer.Dispose);
                TryCleanup("device tooltips", _deviceTabToolTip.Dispose);
                TryCleanup("tray service", _trayService.Dispose);
                return;
            }
            var cleanupStillRunning = _exitCleanupTask != null &&
                !_exitCleanupTask.IsCompleted;
            if (!cleanupStillRunning)
                TryCleanup("device monitor", _deviceMonitor.Dispose);
            TryCleanup("phone screen timer", _phoneScreenWakeTimer.Dispose);
            TryCleanup("device tooltips", _deviceTabToolTip.Dispose);
            TryCleanup("app profile menu", _appProfileMenu.Dispose);
            if (_fileTransferStatusForm != null)
                TryCleanup(
                    "file transfer window",
                    _fileTransferStatusForm.Dispose);
            if (_phoneTransferStatusForm != null)
                TryCleanup(
                    "phone transfer window",
                    _phoneTransferStatusForm.Dispose);
            if (!cleanupStillRunning)
            {
                DisposeAllDeviceContexts();
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
            if (e.CloseReason == CloseReason.WindowsShutDown ||
                _systemShutdownInProgress)
            {
                BeginSystemShutdown();
                e.Cancel = false;
                return;
            }
            if (_allowExit) return;
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (!_exitInProgress) HideToTray();
                return;
            }

            _exitInProgress = true;
            _allowExit = true;
            RequestAllRuntimeShutdown();
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
                var cleanupSerial = GetSelectedDeviceSerial();
                _exitCleanupTask = RunExitCleanupAsync(
                    wakeSerials,
                    cleanupSerial);
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

        private void BeginSystemShutdown()
        {
            BeginSystemShutdown(false);
        }

        private void SimulateWindowsShutdown()
        {
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed) return;
                BeginSystemShutdown(true);
                Close();
            });
        }

        private void BeginSystemShutdown(bool simulation)
        {
            if (_systemShutdownInProgress) return;

            _systemShutdownInProgress = true;
            _systemShutdownSimulationInProgress = simulation;
            _exitInProgress = true;
            _allowExit = true;
            _logService.Info(LocalizationService.Get(simulation
                ? "Log.Main.SystemShutdownSimulationStarted"
                : "Log.Main.SystemShutdownDetected"));

            var cleanupTargets = CaptureWindowsShutdownCleanupTargets();
            BeginPhoneScreenWakeSuppression();
            _phoneScreenWakeTimer.Stop();

            // Stop every producer first, without stopping child processes or
            // sending companion detach commands. Only the essential overlay
            // and phone power restoration commands are allowed below; Windows
            // owns process termination after the short bounded wait.
            RequestAllRuntimeShutdown(true);
            TryCleanup(
                "device monitor shutdown request",
                _deviceMonitor.RequestShutdown);
            StopAllInteractiveContextsForWindowsShutdown();

            var cleanupTasks = new List<Task>();
            foreach (var target in cleanupTargets)
            {
                var capturedTarget = target;
                cleanupTasks.Add(Task.Run(delegate
                {
                    try
                    {
                        var result = _adbService.CleanupForWindowsShutdown(
                            capturedTarget.Serial,
                            capturedTarget.RemoveOverlay,
                            capturedTarget.RestoreStayAwake,
                            capturedTarget.OriginalStayAwakeValue);
                        if (!result.IsSuccess)
                        {
                            _logService.Warning(DeviceLogFormatter.ForSerial(
                                capturedTarget.Serial,
                                LocalizationService.Format(
                                    "Log.Main.SystemShutdownDeviceCleanupFailed",
                                    result.StandardError)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Error(
                            DeviceLogFormatter.ForSerial(
                                capturedTarget.Serial,
                                LocalizationService.Get(
                                    "Log.Main.SystemShutdownDeviceCleanupException")),
                            ex);
                    }
                }));
            }
            // The essential device commands are already in flight. Persist
            // only a dirty main-window mode while they run in parallel.
            TrySavePendingRunSettingsForSystemShutdown();
            try
            {
                if (cleanupTasks.Count > 0 &&
                    !Task.WaitAll(cleanupTasks.ToArray(), 1500))
                {
                    _logService.Warning(LocalizationService.Get(
                        "Log.Main.SystemShutdownCleanupTimedOut"));
                }
            }
            catch (AggregateException ex)
            {
                _logService.Error(
                    LocalizationService.Format(
                        "Log.Main.CleanupFailed",
                        "Windows shutdown"),
                    ex.GetBaseException());
            }
        }

        private void TrySavePendingRunSettingsForSystemShutdown()
        {
            if (_selectedMode < 0 ||
                _selectedMode >= _modeSettingsDirty.Length ||
                !_modeSettingsDirty[_selectedMode])
            {
                return;
            }
            try
            {
                ApplyRunSettings(false);
                _modeSettingsDirty[_selectedMode] = false;
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Main.ModeSwitchSaveFailed",
                    ex.Message));
            }
        }

        private IList<WindowsShutdownCleanupTarget>
            CaptureWindowsShutdownCleanupTargets()
        {
            var targets = new List<WindowsShutdownCleanupTarget>();
            var added = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            foreach (var context in GetAllDeviceContexts())
            {
                var dex = context.Runtime.Scrcpy.GetSessionSnapshot();
                string original = null;
                var serial = GetWindowsShutdownSerial(context, dex);
                // Normal DX Manager exit removes overlay state regardless of
                // who created it. Keep the same recovery guarantee here for
                // every currently reachable phone, including a display left
                // behind after its scrcpy window already closed.
                var removeOverlay = !string.IsNullOrWhiteSpace(serial);
                var restoreStayAwake =
                    _settings.Features.DisableStayAwakeOnStop &&
                    TryGetStayAwakeOriginalForContext(
                        context,
                        serial,
                        out original);
                if (!removeOverlay && !restoreStayAwake) continue;
                if (string.IsNullOrWhiteSpace(serial) || !added.Add(serial))
                    continue;

                targets.Add(new WindowsShutdownCleanupTarget
                {
                    Serial = serial,
                    RemoveOverlay = removeOverlay,
                    RestoreStayAwake = restoreStayAwake,
                    OriginalStayAwakeValue = restoreStayAwake &&
                        original != MissingStayAwakeValue
                        ? original
                        : null
                });
            }
            return targets;
        }

        private string GetWindowsShutdownSerial(
            DeviceUiContext context,
            ScrcpySessionSnapshot dex)
        {
            if (context == null || context.Runtime == null)
                return string.Empty;
            if (dex.IsRunning && !string.IsNullOrWhiteSpace(dex.Serial))
                return dex.Serial;
            foreach (var serial in context.Runtime.SingleWindows
                .GetRunningSerials())
            {
                if (!string.IsNullOrWhiteSpace(serial)) return serial;
            }
            return GetContextSerial(context);
        }

        private bool TryGetStayAwakeOriginalForContext(
            DeviceUiContext context,
            string preferredSerial,
            out string original)
        {
            if (TryGetStayAwakeOriginalValue(preferredSerial, out original))
                return true;
            if (context != null && context.Device != null &&
                context.Device.Transports != null)
            {
                foreach (var transport in context.Device.Transports)
                {
                    if (transport != null &&
                        TryGetStayAwakeOriginalValue(
                            transport.Serial,
                            out original))
                    {
                        return true;
                    }
                }
            }
            original = null;
            return false;
        }

        private sealed class WindowsShutdownCleanupTarget
        {
            public string Serial;
            public bool RemoveOverlay;
            public bool RestoreStayAwake;
            public string OriginalStayAwakeValue;
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

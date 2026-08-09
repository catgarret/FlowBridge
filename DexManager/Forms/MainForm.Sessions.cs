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
        private async Task InitializeAdbAndMonitorAsync()
        {
            _adbStatusValue.Text =
                LocalizationService.Get("Status.Initializing");
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.Waiting"),
                LocalizationService.Get("Main.PreparingAdb"));
            try
            {
                await Task.Run(delegate
                {
                    _adbService.LogStartupDiagnostics();
                    _adbService.StartServer();
                    if (_wirelessAdbService.IsWirelessMode)
                    {
                        _wirelessAdbService.TryReconnect(true);
                        return;
                    }
                    var devices = _adbService.GetDevices();
                    _wirelessAdbService.SelectPreferredDevice(devices);
                    if (_settings.Features.ScrcpyWakeUpMode == ScrcpyWakeUpMode.AlwaysOnStartup)
                    {
                        var target = GetSelectedDeviceSerial();
                        _adbService.WakeUp(
                            target,
                            serial => _scrcpyService.RunWakeUp(
                                serial,
                                _settings.Timing.AdbWakeUpDelayMs));
                    }
                    else
                    {
                        var target = GetSelectedDeviceSerial();
                        if (_settings.Features.ScrcpyWakeUpMode == ScrcpyWakeUpMode.OnAdbFailure &&
                            (string.IsNullOrWhiteSpace(target) ||
                             !_adbService.IsAuthorizedDeviceConnected(target)))
                        {
                            _adbService.WakeUp(
                                target,
                                serial => _scrcpyService.RunWakeUp(
                                    serial,
                                    _settings.Timing.AdbWakeUpDelayMs));
                        }
                    }
                });
                _adbStatusValue.Text =
                    LocalizationService.Get("Status.Ready");
                _connectionError = null;
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.Waiting"),
                    LocalizationService.Get("Main.WaitingPhone"));
            }
            catch (Exception ex)
            {
                _adbStatusValue.Text =
                    LocalizationService.Get("Status.Error");
                _logService.Error(
                    LocalizationService.Get("Log.Main.AdbInitFailed"),
                    ex);
                _connectionError = LocalizationService.Format(
                    "Error.AdbInit",
                    ex.Message);
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
            }
            finally
            {
                if (!_exitInProgress && !IsDisposed)
                    _deviceMonitor.Start();
            }
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            if (_selectedMode == 0)
                await StartDexAsync();
            else
                await StartSingleWindowAsync(_selectedMode);
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            if (_selectedMode == 0)
                await StopDexAsync();
            else
                await StopSingleWindowAsync(_selectedMode);
        }

        private async Task StartDexAsync()
        {
            if (_exitInProgress || _orchestrator.IsShutdownRequested) return;
            try
            {
                if (_selectedMode == 0) ApplyRunSettings(false);
            }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Get(
                        "Error.ApplyLaunchSettings"),
                    ex);
                return;
            }

            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Starting"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.DexStarting"),
                LocalizationService.Get("Main.DexPreparing"));
            try
            {
                var serial = GetSelectedDeviceSerial();
                if (string.IsNullOrWhiteSpace(serial))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.NoAuthorizedDevice"));
                }
                if (!await WaitForDeviceStartDelayAsync(serial)) return;
                var scrcpySettings =
                    GetSelectedDeviceRunSettings().Scrcpy;
                if (scrcpySettings.TurnScreenOff)
                    RememberManagedSerial(serial);
                await _orchestrator.StartAsync(serial);
                if (_exitInProgress || !_orchestrator.IsRunning) return;
                RememberStartedApp(
                    scrcpySettings.StartAppPackage,
                    scrcpySettings.StartAppName);
                _modeSettingsDirty[0] = false;
            }
            catch (Exception ex)
            {
                if (!_exitInProgress)
                {
                    ShowError(
                        LocalizationService.Get("Error.StartDex"),
                        ex);
                }
            }
            finally
            {
                UpdateRunningState();
                UpdatePhoneScreenWakeSchedule();
            }
        }

        private async Task StopDexAsync(bool suppressUserError = false)
        {
            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Stopping"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.DexStopping"),
                LocalizationService.Get("Main.DexCleaning"));
            try { await _orchestrator.StopAsync(); }
            catch (Exception ex)
            {
                if (!suppressUserError)
                {
                    ShowError(
                        LocalizationService.Get("Error.StopDex"),
                        ex);
                }
            }
            finally { UpdateRunningState(); }
        }

        private async Task StartSingleWindowAsync(int slot)
        {
            if (_exitInProgress) return;
            try { ApplyRunSettings(false); }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Get(
                        "Error.ApplySingleSettings"),
                    ex);
                return;
            }

            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Starting"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Format(
                    "Main.SingleStarting",
                    slot),
                LocalizationService.Get("Main.SinglePreparing"));
            try
            {
                var settings = GetSingleWindowSettings(slot);
                var serial = GetSelectedDeviceSerial();
                if (string.IsNullOrWhiteSpace(serial))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.NoAuthorizedDevice"));
                }
                if (!await WaitForDeviceStartDelayAsync(serial)) return;
                if (settings.TurnScreenOff)
                    RememberManagedSerial(serial);
                await Task.Run(delegate
                {
                    _singleWindowService.Start(slot, settings, serial);
                });
                if (_exitInProgress ||
                    !_singleWindowService.IsRunning(slot)) return;
                RememberStartedApp(
                    settings.StartAppPackage,
                    settings.StartAppName);
                _modeSettingsDirty[slot] = false;
            }
            catch (Exception ex)
            {
                if (!_exitInProgress)
                {
                    ShowError(
                        LocalizationService.Format(
                            "Error.StartSingle",
                            slot),
                        ex);
                }
            }
            finally
            {
                UpdateRunningState();
                UpdatePhoneScreenWakeSchedule();
            }
        }

        private async Task StopSingleWindowAsync(int slot)
        {
            _connectionError = null;
            SetOperationState(
                true,
                LocalizationService.Get("Status.Stopping"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Format(
                    "Main.SingleStopping",
                    slot),
                LocalizationService.Get("Main.SingleCleaning"));
            try
            {
                await Task.Run(delegate
                {
                    _singleWindowService.Stop(slot);
                });
            }
            catch (Exception ex)
            {
                ShowError(
                    LocalizationService.Format(
                        "Error.StopSingle",
                        slot),
                    ex);
            }
            finally
            {
                UpdateRunningState();
            }
        }

        private void DeviceMonitor_StateChanged(object sender, DeviceStateChangedEventArgs e)
        {
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed) return;
                if (!string.IsNullOrWhiteSpace(_selectedDeviceIdentity))
                    return;
                _lastDeviceState = e.Current;
                _adbStatusValue.Text =
                    e.Current.Status == AdbDeviceStatus.Unknown
                        ? LocalizationService.Get("Status.Idle")
                        : LocalizationService.Get("Status.Responding");
                _deviceStatusValue.Text = GetDeviceStatusText(e.Current);
                _deviceInfoLabel.Text = e.Current.Status == AdbDeviceStatus.Device
                    ? (string.IsNullOrWhiteSpace(e.Current.DisplayName)
                        ? LocalizationService.Format(
                            "Main.ConnectedDeviceFallback",
                            AdbService.IsTcpIpSerial(e.Current.Serial)
                                ? "Wi-Fi"
                                : "USB")
                        : LocalizationService.Format(
                            "Main.ConnectedDevice",
                            e.Current.DisplayName,
                            AdbService.IsTcpIpSerial(e.Current.Serial)
                                ? "Wi-Fi"
                                : "USB"))
                    : LocalizationService.Get("Main.WaitingPhone");
                if (e.Current.Status != AdbDeviceStatus.Device)
                {
                    System.Threading.Interlocked.Increment(
                        ref _screenOffReapplyGeneration);
                }
                if (!IsSelectedModeRunning())
                    UpdateIndicatorForDevice(e.Current);
            });
        }

        private void DeviceMonitor_DeviceConnected(object sender, DeviceStateChangedEventArgs e)
        {
            if (_exitInProgress) return;
            try
            {
                _runtimeSessions.BindServiceInstance(
                    e.Current.Serial,
                    _activeRuntime.InstanceId);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not bind the selected device to its runtime " +
                    "service set.",
                    ex);
                return;
            }
            RecordDeviceConnected(e.Current.Serial);
            ConfigurePhoneTransferReceiver(e.Current.Serial);
            var deviceSwitch = IsDeviceSwitch(e);
            RunOnUi(async delegate
            {
                if (_exitInProgress ||
                    !await WaitForDeviceStartDelayAsync(e.Current.Serial))
                {
                    return;
                }

                MarkSerialReconnected(e.Current.Serial);
                var cleanupReady = await _orchestrator
                    .RetryDeferredCleanupAsync(
                    e.Current.Serial);
                await QueueDeviceStayAwakeUpdate();
                UpdatePhoneScreenWakeSchedule();
                if (cleanupReady &&
                    !deviceSwitch &&
                    _settings.Features.AutoStartDexOnDeviceConnected)
                {
                    await StartDexAsync();
                }
            });
        }

        private void DeviceMonitor_DeviceDisconnected(object sender, DeviceStateChangedEventArgs e)
        {
            if (_exitInProgress) return;
            if (e != null && e.Previous != null)
            {
                _fileTransferCoordinator.CancelSerial(e.Previous.Serial);
                var detachTask = _phoneTransferReceiver.DetachAsync(e.Previous.Serial);
            }
            if (IsDeviceSwitch(e))
            {
                ForgetDeviceConnectionTimestamp(e.Previous.Serial);
                RunOnUi(async delegate
                {
                    var previousStillConnected = await Task.Run(delegate
                    {
                        return _adbService.IsAuthorizedDeviceConnected(
                            e.Previous.Serial);
                    });
                    if (previousStillConnected)
                        MarkSerialAvailable(e.Previous.Serial);
                    else
                        MarkSerialDisconnected(e.Previous.Serial);
                    await HandleDeviceSwitchAsync(e);
                });
                return;
            }
            ForgetDeviceConnection(e.Previous.Serial);
            RunOnUi(async delegate
            {
                MarkSerialDisconnected(e.Previous.Serial);
                System.Threading.Interlocked.Increment(
                    ref _screenOffReapplyGeneration);
                if (_orchestrator.IsRunning)
                    await StopDexAsync(true);
                if (IsAnySingleWindowRunning())
                {
                    try
                    {
                        await Task.Run(
                            (Action)_singleWindowService.StopAll);
                    }
                    catch (Exception ex)
                    {
                        _logService.Error(
                            LocalizationService.Format(
                                "Log.Main.CleanupFailed",
                                "disconnected single-window sessions"),
                            ex);
                    }
                }
            });
        }

        private async void ConfigurePhoneTransferReceiver(string serial)
        {
            if (_exitInProgress ||
                string.IsNullOrWhiteSpace(serial))
            {
                return;
            }
            try
            {
                await _phoneTransferReceiver.AttachAsync(serial);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not prepare phone-to-PC transfer.", ex);
            }
        }

        private static bool IsDeviceSwitch(DeviceStateChangedEventArgs e)
        {
            return e != null &&
                e.Previous != null &&
                e.Current != null &&
                e.Previous.IsConnected &&
                e.Current.IsConnected &&
                !string.Equals(
                    e.Previous.Serial,
                    e.Current.Serial,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void RecordDeviceConnected(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            lock (_deviceConnectionSync)
            {
                _deviceConnectedAtUtc[serial] = DateTime.UtcNow;
                _disconnectedSerials.Remove(serial);
            }
        }

        private void ForgetDeviceConnection(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            lock (_deviceConnectionSync)
            {
                _deviceConnectedAtUtc.Remove(serial);
                _disconnectedSerials.Add(serial);
            }
        }

        private void ForgetDeviceConnectionTimestamp(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            lock (_deviceConnectionSync)
                _deviceConnectedAtUtc.Remove(serial);
        }

        private async Task<bool> WaitForDeviceStartDelayAsync(
            string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            DateTime connectedAt;
            lock (_deviceConnectionSync)
            {
                if (!_deviceConnectedAtUtc.TryGetValue(
                    serial,
                    out connectedAt))
                {
                    connectedAt = DateTime.UtcNow;
                    _deviceConnectedAtUtc[serial] = connectedAt;
                }
            }

            var readyAt = connectedAt.AddMilliseconds(
                Math.Max(_settings.Timing.ConnectedStartDelayMs, 0));
            var remaining = readyAt - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Main.DeviceStartDelay",
                    serial,
                    Math.Max(1, (int)Math.Ceiling(
                        remaining.TotalMilliseconds))));
                await Task.Delay(Math.Max(
                    1,
                    (int)Math.Ceiling(remaining.TotalMilliseconds)));
            }

            if (_exitInProgress || IsDisposed ||
                IsSerialMarkedDisconnected(serial) ||
                !string.Equals(
                    GetSelectedDeviceSerial(),
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            var current = _deviceMonitor.CurrentState;
            if (current != null &&
                current.IsConnected &&
                current.Status == AdbDeviceStatus.Device &&
                string.Equals(
                    current.Serial,
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var authorized = await Task.Run(delegate
            {
                return _adbService.IsAuthorizedDeviceConnected(serial);
            });
            return authorized &&
                !_exitInProgress &&
                !IsSerialMarkedDisconnected(serial) &&
                string.Equals(
                    GetSelectedDeviceSerial(),
                    serial,
                    StringComparison.OrdinalIgnoreCase);
        }

        private string GetSelectedDeviceSerial()
        {
            if (_selectedDeviceContext != null &&
                !string.IsNullOrWhiteSpace(
                    _selectedDeviceContext.Identity))
            {
                return _selectedDeviceContext.Device != null &&
                    _selectedDeviceContext.Device.IsConnected
                    ? GetContextSerial(_selectedDeviceContext)
                    : string.Empty;
            }

            var selected = GetContextSerial(_selectedDeviceContext);
            if (!string.IsNullOrWhiteSpace(selected)) return selected;
            var current = _deviceMonitor.CurrentState;
            if (current != null &&
                current.IsConnected &&
                current.Status == AdbDeviceStatus.Device &&
                !string.IsNullOrWhiteSpace(current.Serial))
            {
                return current.Serial;
            }
            return _wirelessAdbService.SelectedSerial;
        }

        private void MarkSerialDisconnected(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            lock (_deviceConnectionSync)
                _disconnectedSerials.Add(serial);

            var shouldWake = _managedSerialHistory.Remove(serial) ||
                IsScreenOffRequestedForSerial(serial);
            if (shouldWake) _deferredPhoneWakeSerials.Add(serial);
            _phoneScreenWakeInProgress.Remove(serial);
            UpdatePhoneScreenWakeSchedule();
        }

        private void MarkSerialReconnected(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            lock (_deviceConnectionSync)
                _disconnectedSerials.Remove(serial);
            if (_deferredPhoneWakeSerials.Remove(serial))
                _managedSerialHistory.Add(serial);
        }

        private void MarkSerialAvailable(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            lock (_deviceConnectionSync)
                _disconnectedSerials.Remove(serial);
        }

        private bool IsSerialMarkedDisconnected(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return true;
            lock (_deviceConnectionSync)
                return _disconnectedSerials.Contains(serial);
        }

        private async Task HandleDeviceSwitchAsync(
            DeviceStateChangedEventArgs e)
        {
            if (_exitInProgress) return;
            if (_orchestrator.IsRunning)
                await StopDexAsync(true);

            if (IsAnySingleWindowRunning())
            {
                try
                {
                    await Task.Run(
                        (Action)_singleWindowService.StopAll);
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Main.DeviceSwitchCleanupFailed"),
                        ex);
                    return;
                }
            }

            if (_exitInProgress || IsAnyScrcpyRunning()) return;
            if (!await WaitForDeviceStartDelayAsync(e.Current.Serial))
                return;
            var current = _deviceMonitor.CurrentState;
            if (!current.IsConnected ||
                current.Status != AdbDeviceStatus.Device ||
                !string.Equals(
                    current.Serial,
                    e.Current.Serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_settings.Features.AutoStartDexOnDeviceConnected)
                await StartDexAsync();
        }
    }
}

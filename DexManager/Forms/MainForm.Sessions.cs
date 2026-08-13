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

    }
}

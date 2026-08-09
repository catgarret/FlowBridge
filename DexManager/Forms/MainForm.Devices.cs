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
        private sealed class DeviceUiContext
        {
            public string Identity;
            public PhysicalDeviceInfo Device;
            public DeviceRuntimeServiceSet Runtime;
            public CaptureCoordinator Capture;
            public AutoHideService AutoHide;
            public EnvironmentCheckService EnvironmentCheck;
            public KeyMappingService KeyMapping;
            public MiniControlBarManager MiniBar;
            public bool WasConnected;
            public string ActiveSerial = string.Empty;
            public int SelectedMode;
            public bool[] ModeSettingsDirty = new bool[4];
        }

        private DeviceUiContext CreateInitialDeviceContext()
        {
            return new DeviceUiContext
            {
                Runtime = _activeRuntime,
                Capture = _captureCoordinator,
                AutoHide = _autoHideService,
                EnvironmentCheck = _environmentCheckService,
                KeyMapping = _keyMappingService,
                MiniBar = _miniControlBarManager,
                SelectedMode = _selectedMode,
                ModeSettingsDirty = _modeSettingsDirty
            };
        }

        private DeviceUiContext CreateDeviceContext(
            DeviceRuntimeServiceSet runtime)
        {
            var hotkeys = new HotkeyService(_logService, _settings);
            var capture = new CaptureCoordinator(
                hotkeys,
                _captureService,
                runtime.Scrcpy,
                runtime.SingleWindows,
                _settings,
                _logService);
            var context = new DeviceUiContext
            {
                Runtime = runtime,
                Capture = capture,
                AutoHide = new AutoHideService(
                    runtime.Scrcpy,
                    runtime.SingleWindows,
                    _logService,
                    _settings.Timing.AutoHideIdleSeconds),
                KeyMapping = new KeyMappingService(
                    runtime.Scrcpy,
                    runtime.SingleWindows,
                    _adbService,
                    _settings,
                    _settings.KeyMappings,
                    _logService)
            };
            context.EnvironmentCheck = new EnvironmentCheckService(
                _adbService,
                runtime.Scrcpy,
                _pathService,
                _logService,
                _settingsService,
                _settings,
                delegate { return GetContextSerial(context); });
            context.MiniBar = new MiniControlBarManager(
                _settings,
                runtime.Scrcpy,
                runtime.SingleWindows,
                capture,
                ShowMainWindow,
                _logService);
            return context;
        }

        private void PhysicalDeviceRegistry_SnapshotChanged(
            object sender,
            DeviceRegistrySnapshotChangedEventArgs e)
        {
            if (e == null || e.Current == null) return;
            RunOnUi(delegate { ReconcileDeviceTabs(e.Current); });
        }

        private void ReconcileDeviceTabs(DeviceRegistrySnapshot snapshot)
        {
            if (_exitInProgress || IsDisposed) return;

            foreach (var context in _deviceContexts.Values)
                context.Device = null;

            foreach (var device in snapshot.Devices)
            {
                if (device == null ||
                    string.IsNullOrWhiteSpace(device.Identity)) continue;

                var context = EnsureDeviceContext(device);
                context.Device = device.Clone();
                ConfigureContextConnection(context);
            }

            foreach (var context in _deviceContexts.Values)
            {
                if (context.Device == null && context.WasConnected)
                    HandleContextDisconnected(context);
            }

            if (_selectedDeviceContext == null ||
                string.IsNullOrWhiteSpace(_selectedDeviceContext.Identity))
            {
                foreach (var device in snapshot.Devices)
                {
                    DeviceUiContext context;
                    if (device != null && device.IsConnected &&
                        _deviceContexts.TryGetValue(
                            device.Identity,
                            out context))
                    {
                        SelectDeviceContext(context);
                        break;
                    }
                }
            }

            RebuildDeviceTabs();
            RefreshSelectedDeviceState();
        }

        private DeviceUiContext EnsureDeviceContext(PhysicalDeviceInfo device)
        {
            DeviceUiContext context;
            lock (_deviceContextsSync)
            {
                if (_deviceContexts.TryGetValue(
                        device.Identity,
                        out context))
                {
                    return context;
                }
            }

            var session = _runtimeSessions.Current.FindByIdentity(
                device.Identity);
            var runtime = session == null
                ? null
                : _runtimeServiceFactory.Find(session.ServiceInstanceId);

            if (runtime == null &&
                _initialDeviceContext != null &&
                string.IsNullOrWhiteSpace(_initialDeviceContext.Identity))
            {
                context = _initialDeviceContext;
                runtime = context.Runtime;
            }
            else
            {
                runtime = runtime ?? _runtimeServiceFactory.Create();
                context = CreateDeviceContext(runtime);
                AttachContextEvents(context);
                context.MiniBar.Start();
            }

            context.Identity = device.Identity;
            context.Device = device.Clone();
            context.Runtime.Dex.DeviceIdentity = device.Identity;
            lock (_deviceContextsSync)
                _deviceContexts.Add(device.Identity, context);
            if (ReferenceEquals(context, _selectedDeviceContext))
                _selectedDeviceIdentity = device.Identity;

            var transport = device.SelectPreferredTransport(string.Empty);
            if (transport != null &&
                !string.IsNullOrWhiteSpace(transport.Serial))
            {
                _runtimeSessions.BindServiceInstance(
                    transport.Serial,
                    runtime.InstanceId);
            }
            _logService.Info(LocalizationService.Format(
                "Log.DeviceContext.Registered",
                GetContextDisplayName(context),
                transport == null ? device.Identity : transport.Serial,
                GetTransportText(transport)));
            return context;
        }

        private void AttachContextEvents(DeviceUiContext context)
        {
            context.Runtime.Scrcpy.RunningChanged +=
                ScrcpyService_RunningChanged;
            context.Runtime.SingleWindows.RunningChanged +=
                SingleWindowService_RunningChanged;
            context.Capture.ExitHotkeyPressed +=
                CaptureCoordinator_ExitHotkeyPressed;
            context.AutoHide.IdleHideRequested +=
                AutoHideService_IdleHideRequested;
            context.Runtime.FileTransfers.ProgressChanged +=
                FileTransferCoordinator_ProgressChanged;
            context.Runtime.PhoneTransfers.ProgressChanged +=
                PhoneTransferReceiver_ProgressChanged;
        }

        private void ConfigureContextConnection(DeviceUiContext context)
        {
            if (context == null || context.Device == null ||
                !context.Device.IsConnected)
            {
                if (context != null && context.WasConnected)
                    HandleContextDisconnected(context);
                return;
            }

            var serial = GetContextSerial(context);
            if (string.IsNullOrWhiteSpace(serial)) return;
            _runtimeSessions.BindServiceInstance(
                serial,
                context.Runtime.InstanceId);
            var newlyConnected = !context.WasConnected;
            var connectionChanged = newlyConnected ||
                !string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase);
            var previousSerial = context.ActiveSerial;
            if (context.WasConnected && connectionChanged &&
                !string.IsNullOrWhiteSpace(previousSerial))
            {
                context.Runtime.FileTransfers.CancelSerial(previousSerial);
                var detachTask = context.Runtime.PhoneTransfers.DetachAsync(
                    previousSerial);
                ForgetDeviceConnectionTimestamp(previousSerial);
            }
            context.WasConnected = true;
            context.ActiveSerial = serial;
            if (connectionChanged)
            {
                RecordDeviceConnected(serial);
                MarkSerialReconnected(serial);
                ConfigurePhoneTransferReceiver(context, serial);
                RetryContextCleanupAsync(
                    context,
                    serial,
                    newlyConnected);
            }
        }

        private async void RetryContextCleanupAsync(
            DeviceUiContext context,
            string serial,
            bool allowAutoStart)
        {
            try
            {
                var cleanupReady = await context.Runtime.Dex
                    .RetryDeferredCleanupAsync(serial);
                if (cleanupReady && allowAutoStart &&
                    _settings.Features.AutoStartDexOnDeviceConnected &&
                    ReferenceEquals(context, _selectedDeviceContext) &&
                    await WaitForDeviceStartDelayAsync(serial))
                {
                    await StartDexAsync();
                }
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not retry deferred cleanup for " + serial + ".",
                    ex);
            }
        }

        private void HandleContextDisconnected(DeviceUiContext context)
        {
            context.WasConnected = false;
            var serial = context.ActiveSerial;
            context.ActiveSerial = string.Empty;
            if (string.IsNullOrWhiteSpace(serial)) return;
            ForgetDeviceConnection(serial);
            MarkSerialDisconnected(serial);
            context.Runtime.FileTransfers.CancelSerial(serial);
            var detachTask = context.Runtime.PhoneTransfers.DetachAsync(serial);
            Task.Run(async delegate
            {
                try
                {
                    if (context.Runtime.Dex.IsRunning)
                        await context.Runtime.Dex.StopAsync()
                            .ConfigureAwait(false);
                    context.Runtime.SingleWindows.StopAll();
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        "Could not clean the disconnected device runtime " +
                        serial + ".",
                        ex);
                }
            });
        }

        private async void ConfigurePhoneTransferReceiver(
            DeviceUiContext context,
            string serial)
        {
            if (_exitInProgress || context == null ||
                string.IsNullOrWhiteSpace(serial)) return;
            try
            {
                await context.Runtime.PhoneTransfers.AttachAsync(serial);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not prepare phone-to-PC transfer for " + serial +
                    ".",
                    ex);
            }
        }

        private void RebuildDeviceTabs()
        {
            _deviceTabsPanel.SuspendLayout();
            try
            {
                while (_deviceTabsPanel.Controls.Count > 0)
                    _deviceTabsPanel.Controls[0].Dispose();
                _deviceTabsPanel.Controls.Clear();
                _deviceTabButtons.Clear();
                foreach (var item in GetDeviceContextEntries())
                {
                    var context = item.Value;
                    var device = context.Device;
                    var connected = device != null && device.IsConnected;
                    var transport = device == null
                        ? null
                        : device.SelectPreferredTransport(
                            GetContextSerial(context));
                    var transportText = GetTransportText(transport);
                    var button = new ThemedButton
                    {
                        Size = new Size(154, 38),
                        Margin = new Padding(0, 0, 6, 0),
                        NavigationStyle = true,
                        ShowNavigationDot = true,
                        Primary = ReferenceEquals(
                            context,
                            _selectedDeviceContext),
                        Text = GetContextDisplayName(context),
                        TrailingText = connected
                            ? transportText
                            : LocalizationService.Get(
                                "Device.Disconnected")
                    };
                    button.Click += delegate
                    {
                        SelectDeviceContext(context);
                    };
                    _deviceTabButtons[item.Key] = button;
                    _deviceTabsPanel.Controls.Add(button);
                }
            }
            finally
            {
                _deviceTabsPanel.ResumeLayout();
            }
        }

        private void SelectDeviceContext(DeviceUiContext context)
        {
            if (context == null || ReferenceEquals(
                    context,
                    _selectedDeviceContext) &&
                !string.IsNullOrWhiteSpace(context.Identity))
            {
                return;
            }

            SaveCurrentModeBeforeSwitch();
            if (_selectedDeviceContext != null)
            {
                _selectedDeviceContext.SelectedMode = _selectedMode;
                _selectedDeviceContext.ModeSettingsDirty =
                    _modeSettingsDirty;
            }
            StopInteractiveContext(_selectedDeviceContext);
            _selectedDeviceContext = context;
            _selectedDeviceIdentity = context.Identity ?? string.Empty;
            _modeSettingsDirty = context.ModeSettingsDirty ??
                new bool[4];
            ActivateContextServices(context);
            if (_interactiveServicesStarted)
                StartInteractiveContext(context);
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.Close();
            if (_environmentCheckForm != null &&
                !_environmentCheckForm.IsDisposed)
            {
                _environmentCheckForm.Close();
            }
            _connectionError = null;
            var serial = GetContextSerial(context);
            var transport = context.Device == null
                ? null
                : context.Device.SelectPreferredTransport(serial);
            _logService.Info(LocalizationService.Format(
                "Log.DeviceContext.Selected",
                GetContextDisplayName(context),
                string.IsNullOrWhiteSpace(serial)
                    ? context.Identity
                    : serial,
                GetTransportText(transport)));
            RefreshSelectedDeviceState();
            RebuildDeviceTabs();
            DisplayMode(context.SelectedMode);
        }

        private void ActivateContextServices(DeviceUiContext context)
        {
            _activeRuntime = context.Runtime;
            _fileTransferCoordinator = context.Runtime.FileTransfers;
            _phoneTransferReceiver = context.Runtime.PhoneTransfers;
            _scrcpyService = context.Runtime.Scrcpy;
            _singleWindowService = context.Runtime.SingleWindows;
            _screenOffService = context.Runtime.ScreenOff;
            _orchestrator = context.Runtime.Dex;
            _captureCoordinator = context.Capture;
            _autoHideService = context.AutoHide;
            _environmentCheckService = context.EnvironmentCheck;
            _keyMappingService = context.KeyMapping;
            _miniControlBarManager = context.MiniBar;
        }

        private void StartInteractiveContext(DeviceUiContext context)
        {
            if (context == null) return;
            try { context.Capture.Start(); }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.CaptureHotkeyRegistrationFailed"),
                    ex);
            }
            if (_settings.Features.AutoHideEnabled)
                context.AutoHide.Start();
            try { context.KeyMapping.Start(); }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.KeyMappingStartFailed"),
                    ex);
            }
        }

        private void StopInteractiveContext(DeviceUiContext context)
        {
            if (context == null) return;
            TryCleanup("capture coordinator", context.Capture.Stop);
            TryCleanup("automatic hide", context.AutoHide.Stop);
            TryCleanup("key mapping", context.KeyMapping.Stop);
        }

        private void RefreshSelectedDeviceState()
        {
            var context = _selectedDeviceContext;
            var device = context == null ? null : context.Device;
            var transport = device == null
                ? null
                : device.SelectPreferredTransport(GetContextSerial(context));
            _lastDeviceState = transport == null
                ? DeviceState.Disconnected()
                : new DeviceState
                {
                    IsConnected = transport.IsAuthorized,
                    Serial = transport.Serial,
                    DisplayName = device.DisplayName,
                    Status = transport.Status
                };
            _deviceInfoLabel.Text = device == null
                ? LocalizationService.Get("Main.WaitingPhone")
                : LocalizationService.Format(
                    "Main.ConnectedDevice",
                    GetContextDisplayName(context),
                    GetTransportText(transport));
            _adbStatusValue.Text = _lastDeviceState.IsConnected
                ? LocalizationService.Get("Status.Ready")
                : LocalizationService.Get("Status.Idle");
            _deviceStatusValue.Text = GetDeviceStatusText(_lastDeviceState);
            if (!IsSelectedModeRunning())
                UpdateIndicatorForDevice(_lastDeviceState);
        }

        private string GetContextSerial(DeviceUiContext context)
        {
            if (context == null || context.Device == null)
                return context == null
                    ? string.Empty
                    : context.ActiveSerial ?? string.Empty;
            var session = _runtimeSessions.Current.FindByIdentity(
                context.Identity);
            var preferred = context.Device.SelectPreferredTransport(
                session == null
                    ? string.Empty
                    : session.ActiveTransportSerial);
            return preferred == null ? string.Empty : preferred.Serial;
        }

        private static string GetContextDisplayName(DeviceUiContext context)
        {
            if (context == null) return string.Empty;
            if (context.Device != null &&
                !string.IsNullOrWhiteSpace(context.Device.DisplayName))
            {
                return context.Device.DisplayName;
            }
            return context.Identity ?? string.Empty;
        }

        private static string GetTransportText(DeviceTransportInfo transport)
        {
            if (transport == null) return "-";
            switch (transport.Kind)
            {
                case DeviceTransportKind.Usb: return "USB";
                case DeviceTransportKind.Wireless: return "Wi-Fi";
                case DeviceTransportKind.Emulator: return "Emulator";
                default: return "ADB";
            }
        }

        private bool IsActiveRuntimeSender(object sender)
        {
            return ReferenceEquals(sender, _scrcpyService) ||
                ReferenceEquals(sender, _singleWindowService);
        }

        private ScreenOffService GetScreenOffServiceForSerial(string serial)
        {
            foreach (var context in GetAllDeviceContexts())
            {
                var dexSession = context.Runtime.Scrcpy
                    .GetSessionSnapshot();
                if (string.Equals(
                        dexSession.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return context.Runtime.ScreenOff;
                }
                foreach (var candidate in context.Runtime.SingleWindows
                    .GetRunningSerials())
                {
                    if (string.Equals(
                            candidate,
                            serial,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return context.Runtime.ScreenOff;
                    }
                }
            }
            return _screenOffService;
        }

        private void RequestAllRuntimeShutdown()
        {
            foreach (var context in GetAllDeviceContexts())
            {
                context.Runtime.Dex.RequestShutdown();
                context.Runtime.SingleWindows.RequestShutdown();
                context.Runtime.ScreenOff.RequestShutdown();
                context.Runtime.FileTransfers.RequestShutdown();
                context.Runtime.PhoneTransfers.RequestShutdown();
            }
        }

        private IList<DeviceUiContext> GetAllDeviceContexts()
        {
            var result = new List<DeviceUiContext>();
            if (_initialDeviceContext != null)
                result.Add(_initialDeviceContext);
            lock (_deviceContextsSync)
            {
                foreach (var context in _deviceContexts.Values)
                {
                    if (!result.Contains(context)) result.Add(context);
                }
            }
            return result;
        }

        private IList<KeyValuePair<string, DeviceUiContext>>
            GetDeviceContextEntries()
        {
            lock (_deviceContextsSync)
            {
                return new List<KeyValuePair<string, DeviceUiContext>>(
                    _deviceContexts);
            }
        }

        private async Task CleanupAllRuntimeSessionsAsync()
        {
            foreach (var context in GetAllDeviceContexts())
            {
                var serial = GetContextSerial(context);
                await TryCleanupAsync(
                    "DeX session " + GetContextDisplayName(context),
                    delegate
                    {
                        return context.Runtime.Dex.ShutdownAsync(serial);
                    }).ConfigureAwait(false);
                await TryCleanupAsync(
                    "single-window sessions " +
                        GetContextDisplayName(context),
                    delegate
                    {
                        return Task.Run(
                            (Action)context.Runtime.SingleWindows.StopAll);
                    }).ConfigureAwait(false);
            }
        }

        private void DisposeAllDeviceContexts()
        {
            foreach (var context in GetAllDeviceContexts())
            {
                context.Runtime.Scrcpy.RunningChanged -=
                    ScrcpyService_RunningChanged;
                context.Runtime.SingleWindows.RunningChanged -=
                    SingleWindowService_RunningChanged;
                context.Capture.ExitHotkeyPressed -=
                    CaptureCoordinator_ExitHotkeyPressed;
                context.AutoHide.IdleHideRequested -=
                    AutoHideService_IdleHideRequested;
                context.Runtime.FileTransfers.ProgressChanged -=
                    FileTransferCoordinator_ProgressChanged;
                context.Runtime.PhoneTransfers.ProgressChanged -=
                    PhoneTransferReceiver_ProgressChanged;
                TryCleanup("mini control bar", context.MiniBar.Dispose);
                TryCleanup("capture coordinator", context.Capture.Dispose);
                TryCleanup("automatic hide", context.AutoHide.Dispose);
                TryCleanup("key mapping", context.KeyMapping.Dispose);
                TryCleanup(
                    "screen-off service",
                    context.Runtime.ScreenOff.Dispose);
                TryCleanup(
                    "single-window service",
                    context.Runtime.SingleWindows.Dispose);
                TryCleanup(
                    "scrcpy service",
                    context.Runtime.Scrcpy.Dispose);
                TryCleanup(
                    "file transfer service",
                    context.Runtime.FileTransfers.Dispose);
                TryCleanup(
                    "phone transfer receiver",
                    context.Runtime.PhoneTransfers.Dispose);
            }
        }

        private Task DetachSelectedPhoneTransferAsync(string serial)
        {
            return _phoneTransferReceiver.DetachAsync(serial);
        }
    }
}

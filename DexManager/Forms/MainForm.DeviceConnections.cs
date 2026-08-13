using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
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

            // The compatibility context is assigned to the first registry
            // entry that is materialized.  That registry order is unrelated
            // to the presentation order, so remember that initial selection
            // is still pending before the context receives an identity.
            var initialSelectionPending =
                _initialDeviceContext != null &&
                string.IsNullOrWhiteSpace(_initialDeviceContext.Identity);

            if (!_deviceTabsVisibleForRun &&
                CountConnectedPhysicalDevices(snapshot) >= 2)
            {
                _deviceTabsVisibleForRun = true;
            }

            _devicePresentationOrder.Reconcile(snapshot.Devices);

            foreach (var context in _deviceContexts.Values)
                context.Device = null;

            foreach (var device in snapshot.Devices)
            {
                if (device == null ||
                    string.IsNullOrWhiteSpace(device.Identity)) continue;

                var context = EnsureDeviceContext(device);
                context.Device = device.Clone();
                RememberContextDisplayName(context, device);
                ConfigureContextPresentation(context);
                ConfigureContextConnection(context);
            }

            foreach (var context in _deviceContexts.Values)
            {
                if (context.Device == null && context.WasConnected)
                    HandleContextDisconnected(context);
            }

            if (initialSelectionPending ||
                _selectedDeviceContext == null ||
                string.IsNullOrWhiteSpace(_selectedDeviceContext.Identity))
            {
                foreach (var identity in
                    _devicePresentationOrder.GetIdentities())
                {
                    DeviceUiContext context;
                    if (_deviceContexts.TryGetValue(identity, out context) &&
                        context.Device != null &&
                        context.Device.IsConnected)
                    {
                        SelectDeviceContext(context);
                        break;
                    }
                }
            }

            RebuildDeviceTabs();
            RefreshSelectedDeviceState();
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.RefreshSelectedDeviceContext();
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
            RememberContextDisplayName(context, device);
            context.Runtime.Dex.DeviceIdentity = device.Identity;
            ConfigureContextPresentation(context);
            lock (_deviceContextsSync)
                _deviceContexts.Add(device.Identity, context);
            if (ReferenceEquals(context, _selectedDeviceContext))
                _selectedDeviceIdentity = device.Identity;

            var transport = GetContextTransport(context);
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
            if (context == null || context.Device == null)
            {
                if (context != null && context.WasConnected)
                    HandleContextDisconnected(context);
                return;
            }

            var selectedTransport = GetContextTransport(context);
            var serial = selectedTransport == null
                ? string.Empty
                : selectedTransport.Serial;
            if (string.IsNullOrWhiteSpace(serial))
            {
                if (context.WasConnected)
                    HandleContextDisconnected(context);
                return;
            }
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
                context.ConnectionGeneration++;
                if (!newlyConnected &&
                    !string.IsNullOrWhiteSpace(previousSerial))
                {
                    SwitchContextTransportAsync(
                        context,
                        previousSerial,
                        serial,
                        context.ConnectionGeneration);
                    return;
                }
                RecordDeviceConnected(serial);
                MarkSerialReconnected(serial);
                ConfigurePhoneTransferReceiver(context, serial);
                RetryContextCleanupAsync(
                    context,
                    serial,
                    newlyConnected);
            }
        }

        private async void SwitchContextTransportAsync(
            DeviceUiContext context,
            string previousSerial,
            string serial,
            int generation)
        {
            try
            {
                if (context.Runtime.Dex.IsRunning)
                    await context.Runtime.Dex.StopAsync();
                await Task.Run((Action)context.Runtime.SingleWindows.StopAll);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not stop the previous transport session " +
                    previousSerial + ".",
                    ex);
            }

            if (_exitInProgress || context.ConnectionGeneration != generation ||
                !context.WasConnected || !string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    GetContextSerial(context),
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RecordDeviceConnected(serial);
            MarkSerialReconnected(serial);
            ConfigurePhoneTransferReceiver(context, serial);
            RetryContextCleanupAsync(context, serial, false);
            RefreshSelectedDeviceState();
            RebuildDeviceTabs();
        }

        private async void RetryContextCleanupAsync(
            DeviceUiContext context,
            string serial,
            bool allowAutoStart)
        {
            var generation = context == null
                ? -1
                : context.ConnectionGeneration;
            try
            {
                var cleanupReady = await context.Runtime.Dex
                    .RetryDeferredCleanupAsync(serial);
                if (cleanupReady && allowAutoStart &&
                    _settings.Features.AutoStartDexOnDeviceConnected &&
                    await WaitForDeviceStartDelayAsync(
                        serial,
                        context,
                        generation))
                {
                    await StartDexForContextAsync(
                        context,
                        serial,
                        generation);
                }
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not retry deferred cleanup for " + serial + ".",
                    ex);
            }
        }

        private async Task StartDexForContextAsync(
            DeviceUiContext context,
            string serial,
            int generation)
        {
            if (!IsContextConnectionCurrent(context, serial, generation) ||
                context.Runtime.Dex.IsShutdownRequested ||
                context.Runtime.Dex.IsRunning)
            {
                return;
            }

            try
            {
                var runSettings = GetDeviceRunSettings(
                    _settings,
                    context.Identity);
                if (runSettings.Scrcpy.TurnScreenOff)
                    RememberManagedSerial(serial);

                if (ReferenceEquals(context, _selectedDeviceContext))
                {
                    _connectionError = null;
                    SetOperationState(
                        true,
                        LocalizationService.Get("Status.Starting"));
                    SetConnectionIndicator(
                        Color.DarkOrange,
                        LocalizationService.Get("Main.DexStarting"),
                        LocalizationService.Get("Main.DexPreparing"));
                }

                _logService.Info(
                    "Starting DeX automatically for " +
                    GetContextDisplayName(context) + " (" + serial + ").");
                await context.Runtime.Dex.StartAsync(serial);

                if (_exitInProgress ||
                    !IsContextConnectionCurrent(
                        context,
                        serial,
                        generation) ||
                    !context.Runtime.Dex.IsRunning)
                {
                    return;
                }

                RememberStartedApp(
                    runSettings.Scrcpy.StartAppPackage,
                    runSettings.Scrcpy.StartAppName);
                context.ModeSettingsDirty[0] = false;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not start DeX automatically for " +
                    GetContextDisplayName(context) + " (" + serial + ").",
                    ex);
                if (!_exitInProgress && ReferenceEquals(
                        context,
                        _selectedDeviceContext))
                {
                    ShowError(
                        LocalizationService.Get("Error.StartDex"),
                        ex);
                }
            }
            finally
            {
                if (!_exitInProgress && !IsDisposed)
                {
                    if (ReferenceEquals(context, _selectedDeviceContext))
                        UpdateRunningState();
                    UpdatePhoneScreenWakeSchedule();
                    RebuildDeviceTabs();
                }
            }
        }

        private bool IsContextConnectionCurrent(
            DeviceUiContext context,
            string serial,
            int generation)
        {
            return context != null &&
                !_exitInProgress &&
                !IsDisposed &&
                context.ConnectionGeneration == generation &&
                context.WasConnected &&
                string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    GetContextSerial(context),
                    serial,
                    StringComparison.OrdinalIgnoreCase) &&
                !IsSerialMarkedDisconnected(serial);
        }

        private void HandleContextDisconnected(DeviceUiContext context)
        {
            context.WasConnected = false;
            context.ConnectionGeneration++;
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
                await context.Runtime.PhoneTransfers.AttachAsync(
                    serial,
                    GetContextDisplayName(context));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not prepare phone-to-PC transfer for " + serial +
                    ".",
                    ex);
            }
        }

        private void ConfigurePhoneTransferReceiver(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            foreach (var context in GetAllDeviceContexts())
            {
                if (!string.Equals(
                    GetContextSerial(context),
                    serial,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                ConfigurePhoneTransferReceiver(context, serial);
                return;
            }
            _logService.Warning(
                "Could not find a device runtime for phone transfer: " +
                serial + ".");
        }

    }
}

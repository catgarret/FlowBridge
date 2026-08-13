using System;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
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
            return await WaitForDeviceStartDelayAsync(
                serial,
                null,
                -1);
        }

        private async Task<bool> WaitForDeviceStartDelayAsync(
            string serial,
            DeviceUiContext context,
            int generation)
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
                (context == null && !string.Equals(
                    GetSelectedDeviceSerial(),
                    serial,
                    StringComparison.OrdinalIgnoreCase)) ||
                (context != null && !IsContextConnectionCurrent(
                    context,
                    serial,
                    generation)))
            {
                return false;
            }
            var current = context == null
                ? _deviceMonitor.CurrentState
                : null;
            if (context == null && current != null &&
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
                (context == null
                    ? string.Equals(
                        GetSelectedDeviceSerial(),
                        serial,
                        StringComparison.OrdinalIgnoreCase)
                    : IsContextConnectionCurrent(
                        context,
                        serial,
                        generation));
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

    }
}


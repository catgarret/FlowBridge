using System;
using DexManager.Models;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
        private DeviceRunSettingsProfile GetSelectedDeviceRunSettings()
        {
            return GetDeviceRunSettings(
                _settings,
                _selectedDeviceIdentity);
        }

        private static DeviceRunSettingsProfile GetDeviceRunSettings(
            AppSettings settings,
            string deviceIdentity)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (!string.IsNullOrWhiteSpace(deviceIdentity))
            {
                return settings.GetOrCreateDeviceRunSettings(
                    deviceIdentity);
            }

            // Before the first physical device is identified, preserve the
            // legacy settings as the default template shown by the main UI.
            return new DeviceRunSettingsProfile
            {
                DeviceIdentity = string.Empty,
                VirtualDisplay = settings.VirtualDisplay,
                Scrcpy = settings.Scrcpy,
                LastSuccess = settings.LastSuccess,
                SingleWindowSlots = settings.SingleWindowSlots,
                SingleWindowAppProfiles =
                    settings.SingleWindowAppProfiles
            };
        }
    }
}

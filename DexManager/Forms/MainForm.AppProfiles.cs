using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private void SetAppProfileControlsVisible(bool visible)
        {
            _appProfileButton.Visible = visible;
            if (visible) UpdateAppProfileControls();
        }

        private void UpdateAppProfileControls()
        {
            if (_appProfileButton == null || _selectedMode == 0)
                return;

            var packageName = GetSelectedAppPackage();
            var hasApp = !string.IsNullOrWhiteSpace(packageName);
            var profile = hasApp
                ? FindSingleWindowAppProfile(
                    GetSelectedDeviceRunSettings(),
                    packageName)
                : null;

            _appProfileButton.Text = !hasApp
                ? LocalizationService.Get("Main.AppProfile.SelectApp")
                : profile == null
                    ? LocalizationService.Get("Main.AppProfile.NotSaved")
                    : LocalizationService.Get("Main.AppProfile.Saved");
            _appProfileButton.Enabled = hasApp;
            _saveAppProfileMenuItem.Enabled = hasApp;
            _deleteAppProfileMenuItem.Enabled = profile != null;
        }

        private void AppProfileButton_Click(object sender, EventArgs e)
        {
            if (!_appProfileButton.Enabled) return;
            _appProfileMenu.Show(
                _appProfileButton,
                new System.Drawing.Point(0, _appProfileButton.Height));
        }

        private bool ApplySelectedAppProfileIfAvailable()
        {
            var packageName = GetSelectedAppPackage();
            if (string.IsNullOrWhiteSpace(packageName)) return false;

            var profile = FindSingleWindowAppProfile(
                GetSelectedDeviceRunSettings(),
                packageName);
            if (profile == null) return false;

            var appName = GetSelectedAppName(packageName);
            var selectedMode = _selectedMode;
            try
            {
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    var runSettings = GetDeviceRunSettings(
                        candidate,
                        _selectedDeviceIdentity);
                    var slot = GetSingleWindowSettings(
                        runSettings,
                        selectedMode);
                    CopyProfileToSlot(
                        profile,
                        slot,
                        packageName,
                        appName);
                });
                LoadRunSettings();
                _logService.Info(LocalizationService.Format(
                    "Log.Main.AppProfileApplied",
                    appName,
                    selectedMode));
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppProfileApplyFailed"),
                    ex);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.AppProfile.ApplyFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        private void SaveAppProfileButton_Click(
            object sender,
            EventArgs e)
        {
            if (_selectedMode == 0) return;

            var packageName = GetSelectedAppPackage();
            if (string.IsNullOrWhiteSpace(packageName)) return;
            var appName = GetSelectedAppName(packageName);
            var existing = FindSingleWindowAppProfile(
                GetSelectedDeviceRunSettings(),
                packageName);
            if (existing != null)
            {
                var result = MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.AppProfile.OverwriteConfirm",
                        appName),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes) return;
            }

            try
            {
                ApplyRunSettings(false);
                var source = GetSingleWindowSettings(_selectedMode);
                var profile = CreateProfileFromSlot(
                    source,
                    packageName,
                    appName);
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    var runSettings = GetDeviceRunSettings(
                        candidate,
                        _selectedDeviceIdentity);
                    if (runSettings.SingleWindowAppProfiles == null)
                    {
                        runSettings.SingleWindowAppProfiles =
                            new List<SingleWindowAppProfile>();
                    }

                    for (var index = 0;
                        index < runSettings.SingleWindowAppProfiles.Count;
                        index++)
                    {
                        var current =
                            runSettings.SingleWindowAppProfiles[index];
                        if (current == null ||
                            !string.Equals(
                                current.PackageName,
                                packageName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        runSettings.SingleWindowAppProfiles[index] = profile;
                        return;
                    }

                    runSettings.SingleWindowAppProfiles.Add(profile);
                });
                UpdateAppProfileControls();
                _logService.Info(LocalizationService.Format(
                    "Log.Main.AppProfileSaved",
                    appName));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppProfileSaveFailed"),
                    ex);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.AppProfile.SaveFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void DeleteAppProfileButton_Click(
            object sender,
            EventArgs e)
        {
            if (_selectedMode == 0) return;

            var packageName = GetSelectedAppPackage();
            var profile = FindSingleWindowAppProfile(
                GetSelectedDeviceRunSettings(),
                packageName);
            if (profile == null) return;

            var appName = GetSelectedAppName(packageName);
            var result = MessageBox.Show(
                this,
                LocalizationService.Format(
                    "Main.AppProfile.DeleteConfirm",
                    appName),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes) return;

            try
            {
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    var runSettings = GetDeviceRunSettings(
                        candidate,
                        _selectedDeviceIdentity);
                    if (runSettings.SingleWindowAppProfiles == null) return;
                    for (var index =
                            runSettings.SingleWindowAppProfiles.Count - 1;
                        index >= 0;
                        index--)
                    {
                        var current =
                            runSettings.SingleWindowAppProfiles[index];
                        if (current != null &&
                            string.Equals(
                                current.PackageName,
                                packageName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            runSettings.SingleWindowAppProfiles.RemoveAt(index);
                        }
                    }
                });
                UpdateAppProfileControls();
                _logService.Info(LocalizationService.Format(
                    "Log.Main.AppProfileDeleted",
                    appName));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppProfileDeleteFailed"),
                    ex);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.AppProfile.DeleteFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static SingleWindowAppProfile FindSingleWindowAppProfile(
            DeviceRunSettingsProfile settings,
            string packageName)
        {
            if (settings == null ||
                settings.SingleWindowAppProfiles == null ||
                string.IsNullOrWhiteSpace(packageName))
            {
                return null;
            }

            foreach (var profile in settings.SingleWindowAppProfiles)
            {
                if (profile != null &&
                    string.Equals(
                        profile.PackageName,
                        packageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            return null;
        }

        private static SingleWindowAppProfile CreateProfileFromSlot(
            SingleWindowSlotSettings source,
            string packageName,
            string appName)
        {
            return new SingleWindowAppProfile
            {
                PackageName = packageName,
                AppName = appName,
                Width = source.Width,
                Height = source.Height,
                Dpi = source.Dpi,
                BitRate = source.BitRate,
                MaxFps = source.MaxFps,
                TurnScreenOff = source.TurnScreenOff,
                StayAwake = source.StayAwake,
                UseHidKeyboard = source.UseHidKeyboard,
                UseHidMouse = source.UseHidMouse,
                ForceStopStartApp = source.ForceStopStartApp,
                AdditionalArguments = source.AdditionalArguments,
                CustomWidth = source.CustomWidth,
                CustomHeight = source.CustomHeight,
                FlexDisplay = source.FlexDisplay
            };
        }

        private static void CopyProfileToSlot(
            SingleWindowAppProfile source,
            SingleWindowSlotSettings target,
            string packageName,
            string appName)
        {
            target.Width = source.Width;
            target.Height = source.Height;
            target.Dpi = source.Dpi;
            target.BitRate = source.BitRate;
            target.MaxFps = source.MaxFps;
            target.TurnScreenOff = source.TurnScreenOff;
            target.StayAwake = source.StayAwake;
            target.UseHidKeyboard = source.UseHidKeyboard;
            target.UseHidMouse = source.UseHidMouse;
            target.ForceStopStartApp = source.ForceStopStartApp;
            target.StartAppPackage = packageName;
            target.StartAppName = string.IsNullOrWhiteSpace(appName)
                ? source.AppName
                : appName;
            target.AdditionalArguments = source.AdditionalArguments;
            target.CustomWidth = source.CustomWidth;
            target.CustomHeight = source.CustomHeight;
            target.FlexDisplay = source.FlexDisplay;
        }
    }
}

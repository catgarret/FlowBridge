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
        private void ResolutionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingRunSettings) return;

            if (_resolutionSelectionInitialized && _resolutionWasCustom)
                StoreCurrentCustomResolution();

            ApplyResolutionSelection();
        }

        private void ApplyResolutionSelection()
        {
            var preset = _resolutionBox.SelectedItem as ResolutionPreset;
            var custom = preset == null || preset.Width == 0;
            LayoutResolutionControls(custom);
            _widthBox.Enabled = custom;
            _heightBox.Enabled = custom;
            _widthBox.Visible = custom;
            _heightBox.Visible = custom;
            _widthLabel.Visible = custom;
            _heightLabel.Visible = custom;
            if (custom)
            {
                _widthBox.Value = Clamp(
                    GetCurrentCustomWidth(),
                    _widthBox);
                _heightBox.Value = Clamp(
                    GetCurrentCustomHeight(),
                    _heightBox);
            }
            else
            {
                _widthBox.Value = preset.Width;
                _heightBox.Value = preset.Height;
            }

            _resolutionWasCustom = custom;
            _resolutionSelectionInitialized = true;
        }

        private void LayoutResolutionControls(bool custom)
        {
            if (!custom)
            {
                _resolutionBox.Width = 304;
                return;
            }

            const int fieldTop = 72;
            const int labelGap = 6;
            const int numberWidth = 50;
            const int widthGroupOffset = 50;
            const int dpiGap = 22;

            _resolutionBox.Width = 110;
            _widthBox.Width = numberWidth;
            _heightBox.Width = numberWidth;

            // Keep the numeric fields on the same axes in every language.
            // Only the translated labels move, right-aligned to their field.
            _widthBox.Left = _resolutionBox.Right + widthGroupOffset;
            _heightBox.Left = _dpiBox.Left - dpiGap - numberWidth;

            var heightLabelWidth = MeasureInlineLabel(_heightLabel);
            _heightLabel.Left =
                _heightBox.Left - labelGap - heightLabelWidth;
            var widthLabelWidth = MeasureInlineLabel(_widthLabel);
            _widthLabel.Left =
                _widthBox.Left - labelGap - widthLabelWidth;

            _widthBox.Top = fieldTop;
            _heightBox.Top = fieldTop;
            _widthLabel.Top = GetInlineLabelTop(_widthLabel, fieldTop);
            _heightLabel.Top = GetInlineLabelTop(_heightLabel, fieldTop);
        }

        private static int MeasureInlineLabel(Label label)
        {
            return TextRenderer.MeasureText(
                label.Text,
                label.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
        }

        private static int GetInlineLabelTop(
            Label label,
            int fieldTop)
        {
            var labelHeight = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Height;
            return fieldTop + (32 - labelHeight) / 2 + 1;
        }

        private void LoadRunSettings()
        {
            int width;
            int height;
            int dpi;
            string bitRate;
            int maxFps;
            bool turnScreenOff;
            bool stayAwake;
            bool useHidKeyboard;
            bool useHidMouse;
            bool forceStopStartApp;
            bool flexDisplay;
            string startAppPackage;
            string startAppName;
            string additionalArguments;

            _loadingRunSettings = true;
            _resolutionSelectionInitialized = false;
            var runSettings = GetSelectedDeviceRunSettings();
            if (_selectedMode == 0)
            {
                width = runSettings.VirtualDisplay.Width;
                height = runSettings.VirtualDisplay.Height;
                dpi = runSettings.VirtualDisplay.Dpi;
                bitRate = runSettings.Scrcpy.BitRate;
                maxFps = runSettings.Scrcpy.MaxFps;
                turnScreenOff = runSettings.Scrcpy.TurnScreenOff;
                stayAwake = runSettings.Scrcpy.StayAwake;
                useHidKeyboard = runSettings.Scrcpy.UseHidKeyboard;
                useHidMouse = runSettings.Scrcpy.UseHidMouse;
                forceStopStartApp = runSettings.Scrcpy.ForceStopStartApp;
                flexDisplay = false;
                startAppPackage = runSettings.Scrcpy.StartAppPackage;
                startAppName = runSettings.Scrcpy.StartAppName;
                additionalArguments =
                    runSettings.Scrcpy.AdditionalArguments;
            }
            else
            {
                var slot = GetSingleWindowSettings(_selectedMode);
                width = slot.Width;
                height = slot.Height;
                dpi = slot.Dpi;
                bitRate = slot.BitRate;
                maxFps = slot.MaxFps;
                turnScreenOff = slot.TurnScreenOff;
                stayAwake = slot.StayAwake;
                useHidKeyboard = slot.UseHidKeyboard;
                useHidMouse = slot.UseHidMouse;
                forceStopStartApp = slot.ForceStopStartApp;
                flexDisplay = slot.FlexDisplay;
                startAppPackage = slot.StartAppPackage;
                startAppName = slot.StartAppName;
                additionalArguments = slot.AdditionalArguments;
            }

            _widthBox.Value = Clamp(width, _widthBox);
            _heightBox.Value = Clamp(height, _heightBox);
            _dpiBox.Value = Clamp(dpi, _dpiBox);
            _bitRateBox.Value = Clamp(
                ParseBitRateNumber(bitRate),
                _bitRateBox);
            _maxFpsBox.SelectedItem = maxFps == 30 ? 30 : 60;
            _turnScreenOffBox.Checked = turnScreenOff;
            _stayAwakeBox.Checked = stayAwake;
            _useHidKeyboardBox.Checked = useHidKeyboard;
            _useHidMouseBox.Checked = useHidMouse;
            _forceStopAppBox.Checked = forceStopStartApp;
            _flexDisplayBox.Checked = flexDisplay;
            _additionalArgumentsBox.Text = additionalArguments;
            SetSelectedAppPackage(startAppPackage, startAppName);
            _resolutionBox.SelectedIndex = FindResolutionPresetIndex(
                width,
                height);
            ApplyResolutionSelection();
            _loadingRunSettings = false;
            UpdateApplySettingsLink();
            UpdateAppProfileControls();
        }

        private int FindResolutionPresetIndex(int width, int height)
        {
            for (var index = 0; index < _resolutionBox.Items.Count; index++)
            {
                var preset = _resolutionBox.Items[index] as ResolutionPreset;
                if (preset != null && preset.Width == width && preset.Height == height)
                    return index;
            }

            return _resolutionBox.Items.Count - 1;
        }

        private async void ApplyRunSettingsButton_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyRunSettings(false);
                if (_selectedMode > 0)
                {
                    await ApplySingleWindowSettingsAsync(_selectedMode);
                    return;
                }
                var serial = GetSelectedDeviceSerial();
                if (string.IsNullOrWhiteSpace(serial) ||
                    !_adbService.IsAuthorizedDeviceConnected(serial))
                {
                    _logService.Info(LocalizationService.Get(
                        "Log.Main.SettingsDeferredNoDevice"));
                    MessageBox.Show(
                        this,
                        LocalizationService.Get("Main.ApplyNoDevice"),
                        LocalizationService.Get("App.Name"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _modeSettingsDirty[0] = false;
                    return;
                }

                _connectionError = null;
                SetOperationState(
                    true,
                    LocalizationService.Get("Status.Applying"));
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.ApplyStatus"),
                    LocalizationService.Get("Main.ApplyRestartDetail"));

                bool applied;
                BeginPhoneScreenWakeSuppression();
                try
                {
                    applied = await _orchestrator.ApplyRuntimeSettingsAsync();
                }
                finally
                {
                    EndPhoneScreenWakeSuppression();
                }
                if (!applied)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.Get("Main.ApplyDeferred"),
                        LocalizationService.Get("App.Name"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateRunningState();
                _modeSettingsDirty[0] = false;
                MessageBox.Show(
                    this,
                    LocalizationService.Get("Main.ApplySucceeded"),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.SettingsImmediateApplyFailed"),
                    ex);
                _connectionError = LocalizationService.Format(
                    "Main.ApplyFailedShort",
                    ex.Message);
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.ApplyFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                UpdateRunningState();
            }
        }

        private async Task ApplySingleWindowSettingsAsync(int slot)
        {
            var serial = GetSelectedDeviceSerial();
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.SingleSavedNoDevice",
                        slot,
                        Environment.NewLine),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _modeSettingsDirty[slot] = false;
                return;
            }

            if (!_singleWindowService.IsRunning(slot))
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.SingleSaved",
                        slot),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _modeSettingsDirty[slot] = false;
                return;
            }

            SetOperationState(
                true,
                LocalizationService.Get("Status.Applying"));
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Format(
                    "Main.SingleApplying",
                    slot),
                LocalizationService.Get("Main.SingleRestartDetail"));
            var settings = GetSingleWindowSettings(slot);
            BeginPhoneScreenWakeSuppression();
            try
            {
                await Task.Run(delegate
                {
                    _singleWindowService.Restart(slot, settings);
                });
            }
            finally
            {
                EndPhoneScreenWakeSuppression();
            }
            UpdateRunningState();
            _modeSettingsDirty[slot] = false;
            MessageBox.Show(
                this,
                LocalizationService.Format(
                    "Main.SingleApplied",
                    slot),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void LoadAppsButton_Click(object sender, EventArgs e)
        {
            if (_loadAppsButton.Enabled == false) return;

            _loadAppsButton.Enabled = false;
                _loadAppsButton.Text = LocalizationService.Get("Main.Loading");
            try
            {
                var serial = GetSelectedDeviceSerial();
                if (string.IsNullOrWhiteSpace(serial))
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.NoAuthorizedDevice"));
                var apps = await Task.Run(delegate
                {
                    return _scrcpyService.ListApps(serial);
                });
                var selectedPackage = GetSelectedAppPackage();
                var selectedName = GetSelectedAppName(selectedPackage);

                _startAppBox.SelectedIndex = -1;
                _startAppBox.Items.Clear();
                AddNoStartAppItem();
                foreach (var app in apps) _startAppBox.Items.Add(app);
                AddRememberedAppItems();

                var selected = false;
                if (string.IsNullOrWhiteSpace(selectedPackage))
                {
                    _startAppBox.SelectedIndex = 0;
                    selected = true;
                }
                foreach (var app in apps)
                {
                    if (!string.Equals(
                        app.PackageName,
                        selectedPackage,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _startAppBox.SelectedItem = app;
                    selected = true;
                    break;
                }

                if (!selected)
                    SetSelectedAppPackage(selectedPackage, selectedName);
                SaveSelectedAppIdentity();
                _logService.Info(LocalizationService.Get(
                    "Log.Main.AppListDisplayed"));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppListLoadFailed"),
                    ex);
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Main.LoadAppsFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                _loadAppsButton.Text = LocalizationService.Get("Main.LoadApps");
                _loadAppsButton.Enabled = true;
            }
        }

        private void ApplyRunSettings(bool showMessage)
        {
            var bitRate = ((int)_bitRateBox.Value).ToString() + "M";
            ScrcpyService.ValidateAdditionalArguments(
                _additionalArgumentsBox.Text);
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings candidate)
            {
                var runSettings = GetDeviceRunSettings(
                    candidate,
                    _selectedDeviceIdentity);
                if (_selectedMode == 0)
                {
                    runSettings.VirtualDisplay.Width = (int)_widthBox.Value;
                    runSettings.VirtualDisplay.Height = (int)_heightBox.Value;
                    if (IsCustomResolutionSelected())
                    {
                        runSettings.VirtualDisplay.CustomWidth =
                            (int)_widthBox.Value;
                        runSettings.VirtualDisplay.CustomHeight =
                            (int)_heightBox.Value;
                    }
                    runSettings.VirtualDisplay.Dpi = (int)_dpiBox.Value;
                    runSettings.VirtualDisplay.ReuseExistingDisplay = true;
                    runSettings.Scrcpy.BitRate = bitRate;
                    runSettings.Scrcpy.MaxFps = GetSelectedMaxFps();
                    runSettings.Scrcpy.TurnScreenOff =
                        _turnScreenOffBox.Checked;
                    runSettings.Scrcpy.StayAwake = _stayAwakeBox.Checked;
                    runSettings.Scrcpy.UseHidKeyboard =
                        _useHidKeyboardBox.Checked;
                    runSettings.Scrcpy.UseHidMouse =
                        _useHidMouseBox.Checked;
                    runSettings.Scrcpy.ForceStopStartApp =
                        _forceStopAppBox.Checked;
                    var selectedPackage = GetSelectedAppPackage();
                    var selectedName = GetSelectedAppName(selectedPackage);
                    runSettings.Scrcpy.StartAppPackage = selectedPackage;
                    runSettings.Scrcpy.StartAppName = selectedName;
                    runSettings.Scrcpy.AdditionalArguments =
                        _additionalArgumentsBox.Text.Trim();
                }
                else
                {
                    var slot = GetSingleWindowSettings(
                        runSettings,
                        _selectedMode);
                    slot.Width = (int)_widthBox.Value;
                    slot.Height = (int)_heightBox.Value;
                    if (IsCustomResolutionSelected())
                    {
                        slot.CustomWidth = (int)_widthBox.Value;
                        slot.CustomHeight = (int)_heightBox.Value;
                    }
                    slot.Dpi = (int)_dpiBox.Value;
                    slot.BitRate = bitRate;
                    slot.MaxFps = GetSelectedMaxFps();
                    slot.TurnScreenOff = _turnScreenOffBox.Checked;
                    slot.StayAwake = _stayAwakeBox.Checked;
                    slot.UseHidKeyboard = _useHidKeyboardBox.Checked;
                    slot.UseHidMouse = _useHidMouseBox.Checked;
                    slot.ForceStopStartApp = _forceStopAppBox.Checked;
                    slot.FlexDisplay = _flexDisplayBox.Checked;
                    var selectedPackage = GetSelectedAppPackage();
                    var selectedName = GetSelectedAppName(selectedPackage);
                    slot.StartAppPackage = selectedPackage;
                    slot.StartAppName = selectedName;
                    slot.AdditionalArguments =
                        _additionalArgumentsBox.Text.Trim();
                }
            });
            _logService.Info(
                _selectedMode == 0
                    ? LocalizationService.Get(
                        "Log.Main.DexSettingsSaved")
                    : LocalizationService.Format(
                        "Log.Main.SingleWindowSettingsSaved",
                        _selectedMode));
            if (showMessage)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Get("Main.ApplyDeferred"),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private string GetSelectedAppPackage()
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            if (app != null) return app.PackageName ?? string.Empty;
            return string.Empty;
        }

        private void DpiBox_MinimumValueRejected(
            object sender,
            EventArgs e)
        {
            if (_exitInProgress || IsDisposed || Disposing) return;
            MessageBox.Show(
                this,
                LocalizationService.Format(
                    "Main.DpiMinimum",
                    (int)_dpiBox.Minimum),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ResolutionBox_MaximumValueRejected(
            object sender,
            EventArgs e)
        {
            if (_exitInProgress || IsDisposed || Disposing) return;
            var control = sender as ThemedNumberControl;
            var fieldName = ReferenceEquals(control, _heightBox)
                ? LocalizationService.Get("Main.Height")
                : LocalizationService.Get("Main.Width");
            MessageBox.Show(
                this,
                LocalizationService.Format(
                    "Main.ResolutionMaximum",
                    fieldName,
                    control == null ? 4096 : (int)control.Maximum),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private string GetSelectedAppName(string packageName)
        {
            var app = _startAppBox.SelectedItem as ScrcpyAppInfo;
            if (app != null && !string.IsNullOrWhiteSpace(app.PackageName))
                return app.Name ?? app.PackageName;

            if (_selectedMode == 0)
            {
                var scrcpy = GetSelectedDeviceRunSettings().Scrcpy;
                return string.Equals(
                    scrcpy.StartAppPackage,
                    packageName,
                    StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(scrcpy.StartAppName)
                    ? scrcpy.StartAppName
                    : packageName;
            }

            var slot = GetSingleWindowSettings(_selectedMode);
            return string.Equals(
                slot.StartAppPackage,
                packageName,
                StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(slot.StartAppName)
                ? slot.StartAppName
                : packageName;
        }

        private int GetSelectedMaxFps()
        {
            return _maxFpsBox.SelectedItem is int
                ? (int)_maxFpsBox.SelectedItem
                : 60;
        }

        private static int ParseBitRateNumber(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.EndsWith(
                "M",
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    0,
                    normalized.Length - 1);
            }

            int result;
            return int.TryParse(normalized, out result) && result > 0
                ? result
                : 20;
        }
        private sealed class ResolutionPreset
        {
            public ResolutionPreset(string text, int width, int height)
            {
                Text = text;
                Width = width;
                Height = height;
            }

            public string Text { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public override string ToString() { return Text; }
        }
    }
}

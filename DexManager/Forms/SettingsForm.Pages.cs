using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        private Control BuildGeneralPage()
        {
            var page = CreatePage();
            var appearance = CreateTable();
            _languageBox = CreateSelect();
            foreach (AppLanguage language in Enum.GetValues(typeof(AppLanguage)))
                _languageBox.Items.Add(new LanguageOption(language));
            AddRow(appearance, LocalizationService.Get("Settings.Language"), _languageBox);
            AddRow(
                appearance,
                string.Empty,
                CreateHint(LocalizationService.Get("Settings.LanguageRestart")));
            _themeBox = CreateSelect();
            foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
                _themeBox.Items.Add(new ThemeOption(theme));
            AddRow(appearance, LocalizationService.Get("Settings.Theme"), _themeBox);
            AddRow(
                appearance,
                string.Empty,
                CreateHint(LocalizationService.Get("Settings.ThemeRestart")));
            AddCard(page, LocalizationService.Get("Settings.GroupAppearance"), appearance);

            var startup = CreateTable();
            _startWithWindowsBox = AddCheck(startup, LocalizationService.Get("Settings.StartWithWindows"));
            _startMinimizedBox = AddCheck(startup, LocalizationService.Get("Settings.StartMinimized"));
            _autoStartDexBox = AddCheck(startup, LocalizationService.Get("Settings.AutoStartDex"));
            _connectedStartDelayBox = AddNumber(
                startup,
                LocalizationService.Get("Settings.StartDelay"),
                0,
                60);
            _autoHideBox = AddCheck(startup, LocalizationService.Get("Settings.AutoHide"));
            _autoHideSecondsBox = AddNumber(
                startup,
                LocalizationService.Get("Settings.HideDelay"),
                1,
                3600);
            _autoHideBox.CheckedChanged += delegate
            {
                _autoHideSecondsBox.Enabled = _autoHideBox.Checked;
            };
            _showConnectedDeviceInfoBox = AddCheck(
                startup,
                LocalizationService.Get("Settings.ShowConnectedDeviceInfo"));
            AddCard(page, LocalizationService.Get("Settings.GroupStartup"), startup);

            var shutdown = CreateTable();
            // Virtual display cleanup is now a fixed safety policy rather
            // than a user option. Keep the hidden value for config backward
            // compatibility while removing the misleading switch.
            _resetDisplayOnStopBox = new ThemedCheckBox
            {
                Checked = true,
                Visible = false
            };
            _disableStayAwakeBox = AddCheck(shutdown, LocalizationService.Get("Settings.DisableStayAwake"));
            AddCard(page, LocalizationService.Get("Settings.GroupShutdown"), shutdown);
            return page;
        }

        private Control BuildPathPage()
        {
            var page = CreatePage();
            var adbTable = CreateTable();

            _automaticAdbBox = CreateRadio(
                LocalizationService.Get("Settings.AdbAuto"));
            _manualAdbBox = CreateRadio(
                LocalizationService.Get("Settings.AdbManual"));
            _automaticAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            _manualAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            AddRow(adbTable, LocalizationService.Get("Settings.AdbMode"), _automaticAdbBox);
            AddRow(adbTable, string.Empty, _manualAdbBox);
            AddReadOnly(adbTable, LocalizationService.Get("Settings.CurrentOs"), WindowsVersionHelper.GetDisplayName());
            AddReadOnly(adbTable, LocalizationService.Get("Settings.CurrentAdb"), GetAdbDisplayName());
            AddReadOnly(adbTable, LocalizationService.Get("Settings.AdbVersion"), GetAdbVersionText());

            _manualAdbPanel = CreatePathPanel(out _manualAdbPathBox, true);
            AddRow(adbTable, LocalizationService.Get("Settings.ManualAdbPath"), _manualAdbPanel);
            AddCard(page, LocalizationService.Get("Settings.GroupAdb"), adbTable);

            var paths = CreateTable();
            _scrcpyPathBox = AddPath(paths, LocalizationService.Get("Settings.ScrcpyPath"), true);
            _screenshotFolderBox = AddPath(paths, LocalizationService.Get("Settings.ScreenshotFolder"), false);
            _deviceScreenshotFolderBox = AddDevicePath(
                paths,
                LocalizationService.Get("Settings.DeviceFolder"));
            _deviceScreenshotFolderBox.UseMiddleEllipsis = true;
            _pushCaptureBox = AddCheck(
                paths,
                LocalizationService.Get("Settings.PushCapture"));
            _managedFileTransferBox = AddCheck(
                paths,
                LocalizationService.Get(
                    "Settings.ManagedFileTransfer"));
            _fileTransferTargetFolderBox = AddDevicePath(
                paths,
                LocalizationService.Get(
                    "Settings.FileTransferTargetFolder"));
            _fileTransferTargetFolderBox.UseMiddleEllipsis = true;
            AddRow(
                paths,
                string.Empty,
                CreateHint(LocalizationService.Get(
                    "Settings.ManagedFileTransferHint")));
            _logFolderBox = AddPath(paths, LocalizationService.Get("Settings.LogFolder"), false);
            AddCard(page, LocalizationService.Get("Settings.GroupStorage"), paths);
            return page;
        }

        private Control BuildConnectionPage()
        {
            var page = CreatePage();
            var connection = CreateTable();

            _usbConnectionBox = CreateRadio(
                LocalizationService.Get("Settings.Usb"));
            _wirelessConnectionBox = CreateRadio(
                LocalizationService.Get("Settings.Wireless"));
            _usbConnectionBox.CheckedChanged += delegate
            {
                UpdateWirelessControls();
            };
            _wirelessConnectionBox.CheckedChanged += delegate
            {
                UpdateWirelessControls();
            };
            AddRow(connection, LocalizationService.Get("Settings.ConnectionMode"), _usbConnectionBox);
            AddRow(connection, string.Empty, _wirelessConnectionBox);

            _wirelessHostBox = AddText(connection, LocalizationService.Get("Settings.PhoneIp"));
            _wirelessPortBox = AddNumber(
                connection,
                LocalizationService.Get("Settings.ConnectPort"),
                1,
                65535);
            _wirelessAutoReconnectBox = AddCheck(
                connection,
                LocalizationService.Get("Settings.AutoReconnect"));

            var connectionButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 34,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            connectionButtons.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 33.333F));
            connectionButtons.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 33.334F));
            connectionButtons.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 33.333F));
            _wirelessPrepareButton = CreateActionButton(
                LocalizationService.Get("Settings.PrepareWireless"), 130);
            _wirelessPrepareButton.Click +=
                WirelessPrepareButton_Click;
            _wirelessConnectButton = CreateActionButton(
                LocalizationService.Get("Settings.ConnectWireless"), 130);
            _wirelessConnectButton.Click +=
                WirelessConnectButton_Click;
            _wirelessDisconnectButton = CreateActionButton(
                LocalizationService.Get("Settings.Disconnect"), 100);
            _wirelessDisconnectButton.Click +=
                WirelessDisconnectButton_Click;
            _wirelessPrepareButton.Dock = DockStyle.Fill;
            _wirelessPrepareButton.Margin = new Padding(0, 0, 5, 0);
            _wirelessConnectButton.Dock = DockStyle.Fill;
            _wirelessConnectButton.Margin = new Padding(3, 0, 3, 0);
            _wirelessDisconnectButton.Dock = DockStyle.Fill;
            _wirelessDisconnectButton.Margin = new Padding(5, 0, 0, 0);
            connectionButtons.Controls.Add(_wirelessPrepareButton, 0, 0);
            connectionButtons.Controls.Add(_wirelessConnectButton, 1, 0);
            connectionButtons.Controls.Add(_wirelessDisconnectButton, 2, 0);
            AddRow(connection, LocalizationService.Get("Settings.WirelessActions"), connectionButtons);

            _wirelessStatusLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(410, 0),
                ForeColor = _theme.TextSecondary,
                BackColor = _theme.CardBackground
            };
            AddRow(connection, LocalizationService.Get("Settings.CurrentStatus"), _wirelessStatusLabel);
            AddCard(page, LocalizationService.Get("Settings.GroupWireless"), connection);

            var pairing = CreateTable();
            AddRow(
                pairing,
                "Android 11+",
                new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(410, 0),
                    ForeColor = _theme.TextSecondary,
                    BackColor = _theme.CardBackground,
                    Text = LocalizationService.Get("Settings.PairGuide")
                });
            _pairingPortBox = AddNumber(
                pairing,
                LocalizationService.Get("Settings.PairingPort"),
                1,
                65535);
            _pairingCodeBox = AddText(pairing, LocalizationService.Get("Settings.PairingCode"));
            _pairingCodeBox.MaxLength = 6;
            _pairingCodeBox.UsePasswordMask = true;
            _pairButton = CreateActionButton(
                LocalizationService.Get("Settings.Pair"), 100);
            _pairButton.Click += PairButton_Click;
            var pairButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 34,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty
            };
            pairButtons.Controls.Add(_pairButton);
            AddRow(pairing, string.Empty, pairButtons);
            AddCard(page, LocalizationService.Get("Settings.GroupPairing"), pairing);
            return page;
        }

        private Control BuildKeyboardPage()
        {
            var page = CreatePage();
            var hotkeys = CreateTable();
            _captureHotkeyBox = AddHotkey(
                hotkeys,
                LocalizationService.Get("Settings.CaptureHotkey"));
            _exitHotkeyBox = AddHotkey(
                hotkeys,
                LocalizationService.Get("Settings.ExitHotkey"));
            AddRow(
                hotkeys,
                string.Empty,
                CreateHint(LocalizationService.Get(
                    "Settings.HotkeyCaptureGuide")));
            _lowLevelHotkeyBox = AddCheck(hotkeys, LocalizationService.Get("Settings.LowLevelHotkey"));
            _keyboardDiagnosticsBox = AddCheck(hotkeys, LocalizationService.Get("Settings.KeyDiagnostics"));
            AddCard(page, LocalizationService.Get("Settings.GroupHotkeys"), hotkeys);

            var correction = CreateTable();
            _keyInputModeBox = AddCombo<KeyInputMode>(
                correction,
                LocalizationService.Get("Settings.KeyInputMode"));
            _convertHangulBox = AddCheck(correction, LocalizationService.Get("Settings.HangulCorrection"));
            _rightWindowsBox = AddCheck(correction, LocalizationService.Get("Settings.RightWindows"));
            _convertEnterBox = AddCheck(correction, LocalizationService.Get("Settings.EnterConversion"));
            _ignoreShiftSpaceBox = AddCheck(correction, LocalizationService.Get("Settings.IgnoreShiftSpace"));
            AddCard(page, LocalizationService.Get("Settings.GroupInput"), correction);
            return page;
        }

        private Control BuildDiagnosticsPage()
        {
            var page = CreatePage();
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                Width = 620,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(700, 0),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 0, 0, 18),
                Text = LocalizationService.Get("Settings.DiagnosticsGuide")
            });
            var logButton = CreateActionButton(
                LocalizationService.Get("Settings.OpenLogs"), 220);
            logButton.Margin = new Padding(0, 0, 0, 10);
            logButton.Click += delegate
            {
                if (_showLogs != null) _showLogs();
            };
            var environmentButton = CreateActionButton(
                LocalizationService.Get("Settings.OpenEnvironment"), 220);
            environmentButton.Click += delegate
            {
                if (_showEnvironmentCheck != null)
                    _showEnvironmentCheck();
            };
            panel.Controls.Add(logButton);
            panel.Controls.Add(environmentButton);
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 24, 0, 10),
                Text = LocalizationService.Get(
                    "Settings.AdvancedOptionsGuide")
            });
            _advancedOptionsButton = CreateActionButton(
                LocalizationService.Get("Settings.ShowAdvancedOptions"),
                220);
            _advancedOptionsButton.Click += delegate
            {
                _advancedOptionsCard.Visible =
                    !_advancedOptionsCard.Visible;
                _advancedOptionsButton.Text = LocalizationService.Get(
                    _advancedOptionsCard.Visible
                        ? "Settings.HideAdvancedOptions"
                        : "Settings.ShowAdvancedOptions");
                page.PerformLayout();
            };
            panel.Controls.Add(_advancedOptionsButton);
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 24, 0, 10),
                Text = LocalizationService.Get(
                    "Settings.ResetDefaultsGuide")
            });
            var resetButton = CreateActionButton(
                LocalizationService.Get("Settings.ResetDefaults"),
                220);
            resetButton.Click += ResetDefaultsButton_Click;
            panel.Controls.Add(resetButton);
            AddCard(page, LocalizationService.Get("Settings.Diagnostics"), panel);

            var displayCleanup = new FlowLayoutPanel
            {
                AutoSize = true,
                Width = 620,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            displayCleanup.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 0, 0, 12),
                Text = LocalizationService.Get(
                    "Settings.DisplayCleanupGuide")
            });
            _displayCleanupPermissionButton = CreateActionButton(
                LocalizationService.Get(
                    "Settings.GrantDisplayCleanupPermission"),
                220);
            _displayCleanupPermissionButton.Enabled = false;
            _displayCleanupPermissionButton.Click +=
                DisplayCleanupPermissionButton_Click;
            displayCleanup.Controls.Add(_displayCleanupPermissionButton);
            _displayCleanupStatusLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 10, 0, 0),
                Text = LocalizationService.Get(
                    "Settings.DisplayCleanupChecking")
            };
            displayCleanup.Controls.Add(_displayCleanupStatusLabel);
            AddCard(
                page,
                LocalizationService.Get(
                    "Settings.GroupDisplayCleanup"),
                displayCleanup);

            var advanced = CreateTable();
            _wakeUpModeBox = AddCombo<ScrcpyWakeUpMode>(
                advanced,
                LocalizationService.Get("Settings.WakeUpMode"));
            _deviceMonitorIntervalBox = AddNumber(
                advanced,
                LocalizationService.Get("Settings.DeviceInterval"),
                1,
                60);
            _disconnectMonitorIntervalBox = AddNumber(
                advanced,
                LocalizationService.Get("Settings.DisconnectInterval"),
                1,
                60);
            _virtualDisplayTimeoutBox = AddNumber(
                advanced,
                LocalizationService.Get("Settings.VirtualDisplayTimeout"),
                1,
                60);
            _adbWakeUpDelayBox = AddNumber(
                advanced,
                LocalizationService.Get("Settings.WakeDelay"),
                0,
                60);
            _processTimeoutBox = AddNumber(
                advanced,
                LocalizationService.Get("Settings.ProcessTimeout"),
                1,
                120);
            _captureWaitSecondsBox = AddNumber(
                advanced,
                LocalizationService.Get("Settings.CaptureDelay"),
                1,
                60);
            _advancedOptionsCard = AddCard(
                page,
                LocalizationService.Get("Settings.AdvancedOptions"),
                advanced);
            _advancedOptionsCard.Visible = false;
            return page;
        }

    }
}

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
        private void UpdateWirelessControls()
        {
            var enabled = _wirelessConnectionBox != null &&
                _wirelessConnectionBox.Checked;
            if (_wirelessHostBox != null)
                _wirelessHostBox.Enabled = enabled;
            if (_wirelessPortBox != null)
                _wirelessPortBox.Enabled = enabled;
            if (_wirelessAutoReconnectBox != null)
                _wirelessAutoReconnectBox.Enabled = enabled;
            if (_wirelessPrepareButton != null)
                _wirelessPrepareButton.Enabled = enabled;
            if (_wirelessConnectButton != null)
                _wirelessConnectButton.Enabled = enabled;
            if (_wirelessDisconnectButton != null)
                _wirelessDisconnectButton.Enabled = enabled;
            if (_pairingPortBox != null)
                _pairingPortBox.Enabled = enabled;
            if (_pairingCodeBox != null)
                _pairingCodeBox.Enabled = enabled;
            if (_pairButton != null)
                _pairButton.Enabled = enabled;
        }

        private async void WirelessPrepareButton_Click(
            object sender,
            EventArgs e)
        {
            var host = _wirelessHostBox.Text;
            var port = (int)_wirelessPortBox.Value;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.EnableFromUsb(
                    host,
                    port);
            });
            if (IsDisposed) return;
            _wirelessHostBox.Text =
                _settings.Connection.WirelessHost ?? string.Empty;
        }

        private async void WirelessConnectButton_Click(
            object sender,
            EventArgs e)
        {
            var host = _wirelessHostBox.Text;
            var port = (int)_wirelessPortBox.Value;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Connect(
                    host,
                    port);
            });
        }

        private async void WirelessDisconnectButton_Click(
            object sender,
            EventArgs e)
        {
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Disconnect();
            });
            if (IsDisposed) return;
            _usbConnectionBox.Checked =
                !_wirelessAdbService.IsWirelessMode;
        }

        private async void PairButton_Click(
            object sender,
            EventArgs e)
        {
            var host = _wirelessHostBox.Text;
            var port = (int)_pairingPortBox.Value;
            var pairingCode = _pairingCodeBox.Text.Trim();
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Pair(
                    host,
                    port,
                    pairingCode);
            });
            if (IsDisposed) return;
            _pairingCodeBox.Clear();
        }

        private async Task RunWirelessOperationAsync(
            Func<WirelessConnectionResult> operation)
        {
            SetWirelessButtonsEnabled(false);
            UseWaitCursor = true;
            try
            {
                var result = await Task.Run(operation);
                if (IsDisposed) return;
                _wirelessStatusLabel.Text = result.Message +
                    (string.IsNullOrWhiteSpace(result.Endpoint)
                        ? string.Empty
                        : " (" + result.Endpoint + ")");
                _wirelessStatusLabel.ForeColor = result.Success
                    ? Color.DarkGreen
                    : Color.Firebrick;
                if (result.Success)
                    _wirelessConnectionBox.Checked =
                        _wirelessAdbService.IsWirelessMode;
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _wirelessStatusLabel.Text =
                        LocalizationService.Format(
                            "Settings.WirelessOperationFailed",
                            ex.Message);
                    _wirelessStatusLabel.ForeColor = Color.Firebrick;
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    UseWaitCursor = false;
                    UpdateWirelessControls();
                }
            }
        }

        private void SetWirelessButtonsEnabled(bool enabled)
        {
            _wirelessPrepareButton.Enabled = enabled;
            _wirelessConnectButton.Enabled = enabled;
            _wirelessDisconnectButton.Enabled = enabled;
            _pairButton.Enabled = enabled;
        }

        private void UpdateWirelessStatus()
        {
            var target = _wirelessAdbService.SelectedSerial;
            if (string.IsNullOrWhiteSpace(target))
            {
                _wirelessStatusLabel.Text =
                    _settings.Connection.Mode == AdbConnectionMode.Wireless
                        ? LocalizationService.Get(
                            "Settings.WirelessWaiting")
                        : LocalizationService.Get(
                            "Settings.UsbWaiting");
                return;
            }
            _wirelessStatusLabel.Text =
                LocalizationService.Format(
                    AdbService.IsTcpIpSerial(target)
                        ? "Settings.WirelessTarget"
                        : "Settings.UsbTarget",
                    target);
        }

        private string GetAdbVersionText()
        {
            try
            {
                var result = _adbService.GetVersion();
                if (!result.IsSuccess)
                    return LocalizationService.Format(
                        "Settings.CheckFailed",
                        result.StandardError);
                return AdbVersionParser.GetDisplayVersion(
                    result.StandardOutput,
                    LocalizationService.Get(
                        "Settings.NoVersionOutput"));
            }
            catch (Exception ex)
            {
                return LocalizationService.Format(
                    "Settings.CheckFailed",
                    ex.Message);
            }
        }

        private string GetAdbDisplayName()
        {
            if (_settings.Paths.AdbSelectionMode == AdbSelectionMode.Manual)
                return LocalizationService.Get("Settings.AdbTypeManual");

            var selectedPath = NormalizePath(_adbService.AdbPath);
            if (PathsEqual(
                selectedPath,
                ResolveConfiguredPath(_settings.Paths.Win7AdbPath)))
            {
                return LocalizationService.Get("Settings.AdbTypeLegacy");
            }
            var scrcpyPath = ResolveConfiguredPath(
                _settings.Paths.ScrcpyPath);
            var scrcpyAdbPath = string.IsNullOrWhiteSpace(scrcpyPath)
                ? string.Empty
                : Path.Combine(
                    Path.GetDirectoryName(scrcpyPath) ?? string.Empty,
                    "adb.exe");
            if (PathsEqual(selectedPath, scrcpyAdbPath))
                return LocalizationService.Get("Settings.AdbTypeScrcpy");

            return LocalizationService.Get("Settings.AdbTypeExternal");
        }

        private string ResolveConfiguredPath(string configuredPath)
        {
            try
            {
                return NormalizePath(
                    _settingsService.ResolvePath(configuredPath));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                !string.IsNullOrWhiteSpace(right) &&
                string.Equals(
                    NormalizePath(left),
                    NormalizePath(right),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}

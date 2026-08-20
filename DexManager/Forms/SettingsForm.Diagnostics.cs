using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        private Control BuildSelectedDeviceDiagnosticsPanel()
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                Width = 620,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = Padding.Empty,
                BackColor = _theme.CardBackground
            };
            _deviceDiagnosticsStatusLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                MinimumSize = new Size(610, 58),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 0, 0, 12),
                Text = LocalizationService.Get(
                    "Settings.DeviceDiagnosticsWaiting")
            };
            _deviceDiagnosticsRefreshButton = CreateActionButton(
                LocalizationService.Get("Settings.RefreshDeviceDiagnostics"),
                220);
            _deviceDiagnosticsRefreshButton.Click += delegate
            {
                RefreshDeviceDiagnosticsAsync();
            };
            panel.Controls.Add(_deviceDiagnosticsStatusLabel);
            panel.Controls.Add(_deviceDiagnosticsRefreshButton);
            return panel;
        }

        private async void RefreshDeviceDiagnosticsAsync()
        {
            if (IsDisposed || Disposing ||
                _deviceDiagnosticsStatusLabel == null ||
                _deviceDiagnosticsRefreshButton == null)
            {
                return;
            }

            var serial = _getSelectedDeviceSerial();
            var device = FindSelectedPhysicalDevice(serial);
            var generation = ++_deviceDiagnosticsGeneration;
            _deviceDiagnosticsRefreshButton.Enabled = false;
            _deviceDiagnosticsStatusLabel.Text = LocalizationService.Get(
                "Settings.DeviceDiagnosticsChecking");

            DeviceVersionDiagnostic diagnostic;
            try
            {
                diagnostic = await Task.Run(() =>
                    _deviceVersionDiagnosticService.Inspect(serial, device));
            }
            catch (Exception ex)
            {
                diagnostic = new DeviceVersionDiagnostic
                {
                    Serial = serial,
                    DisplayName = device == null
                        ? string.Empty
                        : device.DisplayName,
                    ErrorDetail = ex.Message,
                    QuerySucceeded = false
                };
            }

            if (IsDisposed || Disposing ||
                generation != _deviceDiagnosticsGeneration)
            {
                return;
            }
            _lastDeviceDiagnostic = diagnostic;
            _deviceDiagnosticsRefreshButton.Enabled = true;
            _deviceDiagnosticsStatusLabel.Text =
                FormatDeviceDiagnostic(diagnostic);
        }

        private async void DiagnosticReportButton_Click(
            object sender,
            EventArgs e)
        {
            if (_diagnosticReportButton == null ||
                !_diagnosticReportButton.Enabled)
            {
                return;
            }

            _diagnosticReportButton.Enabled = false;
            try
            {
                var serial = _getSelectedDeviceSerial();
                var identity = _getSelectedDeviceIdentity();
                var devices = _getDeviceSnapshot();
                var device = FindSelectedPhysicalDevice(serial);
                var diagnostic = _lastDeviceDiagnostic;
                if (diagnostic == null || !string.Equals(
                    diagnostic.Serial,
                    serial,
                    StringComparison.OrdinalIgnoreCase))
                {
                    diagnostic = await Task.Run(() =>
                        _deviceVersionDiagnosticService.Inspect(
                            serial,
                            device));
                    _lastDeviceDiagnostic = diagnostic;
                }

                var companion = await Task.Run(() =>
                    _displayCleanupPermissionService.Inspect(serial));
                var adbVersionResult = await Task.Run(() =>
                    _adbService.GetVersion());
                var adbVersion = adbVersionResult == null
                    ? string.Empty
                    : adbVersionResult.StandardOutput;
                var report = _diagnosticReportService.CreateReport(
                    Application.ProductVersion,
                    _adbService.AdbPath,
                    adbVersion,
                    _settings.Paths.ScrcpyPath,
                    identity,
                    devices,
                    _getRuntimeSnapshot(),
                    diagnostic,
                    companion,
                    _logService.GetSessionEntries());

                using (var dialog = new SaveFileDialog
                {
                    AddExtension = true,
                    DefaultExt = "txt",
                    FileName = "DX_Manager_Diagnostic_" +
                        DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt",
                    Filter = LocalizationService.Get(
                        "Settings.DiagnosticReportFilter")
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    File.WriteAllText(
                        dialog.FileName,
                        report,
                        new UTF8Encoding(true));
                    MessageBox.Show(
                        this,
                        LocalizationService.Get(
                            "Settings.DiagnosticReportSaved"),
                        LocalizationService.Get("Settings.Diagnostics"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    OpenReportFolder(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Settings.DiagnosticReportFailed",
                        ex.Message),
                    LocalizationService.Get("Settings.Diagnostics"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed && _diagnosticReportButton != null)
                    _diagnosticReportButton.Enabled = true;
            }
        }

        private PhysicalDeviceInfo FindSelectedPhysicalDevice(string serial)
        {
            var snapshot = _getDeviceSnapshot();
            if (snapshot == null) return null;
            var identity = _getSelectedDeviceIdentity();
            var device = snapshot.FindByIdentity(identity);
            return device ?? snapshot.FindByTransportSerial(serial);
        }

        private static void OpenReportFolder(string reportPath)
        {
            try
            {
                Process.Start(
                    "explorer.exe",
                    "/select,\"" + Path.GetFullPath(reportPath) + "\"");
            }
            catch
            {
                // Saving the report is successful even when Explorer cannot
                // be opened by a restricted Windows environment.
            }
        }

        private static string FormatDeviceDiagnostic(
            DeviceVersionDiagnostic diagnostic)
        {
            if (diagnostic == null || !diagnostic.QuerySucceeded)
            {
                return LocalizationService.Format(
                    "Settings.DeviceDiagnosticsFailed",
                    diagnostic == null ||
                    string.IsNullOrWhiteSpace(diagnostic.ErrorDetail)
                        ? LocalizationService.Get("Common.Unknown")
                        : diagnostic.ErrorDetail);
            }

            var name = string.IsNullOrWhiteSpace(diagnostic.DisplayName)
                ? diagnostic.Model
                : diagnostic.DisplayName;
            var android = string.IsNullOrWhiteSpace(
                diagnostic.AndroidVersion)
                ? LocalizationService.Get("Common.Unknown")
                : diagnostic.AndroidVersion;
            if (diagnostic.AndroidSdk > 0)
                android += " (SDK " + diagnostic.AndroidSdk + ")";
            var oneUi = string.IsNullOrWhiteSpace(diagnostic.OneUiVersion)
                ? LocalizationService.Get("Common.Unknown")
                : diagnostic.OneUiVersion;
            var patch = string.IsNullOrWhiteSpace(diagnostic.SecurityPatch)
                ? LocalizationService.Get("Common.Unknown")
                : diagnostic.SecurityPatch;
            return LocalizationService.Format(
                "Settings.DeviceDiagnosticsSummary",
                name,
                LocalizationService.Get(
                    "Settings.Transport." + diagnostic.TransportKind),
                android,
                oneUi,
                patch,
                LocalizationService.Get(
                    "Settings.Compatibility." +
                    diagnostic.Compatibility));
        }
    }
}

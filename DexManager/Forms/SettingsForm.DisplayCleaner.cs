using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        private async void RefreshDisplayCleanupStatusAsync()
        {
            if (_displayCleanupOperationRunning ||
                _displayCleanupPermissionButton == null ||
                _companionInstallButton == null ||
                _companionUninstallButton == null ||
                _displayCleanupStatusLabel == null)
            {
                return;
            }

            SetCompanionBusy("Settings.DisplayCleanupChecking");
            try
            {
                var serial = _wirelessAdbService.SelectedSerial;
                var status = await Task.Run(
                    () => _displayCleanupPermissionService.Inspect(serial));
                var bundled = await Task.Run(
                    () => _displayCleanupPermissionService
                        .InspectBundledApk());
                if (IsDisposed) return;
                _displayCleanupStatus = status;
                _bundledCompanionStatus = bundled;
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _displayCleanupStatus = new DisplayCleanupPermissionStatus
                {
                    State = DisplayCleanupPermissionState.Error,
                    Detail = ex.Message
                };
            }
            finally
            {
                _displayCleanupOperationRunning = false;
            }
            if (!IsDisposed)
                ApplyDisplayCleanupStatus(_displayCleanupStatus);
        }

        private async void CompanionInstallButton_Click(
            object sender,
            EventArgs e)
        {
            if (_displayCleanupOperationRunning ||
                _displayCleanupStatus == null ||
                _bundledCompanionStatus == null ||
                _bundledCompanionStatus.State !=
                    BundledCompanionState.Ready ||
                string.IsNullOrWhiteSpace(_displayCleanupStatus.Serial))
            {
                return;
            }

            var serial = _displayCleanupStatus.Serial;
            var confirmKey = _displayCleanupStatus.PackageInstalled
                ? "Settings.CompanionUpdateConfirm"
                : "Settings.CompanionInstallConfirm";
            var answer = MessageBox.Show(
                this,
                LocalizationService.Get(confirmKey),
                LocalizationService.Get("Settings.GroupDisplayCleanup"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            SetCompanionBusy("Settings.CompanionInstalling");
            try
            {
                var status = await Task.Run(
                    () => _displayCleanupPermissionService
                        .InstallAndGrant(serial));
                var bundled = await Task.Run(
                    () => _displayCleanupPermissionService
                        .InspectBundledApk());
                if (IsDisposed) return;
                _displayCleanupStatus = status;
                _bundledCompanionStatus = bundled;
                _displayCleanupOperationRunning = false;
                ApplyDisplayCleanupStatus(status);
                if (status.State ==
                        DisplayCleanupPermissionState.Granted &&
                    status.VersionCode ==
                        DisplayCleanupPermissionService.BundledVersionCode)
                {
                    if (_companionInstalled != null)
                        _companionInstalled(serial);
                    MessageBox.Show(
                        this,
                        LocalizationService.Get(
                            "Settings.CompanionInstallSucceeded"),
                        LocalizationService.Get(
                            "Settings.GroupDisplayCleanup"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _displayCleanupStatus = new DisplayCleanupPermissionStatus
                {
                    State = DisplayCleanupPermissionState.Error,
                    Detail = ex.Message,
                    Serial = serial
                };
                _displayCleanupOperationRunning = false;
                ApplyDisplayCleanupStatus(_displayCleanupStatus);
            }
            finally
            {
                _displayCleanupOperationRunning = false;
            }
        }

        private async void CompanionUninstallButton_Click(
            object sender,
            EventArgs e)
        {
            if (_displayCleanupOperationRunning ||
                _displayCleanupStatus == null ||
                !_displayCleanupStatus.PackageInstalled ||
                string.IsNullOrWhiteSpace(_displayCleanupStatus.Serial))
            {
                return;
            }

            var serial = _displayCleanupStatus.Serial;
            var answer = MessageBox.Show(
                this,
                LocalizationService.Get(
                    "Settings.CompanionUninstallConfirm"),
                LocalizationService.Get("Settings.GroupDisplayCleanup"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            SetCompanionBusy("Settings.CompanionUninstalling");
            try
            {
                if (_detachPhoneTransfer != null)
                    await _detachPhoneTransfer(serial);
                var status = await Task.Run(
                    () => _displayCleanupPermissionService
                        .Uninstall(serial));
                if (IsDisposed) return;
                _displayCleanupStatus = status;
                _displayCleanupOperationRunning = false;
                ApplyDisplayCleanupStatus(status);
                if (status.State ==
                    DisplayCleanupPermissionState.NotInstalled)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.Get(
                            "Settings.CompanionUninstallSucceeded"),
                        LocalizationService.Get(
                            "Settings.GroupDisplayCleanup"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else if (status.PackageInstalled &&
                    _companionInstalled != null)
                {
                    _companionInstalled(serial);
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _displayCleanupStatus = new DisplayCleanupPermissionStatus
                {
                    State = DisplayCleanupPermissionState.Error,
                    Detail = ex.Message,
                    Serial = serial
                };
                _displayCleanupOperationRunning = false;
                ApplyDisplayCleanupStatus(_displayCleanupStatus);
            }
            finally
            {
                _displayCleanupOperationRunning = false;
            }
        }

        private async void DisplayCleanupPermissionButton_Click(
            object sender,
            EventArgs e)
        {
            if (_displayCleanupOperationRunning ||
                _displayCleanupStatus == null ||
                _displayCleanupStatus.State !=
                    DisplayCleanupPermissionState.Ready)
            {
                return;
            }

            var answer = MessageBox.Show(
                this,
                LocalizationService.Get(
                    "Settings.DisplayCleanupGrantConfirm"),
                LocalizationService.Get(
                    "Settings.GroupDisplayCleanup"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            SetCompanionBusy("Settings.DisplayCleanupGranting");
            try
            {
                var previous = _displayCleanupStatus;
                var status = await Task.Run(
                    () => _displayCleanupPermissionService.Grant(previous));
                if (IsDisposed) return;
                _displayCleanupStatus = status;
                _displayCleanupOperationRunning = false;
                ApplyDisplayCleanupStatus(status);
                if (status.State ==
                    DisplayCleanupPermissionState.Granted)
                {
                    if (_companionInstalled != null)
                        _companionInstalled(status.Serial);
                    MessageBox.Show(
                        this,
                        LocalizationService.Get(
                            "Settings.DisplayCleanupGrantSucceeded"),
                        LocalizationService.Get(
                            "Settings.GroupDisplayCleanup"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _displayCleanupStatus = new DisplayCleanupPermissionStatus
                {
                    State = DisplayCleanupPermissionState.Error,
                    Detail = ex.Message
                };
                _displayCleanupOperationRunning = false;
                ApplyDisplayCleanupStatus(_displayCleanupStatus);
            }
            finally
            {
                _displayCleanupOperationRunning = false;
            }
        }

        private void SetCompanionBusy(string statusKey)
        {
            _displayCleanupOperationRunning = true;
            _companionInstallButton.Enabled = false;
            _displayCleanupPermissionButton.Enabled = false;
            _companionUninstallButton.Enabled = false;
            _displayCleanupStatusLabel.ForeColor = _theme.TextTertiary;
            _displayCleanupStatusLabel.Text =
                LocalizationService.Get(statusKey);
        }

        private void ApplyDisplayCleanupStatus(
            DisplayCleanupPermissionStatus status)
        {
            if (status == null) return;
            string key;
            switch (status.State)
            {
                case DisplayCleanupPermissionState.NoDevice:
                    key = "Settings.DisplayCleanupNoDevice";
                    break;
                case DisplayCleanupPermissionState.NotInstalled:
                    key = "Settings.DisplayCleanupNotInstalled";
                    break;
                case DisplayCleanupPermissionState.VerificationFailed:
                    key = "Settings.DisplayCleanupVerificationFailed";
                    break;
                case DisplayCleanupPermissionState.Ready:
                    key = "Settings.DisplayCleanupReady";
                    break;
                case DisplayCleanupPermissionState.Granted:
                    key = "Settings.DisplayCleanupGranted";
                    break;
                default:
                    key = "Settings.DisplayCleanupFailed";
                    break;
            }

            var text = LocalizationService.Get(key);
            if (status.PackageInstalled)
            {
                text += Environment.NewLine + LocalizationService.Format(
                    "Settings.CompanionInstalledVersion",
                    status.VersionCode);
            }
            if (_bundledCompanionStatus != null)
            {
                switch (_bundledCompanionStatus.State)
                {
                    case BundledCompanionState.Ready:
                        text += Environment.NewLine +
                            LocalizationService.Format(
                                "Settings.CompanionBundledVersion",
                                _bundledCompanionStatus.VersionName);
                        break;
                    case BundledCompanionState.Missing:
                        text += Environment.NewLine +
                            LocalizationService.Get(
                                "Settings.CompanionBundledMissing");
                        break;
                    case BundledCompanionState.VerificationFailed:
                        text += Environment.NewLine +
                            LocalizationService.Get(
                                "Settings.CompanionBundledInvalid");
                        break;
                }
            }
            if ((status.State ==
                    DisplayCleanupPermissionState.VerificationFailed ||
                status.State == DisplayCleanupPermissionState.Error) &&
                !string.IsNullOrWhiteSpace(status.Detail))
            {
                text += Environment.NewLine + status.Detail;
            }
            if (_bundledCompanionStatus != null &&
                _bundledCompanionStatus.State ==
                    BundledCompanionState.VerificationFailed &&
                !string.IsNullOrWhiteSpace(
                    _bundledCompanionStatus.Detail))
            {
                text += Environment.NewLine +
                    _bundledCompanionStatus.Detail;
            }

            var hasError = status.State ==
                    DisplayCleanupPermissionState.VerificationFailed ||
                status.State == DisplayCleanupPermissionState.Error ||
                (_bundledCompanionStatus != null &&
                 _bundledCompanionStatus.State ==
                    BundledCompanionState.VerificationFailed);
            _displayCleanupStatusLabel.Text = text;
            _displayCleanupStatusLabel.ForeColor = hasError
                ? Color.Firebrick
                : _theme.TextTertiary;

            _displayCleanupPermissionButton.Text = LocalizationService.Get(
                status.State == DisplayCleanupPermissionState.Granted
                    ? "Settings.DisplayCleanupPermissionGrantedButton"
                    : "Settings.GrantDisplayCleanupPermission");
            _displayCleanupPermissionButton.Enabled =
                status.State == DisplayCleanupPermissionState.Ready &&
                !_displayCleanupOperationRunning;

            var bundledReady = _bundledCompanionStatus != null &&
                _bundledCompanionStatus.State ==
                    BundledCompanionState.Ready;
            var newerInstalled = status.PackageInstalled &&
                status.VersionCode >
                    DisplayCleanupPermissionService.BundledVersionCode;
            string installKey;
            if (newerInstalled)
                installKey = "Settings.CompanionNewerInstalled";
            else if (!status.PackageInstalled)
                installKey = "Settings.InstallCompanion";
            else if (status.VersionCode <
                DisplayCleanupPermissionService.BundledVersionCode)
                installKey = "Settings.UpdateCompanion";
            else
                installKey = "Settings.ReinstallCompanion";
            _companionInstallButton.Text =
                LocalizationService.Get(installKey);
            _companionInstallButton.Enabled = bundledReady &&
                !newerInstalled &&
                (status.State == DisplayCleanupPermissionState.NotInstalled ||
                 status.State == DisplayCleanupPermissionState.Ready ||
                 status.State == DisplayCleanupPermissionState.Granted) &&
                !_displayCleanupOperationRunning;
            _companionUninstallButton.Enabled =
                status.PackageInstalled &&
                !_displayCleanupOperationRunning;
        }
    }
}

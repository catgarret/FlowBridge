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
        private async void RefreshDisplayCleanupStatusAsync()
        {
            if (_displayCleanupOperationRunning ||
                _displayCleanupPermissionButton == null ||
                _displayCleanupStatusLabel == null)
            {
                return;
            }

            _displayCleanupOperationRunning = true;
            _displayCleanupPermissionButton.Enabled = false;
            _displayCleanupStatusLabel.ForeColor = _theme.TextTertiary;
            _displayCleanupStatusLabel.Text = LocalizationService.Get(
                "Settings.DisplayCleanupChecking");
            try
            {
                var status = await Task.Run(
                    () => _displayCleanupPermissionService.Inspect());
                if (IsDisposed) return;
                _displayCleanupOperationRunning = false;
                _displayCleanupStatus = status;
                ApplyDisplayCleanupStatus(status);
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _displayCleanupStatus = new DisplayCleanupPermissionStatus
                {
                    State = DisplayCleanupPermissionState.Error,
                    Detail = ex.Message
                };
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

            _displayCleanupOperationRunning = true;
            _displayCleanupPermissionButton.Enabled = false;
            _displayCleanupStatusLabel.ForeColor = _theme.TextTertiary;
            _displayCleanupStatusLabel.Text = LocalizationService.Get(
                "Settings.DisplayCleanupGranting");
            try
            {
                var previous = _displayCleanupStatus;
                var status = await Task.Run(
                    () => _displayCleanupPermissionService.Grant(previous));
                if (IsDisposed) return;
                _displayCleanupOperationRunning = false;
                _displayCleanupStatus = status;
                ApplyDisplayCleanupStatus(status);
                if (status.State ==
                    DisplayCleanupPermissionState.Granted)
                {
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
                ApplyDisplayCleanupStatus(_displayCleanupStatus);
            }
            finally
            {
                _displayCleanupOperationRunning = false;
            }
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
            if ((status.State ==
                    DisplayCleanupPermissionState.VerificationFailed ||
                status.State == DisplayCleanupPermissionState.Error) &&
                !string.IsNullOrWhiteSpace(status.Detail))
            {
                text += Environment.NewLine + status.Detail;
            }
            _displayCleanupStatusLabel.Text = text;
            _displayCleanupStatusLabel.ForeColor =
                status.State ==
                        DisplayCleanupPermissionState.VerificationFailed ||
                    status.State == DisplayCleanupPermissionState.Error
                    ? Color.Firebrick
                    : _theme.TextTertiary;
            _displayCleanupPermissionButton.Text = LocalizationService.Get(
                status.State == DisplayCleanupPermissionState.Granted
                    ? "Settings.DisplayCleanupPermissionGrantedButton"
                    : "Settings.GrantDisplayCleanupPermission");
            _displayCleanupPermissionButton.Enabled =
                status.State == DisplayCleanupPermissionState.Ready &&
                !_displayCleanupOperationRunning;
        }
    }
}

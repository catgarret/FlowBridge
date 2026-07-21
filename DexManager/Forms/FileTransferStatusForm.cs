using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class FileTransferStatusForm : Form
    {
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int FollowIntervalMs = 250;
        private const int AutoHideMilliseconds = 4000;
        private readonly FileTransferCoordinator _coordinator;
        private readonly Label _statusLabel;
        private readonly Label _fileLabel;
        private readonly Label _detailLabel;
        private readonly Label _countLabel;
        private readonly ProgressBar _progressBar;
        private readonly ThemedButton _cancelButton;
        private readonly Timer _followTimer;
        private FileTransferProgress _progress;
        private DateTime _hideAtUtc;

        public FileTransferStatusForm(
            FileTransferCoordinator coordinator,
            AppTheme theme)
        {
            _coordinator = coordinator ??
                throw new ArgumentNullException("coordinator");
            var palette = ThemeColors.Use(theme);
            Text = LocalizationService.Get("FileTransfer.WindowTitle");
            Icon = AppIconProvider.Current;
            Font = UiFonts.Create(9.5F);
            BackColor = palette.WindowBackground;
            ForeColor = palette.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(366, 178);

            _statusLabel = new Label
            {
                AutoSize = false,
                Font = UiFonts.Create(12F, FontStyle.Bold),
                ForeColor = palette.TextPrimary,
                Location = new Point(18, 15),
                Size = new Size(330, 27),
                Text = LocalizationService.Get("FileTransfer.Preparing")
            };
            _fileLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextSecondary,
                Location = new Point(18, 47),
                Size = new Size(330, 22)
            };
            _detailLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextTertiary,
                Location = new Point(18, 72),
                Size = new Size(330, 20)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(18, 99),
                Size = new Size(330, 12),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 25
            };
            _countLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextTertiary,
                Location = new Point(18, 126),
                Size = new Size(236, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cancelButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Cancel"),
                Primary = false,
                Location = new Point(264, 126),
                Size = new Size(84, 32),
                TabStop = false
            };
            _cancelButton.Click += CancelButton_Click;

            Controls.Add(_statusLabel);
            Controls.Add(_fileLabel);
            Controls.Add(_detailLabel);
            Controls.Add(_progressBar);
            Controls.Add(_countLabel);
            Controls.Add(_cancelButton);

            _followTimer = new Timer { Interval = FollowIntervalMs };
            _followTimer.Tick += FollowTimer_Tick;
            _followTimer.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
                return parameters;
            }
        }

        public void UpdateProgress(FileTransferProgress progress)
        {
            if (progress == null || IsDisposed) return;
            if (_progress != null &&
                IsInProgress(_progress.Stage) &&
                progress.Stage == FileTransferStage.Queued &&
                !string.Equals(
                    _progress.RequestId,
                    progress.RequestId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _countLabel.Text = LocalizationService.Format(
                    "FileTransfer.Counts",
                    _progress.CompletedCount,
                    _progress.FailedCount,
                    progress.QueuedCount);
                return;
            }
            _progress = progress;
            _hideAtUtc = DateTime.MinValue;

            _fileLabel.Text = progress.FileName;
            _detailLabel.Text = FormatBytes(progress.FileSize);
            _countLabel.Text = LocalizationService.Format(
                "FileTransfer.Counts",
                progress.CompletedCount,
                progress.FailedCount,
                progress.QueuedCount);

            if (progress.Percent >= 0)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = Math.Max(
                    _progressBar.Minimum,
                    Math.Min(_progressBar.Maximum, progress.Percent));
            }
            else
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
            }

            switch (progress.Stage)
            {
                case FileTransferStage.Queued:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Queued");
                    SetCancelable(true);
                    break;
                case FileTransferStage.Transferring:
                    _statusLabel.Text = progress.Percent >= 0
                        ? LocalizationService.Format(
                            "FileTransfer.TransferringPercent",
                            progress.Percent)
                        : LocalizationService.Get(
                            "FileTransfer.Transferring");
                    SetCancelable(true);
                    break;
                case FileTransferStage.Finalizing:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Finalizing");
                    _cancelButton.Text = LocalizationService.Get(
                        "Common.Cancel");
                    _cancelButton.Enabled = false;
                    break;
                case FileTransferStage.Completed:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Completed");
                    if (!string.IsNullOrWhiteSpace(progress.FinalFileName) &&
                        !string.Equals(
                            progress.FileName,
                            progress.FinalFileName,
                            StringComparison.Ordinal))
                    {
                        _detailLabel.Text = LocalizationService.Format(
                            "FileTransfer.SavedAs",
                            progress.FinalFileName);
                    }
                    SetCancelable(false);
                    _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                        AutoHideMilliseconds);
                    break;
                case FileTransferStage.Canceled:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Canceled");
                    _detailLabel.Text = progress.Message;
                    SetCancelable(false);
                    _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                        AutoHideMilliseconds);
                    break;
                case FileTransferStage.Failed:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Failed");
                    _detailLabel.Text = progress.Message;
                    SetCancelable(false);
                    break;
            }

            if (!Visible) Show();
            Reposition();
        }

        private static bool IsInProgress(FileTransferStage stage)
        {
            return stage == FileTransferStage.Queued ||
                stage == FileTransferStage.Transferring ||
                stage == FileTransferStage.Finalizing;
        }

        private void SetCancelable(bool cancelable)
        {
            _cancelButton.Text = cancelable
                ? LocalizationService.Get("Common.Cancel")
                : LocalizationService.Get("Common.Close");
            _cancelButton.Enabled = true;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (_progress == null) return;
            if (_progress.Stage == FileTransferStage.Queued ||
                _progress.Stage == FileTransferStage.Transferring ||
                _progress.Stage == FileTransferStage.Finalizing)
            {
                _cancelButton.Enabled = false;
                _coordinator.CancelTransfer(_progress.RequestId);
                return;
            }
            Hide();
        }

        private void FollowTimer_Tick(object sender, EventArgs e)
        {
            if (_hideAtUtc != DateTime.MinValue &&
                DateTime.UtcNow >= _hideAtUtc)
            {
                _hideAtUtc = DateTime.MinValue;
                Hide();
                return;
            }
            if (_progress != null &&
                (Visible ||
                 IsInProgress(_progress.Stage) ||
                 _progress.Stage == FileTransferStage.Failed))
            {
                Reposition();
            }
        }

        private void Reposition()
        {
            if (_progress == null) return;
            var handle = _coordinator.GetWindowHandle(_progress.SessionId);
            NativeRect rect;
            if (handle == IntPtr.Zero ||
                !NativeMethods.IsWindow(handle) ||
                NativeMethods.IsIconic(handle) ||
                !NativeMethods.GetWindowRect(handle, out rect))
            {
                if (Visible) Hide();
                return;
            }

            var workingArea = Screen.FromHandle(handle).WorkingArea;
            var x = rect.Right + 10;
            var y = Math.Max(workingArea.Top, rect.Top);
            if (x + Width > workingArea.Right)
                x = rect.Left - Width - 10;
            if (x < workingArea.Left)
            {
                x = Math.Max(workingArea.Left,
                    Math.Min(rect.Left, workingArea.Right - Width));
                y = Math.Min(
                    workingArea.Bottom - Height,
                    rect.Bottom + 10);
            }
            y = Math.Max(
                workingArea.Top,
                Math.Min(y, workingArea.Bottom - Height));
            Location = new Point(x, y);
            if (!Visible) Show();
        }

        private static string FormatBytes(long bytes)
        {
            var value = (double)Math.Max(bytes, 0L);
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var unit = 0;
            while (value >= 1024D && unit < units.Length - 1)
            {
                value /= 1024D;
                unit++;
            }
            return LocalizationService.Format(
                "FileTransfer.Size",
                value.ToString(
                    unit == 0 ? "0" : "0.##",
                    CultureInfo.CurrentCulture),
                units[unit]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _followTimer.Stop();
                _followTimer.Tick -= FollowTimer_Tick;
                _followTimer.Dispose();
                _cancelButton.Click -= CancelButton_Click;
            }
            base.Dispose(disposing);
        }
    }
}

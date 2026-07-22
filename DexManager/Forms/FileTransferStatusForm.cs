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
        private const int WsExToolWindow = 0x00000080;
        private const int AnimationIntervalMs = 350;
        private const int AutoHideMilliseconds = 4000;
        private const int VisibleQueueRows = 5;
        private readonly FileTransferCoordinator _coordinator;
        private readonly Label _statusLabel;
        private readonly Label[] _queueLabels;
        private readonly Label _detailLabel;
        private readonly Label _countLabel;
        private readonly ThemedButton _cancelButton;
        private readonly Timer _animationTimer;
        private readonly ToolTip _toolTip;
        private FileTransferProgress _progress;
        private DateTime _hideAtUtc;
        private bool _positionInitialized;
        private bool _cancelRequested;
        private int _animationFrame;

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
            ClientSize = new Size(430, 286);

            _statusLabel = new Label
            {
                AutoSize = false,
                Font = UiFonts.Create(12F, FontStyle.Bold),
                ForeColor = palette.TextPrimary,
                Location = new Point(18, 14),
                Size = new Size(394, 28),
                Text = LocalizationService.Get("FileTransfer.Preparing")
            };
            Controls.Add(_statusLabel);

            _toolTip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 400,
                ReshowDelay = 100
            };
            _queueLabels = new Label[VisibleQueueRows];
            for (var index = 0; index < _queueLabels.Length; index++)
            {
                var label = new Label
                {
                    AutoEllipsis = true,
                    ForeColor = index == 0
                        ? palette.TextPrimary
                        : palette.TextSecondary,
                    Font = index == 0
                        ? UiFonts.Create(9.5F, FontStyle.Bold)
                        : UiFonts.Create(9.5F),
                    Location = new Point(18, 50 + index * 25),
                    Size = new Size(394, 23),
                    Visible = false
                };
                _queueLabels[index] = label;
                Controls.Add(label);
            }

            _detailLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextTertiary,
                Location = new Point(18, 180),
                Size = new Size(394, 21)
            };
            _countLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextTertiary,
                Location = new Point(18, 208),
                Size = new Size(286, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cancelButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Cancel"),
                Primary = false,
                Location = new Point(328, 238),
                Size = new Size(84, 32),
                TabStop = false
            };
            _cancelButton.Click += CancelButton_Click;

            Controls.Add(_detailLabel);
            Controls.Add(_countLabel);
            Controls.Add(_cancelButton);

            _animationTimer = new Timer
            {
                Interval = AnimationIntervalMs
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
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
                parameters.ExStyle |= WsExToolWindow;
                return parameters;
            }
        }

        public void UpdateProgress(FileTransferProgress progress)
        {
            if (progress == null || IsDisposed) return;
            if (_progress == null ||
                !string.Equals(
                    _progress.RequestId,
                    progress.RequestId,
                    StringComparison.OrdinalIgnoreCase) ||
                progress.Stage == FileTransferStage.Completed ||
                progress.Stage == FileTransferStage.Failed ||
                progress.Stage == FileTransferStage.Canceled)
            {
                _cancelRequested = false;
            }
            _progress = progress;
            _hideAtUtc = DateTime.MinValue;
            _animationFrame = 0;

            UpdateQueueRows(progress);
            _countLabel.Text = LocalizationService.Format(
                "FileTransfer.Counts",
                progress.CompletedCount,
                progress.FailedCount,
                progress.QueuedCount);
            UpdateStageText();
            UpdateDetailText();

            if (!_positionInitialized)
            {
                SetInitialPosition(progress.SessionId);
                _positionInitialized = true;
            }
            if (!Visible) Show();
        }

        private void UpdateQueueRows(FileTransferProgress progress)
        {
            for (var index = 0; index < _queueLabels.Length; index++)
            {
                var label = _queueLabels[index];
                if (index >= progress.VisibleQueue.Count)
                {
                    label.Visible = false;
                    label.Text = string.Empty;
                    _toolTip.SetToolTip(label, string.Empty);
                    continue;
                }

                var entry = progress.VisibleQueue[index];
                var marker = entry.Active ? "▶  " : "•  ";
                if (entry.Active &&
                    progress.Stage == FileTransferStage.Completed)
                {
                    marker = "✓  ";
                }
                else if (entry.Active &&
                    progress.Stage == FileTransferStage.Failed)
                {
                    marker = "!  ";
                }
                label.Text = marker + entry.DisplayName;
                label.Visible = true;
                _toolTip.SetToolTip(label, entry.DisplayName);
            }
        }

        private void UpdateStageText()
        {
            if (_progress == null) return;
            switch (_progress.Stage)
            {
                case FileTransferStage.Queued:
                    _statusLabel.Text = Animated(
                        LocalizationService.Get(
                            "FileTransfer.Preparing"));
                    SetCancelable(true);
                    break;
                case FileTransferStage.Transferring:
                    _statusLabel.Text = Animated(
                        LocalizationService.Get(
                            "FileTransfer.Transferring"));
                    SetCancelable(true);
                    break;
                case FileTransferStage.Finalizing:
                    _statusLabel.Text = Animated(
                        LocalizationService.Get(
                            "FileTransfer.Finalizing"));
                    _cancelButton.Text = LocalizationService.Get(
                        "Common.Cancel");
                    _cancelButton.Enabled = false;
                    break;
                case FileTransferStage.Completed:
                    _statusLabel.Text = LocalizationService.Format(
                        "FileTransfer.CompletedSummary",
                        _progress.BatchItemCount);
                    SetCancelable(false);
                    if (_progress.QueuedCount == 0)
                    {
                        _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                            AutoHideMilliseconds);
                    }
                    break;
                case FileTransferStage.Canceled:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Canceled");
                    SetCancelable(false);
                    _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                        AutoHideMilliseconds);
                    break;
                case FileTransferStage.Failed:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Failed");
                    SetCancelable(false);
                    break;
            }
        }

        private void UpdateDetailText()
        {
            if (_progress == null) return;
            if (_progress.Stage == FileTransferStage.Failed ||
                _progress.Stage == FileTransferStage.Canceled)
            {
                _detailLabel.Text = _progress.Message;
                _toolTip.SetToolTip(_detailLabel, _progress.Message);
                return;
            }
            if (_progress.Stage == FileTransferStage.Completed &&
                !string.IsNullOrWhiteSpace(_progress.FinalFileName) &&
                !string.Equals(
                    _progress.FileName,
                    _progress.FinalFileName,
                    StringComparison.Ordinal))
            {
                _detailLabel.Text = LocalizationService.Format(
                    "FileTransfer.SavedAs",
                    _progress.FinalFileName);
                _toolTip.SetToolTip(
                    _detailLabel,
                    _progress.FinalFileName);
                return;
            }

            var size = FormatBytes(_progress.FileSize);
            if (_progress.StartedUtc == DateTime.MinValue)
            {
                _detailLabel.Text = size;
                return;
            }
            var elapsed = DateTime.UtcNow - _progress.StartedUtc;
            _detailLabel.Text = LocalizationService.Format(
                "FileTransfer.Detail",
                size,
                FormatElapsed(elapsed));
        }

        private string Animated(string text)
        {
            return text.TrimEnd('.', '…') +
                new string('.', _animationFrame % 3 + 1);
        }

        private void SetCancelable(bool cancelable)
        {
            _cancelButton.Text = cancelable
                ? LocalizationService.Get("Common.Cancel")
                : LocalizationService.Get("Common.Close");
            _cancelButton.Enabled = !cancelable || !_cancelRequested;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (_progress == null) return;
            if (_progress.Stage == FileTransferStage.Queued ||
                _progress.Stage == FileTransferStage.Transferring ||
                _progress.Stage == FileTransferStage.Finalizing)
            {
                _cancelRequested = true;
                _cancelButton.Enabled = false;
                _coordinator.CancelTransfer(_progress.RequestId);
                return;
            }
            Hide();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_hideAtUtc != DateTime.MinValue &&
                DateTime.UtcNow >= _hideAtUtc)
            {
                _hideAtUtc = DateTime.MinValue;
                Hide();
                return;
            }
            if (_progress == null || !Visible) return;
            if (_progress.Stage == FileTransferStage.Queued ||
                _progress.Stage == FileTransferStage.Transferring ||
                _progress.Stage == FileTransferStage.Finalizing)
            {
                _animationFrame++;
                UpdateStageText();
                UpdateDetailText();
            }
        }

        private void SetInitialPosition(string sessionId)
        {
            var handle = _coordinator.GetWindowHandle(sessionId);
            NativeRect rect;
            if (handle != IntPtr.Zero &&
                NativeMethods.IsWindow(handle) &&
                !NativeMethods.IsIconic(handle) &&
                NativeMethods.GetWindowRect(handle, out rect))
            {
                var workingArea = Screen.FromHandle(handle).WorkingArea;
                var x = rect.Right + 10;
                var y = Math.Max(workingArea.Top, rect.Top);
                if (x + Width > workingArea.Right)
                    x = rect.Left - Width - 10;
                if (x < workingArea.Left)
                {
                    x = Math.Max(
                        workingArea.Left,
                        Math.Min(rect.Left, workingArea.Right - Width));
                    y = Math.Min(
                        workingArea.Bottom - Height,
                        rect.Bottom + 10);
                }
                y = Math.Max(
                    workingArea.Top,
                    Math.Min(y, workingArea.Bottom - Height));
                Location = new Point(x, y);
                return;
            }

            var fallback = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                fallback.Right - Width - 16,
                fallback.Bottom - Height - 16);
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            var totalHours = (int)elapsed.TotalHours;
            return totalHours > 0
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}:{1:00}:{2:00}",
                    totalHours,
                    elapsed.Minutes,
                    elapsed.Seconds)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}:{1:00}",
                    (int)elapsed.TotalMinutes,
                    elapsed.Seconds);
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
                _animationTimer.Stop();
                _animationTimer.Tick -= AnimationTimer_Tick;
                _animationTimer.Dispose();
                _toolTip.Dispose();
                _cancelButton.Click -= CancelButton_Click;
            }
            base.Dispose(disposing);
        }
    }
}

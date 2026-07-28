using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class PhoneTransferStatusForm : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int AutoHideMilliseconds = 4000;
        private readonly Label _statusLabel;
        private readonly Label _itemLabel;
        private readonly Label _countLabel;
        private readonly Label _byteLabel;
        private readonly ProgressBar _progressBar;
        private readonly ThemedButton _openFolderButton;
        private readonly ThemedButton _closeButton;
        private readonly Timer _autoHideTimer;
        private string _destinationFolder;
        private DateTime _hideAtUtc;

        public PhoneTransferStatusForm(AppTheme theme)
        {
            var palette = ThemeColors.Use(theme);
            Text = LocalizationService.Get("PhoneTransfer.WindowTitle");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = false;
            ClientSize = new Size(430, 184);
            BackColor = palette.WindowBackground;
            Font = UiFonts.Create(9.5F);

            _statusLabel = new Label
            {
                AutoSize = false,
                Font = UiFonts.Create(12F, FontStyle.Bold),
                ForeColor = palette.TextPrimary,
                Location = new Point(18, 14),
                Size = new Size(394, 28),
                Text = LocalizationService.Get("PhoneTransfer.Receiving")
            };
            _itemLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextSecondary,
                Location = new Point(18, 48),
                Size = new Size(394, 21)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(18, 76),
                Size = new Size(394, 12),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28
            };
            _countLabel = new Label
            {
                ForeColor = palette.TextSecondary,
                Location = new Point(18, 98),
                Size = new Size(190, 22)
            };
            _byteLabel = new Label
            {
                ForeColor = palette.TextSecondary,
                Location = new Point(220, 98),
                Size = new Size(192, 22),
                TextAlign = ContentAlignment.TopRight
            };
            _openFolderButton = new ThemedButton
            {
                Text = LocalizationService.Get(
                    "PhoneTransfer.OpenFolder"),
                Primary = false,
                Location = new Point(212, 136),
                Size = new Size(112, 32),
                Enabled = false
            };
            _openFolderButton.Click += delegate { OpenDestination(); };
            _closeButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Close"),
                Primary = false,
                Location = new Point(328, 136),
                Size = new Size(84, 32)
            };
            _closeButton.Click += delegate { Hide(); };

            Controls.Add(_statusLabel);
            Controls.Add(_itemLabel);
            Controls.Add(_progressBar);
            Controls.Add(_countLabel);
            Controls.Add(_byteLabel);
            Controls.Add(_openFolderButton);
            Controls.Add(_closeButton);

            _autoHideTimer = new Timer { Interval = 250 };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            _autoHideTimer.Start();
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

        public void UpdateProgress(PhoneTransferProgress progress)
        {
            if (progress == null || IsDisposed) return;
            _hideAtUtc = DateTime.MinValue;
            _destinationFolder = progress.DestinationFolder;
            _itemLabel.Text = progress.Stage == PhoneTransferStage.Failed
                ? progress.Error
                : progress.CurrentItem;
            _countLabel.Text = LocalizationService.Format(
                "PhoneTransfer.Items",
                progress.CompletedItems,
                progress.TotalItems);
            _byteLabel.Text = progress.TotalBytes > 0
                ? LocalizationService.Format(
                    "PhoneTransfer.Bytes",
                    FormatBytes(progress.ReceivedBytes),
                    FormatBytes(progress.TotalBytes))
                : LocalizationService.Format(
                    "PhoneTransfer.BytesUnknown",
                    FormatBytes(progress.ReceivedBytes));

            if (progress.TotalBytes > 0)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                var percentage = (int)Math.Max(
                    0,
                    Math.Min(
                        100,
                        progress.ReceivedBytes * 100L /
                        Math.Max(1L, progress.TotalBytes)));
                _progressBar.Value = percentage;
            }
            else if (progress.Stage == PhoneTransferStage.Receiving)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 28;
            }

            if (progress.Stage == PhoneTransferStage.Completed)
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Completed");
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Value = 100;
                _openFolderButton.Enabled = Directory.Exists(
                    _destinationFolder);
                _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                    AutoHideMilliseconds);
            }
            else if (progress.Stage == PhoneTransferStage.Failed ||
                progress.Stage == PhoneTransferStage.Canceled)
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Failed");
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Value = 0;
                _openFolderButton.Enabled = Directory.Exists(
                    _destinationFolder);
                if (progress.Stage == PhoneTransferStage.Canceled)
                {
                    _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                        AutoHideMilliseconds);
                }
            }
            else
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Receiving");
            }
        }

        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            if (_hideAtUtc == DateTime.MinValue ||
                DateTime.UtcNow < _hideAtUtc)
            {
                return;
            }
            _hideAtUtc = DateTime.MinValue;
            Hide();
        }

        private void OpenDestination()
        {
            if (string.IsNullOrWhiteSpace(_destinationFolder) ||
                !Directory.Exists(_destinationFolder))
            {
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = _destinationFolder,
                UseShellExecute = true
            });
        }

        private static string FormatBytes(long bytes)
        {
            var value = Math.Max(0L, bytes);
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var amount = (double)value;
            var unit = 0;
            while (amount >= 1024D && unit < units.Length - 1)
            {
                amount /= 1024D;
                unit++;
            }
            return unit == 0
                ? value + " " + units[unit]
                : amount.ToString("0.##") + " " + units[unit];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Tick -= AutoHideTimer_Tick;
                _autoHideTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

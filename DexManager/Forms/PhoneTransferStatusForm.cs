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
        private readonly Label _statusLabel;
        private readonly Label _itemLabel;
        private readonly Label _countLabel;
        private readonly Label _byteLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _openFolderButton;
        private string _destinationFolder;

        public PhoneTransferStatusForm(AppTheme theme)
        {
            var palette = ThemeColors.Use(theme);
            Text = LocalizationService.Get("PhoneTransfer.WindowTitle");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(470, 225);
            BackColor = palette.WindowBackground;
            Font = UiFonts.Create(9.5F);

            _statusLabel = new Label
            {
                AutoSize = false,
                Font = UiFonts.Create(15F, FontStyle.Bold),
                ForeColor = palette.TextPrimary,
                Location = new Point(22, 20),
                Size = new Size(420, 30),
                Text = LocalizationService.Get("PhoneTransfer.Receiving")
            };
            _itemLabel = new Label
            {
                AutoEllipsis = true,
                ForeColor = palette.TextSecondary,
                Location = new Point(23, 58),
                Size = new Size(420, 23)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(24, 90),
                Size = new Size(420, 16),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28
            };
            _countLabel = new Label
            {
                ForeColor = palette.TextSecondary,
                Location = new Point(23, 118),
                Size = new Size(205, 22)
            };
            _byteLabel = new Label
            {
                ForeColor = palette.TextSecondary,
                Location = new Point(237, 118),
                Size = new Size(207, 22),
                TextAlign = ContentAlignment.TopRight
            };
            _openFolderButton = new ThemedButton
            {
                Text = LocalizationService.Get(
                    "PhoneTransfer.OpenFolder"),
                Location = new Point(294, 166),
                Size = new Size(150, 36),
                Enabled = false
            };
            _openFolderButton.Click += delegate { OpenDestination(); };

            Controls.Add(_statusLabel);
            Controls.Add(_itemLabel);
            Controls.Add(_progressBar);
            Controls.Add(_countLabel);
            Controls.Add(_byteLabel);
            Controls.Add(_openFolderButton);
        }

        public void UpdateProgress(PhoneTransferProgress progress)
        {
            if (progress == null || IsDisposed) return;
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
            }
            else
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Receiving");
            }
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
    }
}

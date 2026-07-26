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
        private void OffsetMainContent(int offsetX)
        {
            foreach (Control control in Controls)
            {
                control.Left += offsetX;
            }
        }

        private void ApplyDesignLayout()
        {
            foreach (Control control in Controls)
            {
                var divider = control as Panel;
                if (divider != null && divider.Height == 1)
                    divider.Visible = false;
            }

            _statusCard = CreateCard(new Point(220, 64), new Size(686, 84));
            _displayCard = CreateCard(new Point(220, 164), new Size(686, 182));
            _optionsCard = CreateCard(new Point(220, 362), new Size(686, 270));

            _pageTitle.Location = new Point(220, 14);

            MoveToCard(_indicatorDot, _statusCard, 18, 22);
            MoveToCard(_indicatorStatus, _statusCard, 70, 16);
            _indicatorStatus.Size = new Size(360, 28);
            MoveToCard(_indicatorDetail, _statusCard, 70, 48);
            _indicatorDetail.Size = new Size(580, 20);
            _deviceInfoLabel.Visible = false;

            MoveToCard(_displaySettingsTitle, _displayCard, 20, 13);
            MoveToCard(_resolutionLabel, _displayCard, 20, 51);
            MoveToCard(_resolutionBox, _displayCard, 20, 72);
            _resolutionBox.Size = new Size(304, 32);
            MoveToCard(_widthLabel, _displayCard, 158, 78);
            MoveToCard(_widthBox, _displayCard, 196, 72);
            _widthBox.Size = new Size(55, 32);
            MoveToCard(_heightLabel, _displayCard, 257, 78);
            MoveToCard(_heightBox, _displayCard, 305, 72);
            _heightBox.Size = new Size(55, 32);
            MoveToCard(_dpiLabel, _displayCard, 362, 51);
            MoveToCard(_dpiBox, _displayCard, 362, 72);
            _dpiBox.Size = new Size(304, 32);
            MoveToCard(_bitRateLabel, _displayCard, 20, 113);
            MoveToCard(_bitRateBox, _displayCard, 20, 134);
            _bitRateBox.Size = new Size(304, 32);
            MoveToCard(_maxFpsLabel, _displayCard, 362, 113);
            MoveToCard(_maxFpsBox, _displayCard, 362, 134);
            _maxFpsBox.Size = new Size(304, 32);

            MoveToCard(_optionsTitle, _optionsCard, 20, 13);
            MoveToCard(_turnScreenOffBox, _optionsCard, 20, 49);
            MoveToCard(_useHidKeyboardBox, _optionsCard, 20, 84);
            MoveToCard(_useHidMouseBox, _optionsCard, 20, 119);
            MoveToCard(_forceStopAppBox, _optionsCard, 362, 49);
            MoveToCard(_flexDisplayBox, _optionsCard, 362, 84);
            MoveToCard(
                _stayAwakeBox,
                _optionsCard,
                362,
                _selectedMode == 0 ? 84 : 119);
            foreach (var option in new[]
            {
                _turnScreenOffBox,
                _useHidKeyboardBox,
                _useHidMouseBox,
                _forceStopAppBox,
                _flexDisplayBox,
                _stayAwakeBox
            })
            {
                option.Size = new Size(284, 30);
            }
            MoveToCard(_startAppLabel, _optionsCard, 20, 168);
            MoveToCard(_startAppBox, _optionsCard, 20, 189);
            _startAppBox.Size = new Size(470, 32);
            MoveToCard(_loadAppsButton, _optionsCard, 500, 189);
            _loadAppsButton.Size = new Size(146, 32);
            MoveToCard(_advancedToggle, _optionsCard, 20, 238);
            MoveToCard(_additionalArgumentsBox, _optionsCard, 190, 232);
            _additionalArgumentsBox.Size = new Size(456, 32);

            _startButton.Location = new Point(754, 646);
            _stopButton.Location = _startButton.Location;
            _applySettingsLink.Location = new Point(638, 655);
            _modeHintLabel.Visible = false;

            _sidebar.Location = new Point(14, 14);
            _sidebar.Size = new Size(188, 618);
            _sidebar.BringToFront();
        }

        private RoundedPanel CreateCard(Point location, Size size)
        {
            var card = new RoundedPanel
            {
                Location = location,
                Size = size,
                Radius = 14,
                FillColor = _theme.CardBackground,
                BorderColor = _theme.CardBorder
            };
            Controls.Add(card);
            card.SendToBack();
            return card;
        }

        private static void MoveToCard(
            Control control,
            Control card,
            int x,
            int y)
        {
            control.Parent = card;
            control.Location = new Point(x, y);
        }

        private void ApplyTheme()
        {
            BackColor = _theme.WindowBackground;
            _pageTitle.ForeColor = _theme.TextPrimary;
            _indicatorStatus.ForeColor = _theme.TextPrimary;
            _indicatorDetail.ForeColor = _theme.TextTertiary;
            _deviceInfoLabel.ForeColor = _theme.TextTertiary;

            ApplyCardTheme(_statusCard);
            ApplyCardTheme(_displayCard);
            ApplyCardTheme(_optionsCard);
            _displaySettingsTitle.ForeColor = _theme.TextSecondary;
            _optionsTitle.ForeColor = _theme.TextSecondary;

            foreach (var label in new[]
            {
                _resolutionLabel,
                _widthLabel,
                _heightLabel,
                _dpiLabel,
                _bitRateLabel,
                _maxFpsLabel,
                _startAppLabel
            })
            {
                label.ForeColor = _theme.TextTertiary;
                label.BackColor = _theme.CardBackground;
            }

            foreach (var option in new[]
            {
                _turnScreenOffBox,
                _useHidKeyboardBox,
                _useHidMouseBox,
                _forceStopAppBox,
                _flexDisplayBox,
                _stayAwakeBox
            })
            {
                option.BackColor = _theme.CardBackground;
                option.ForeColor = _theme.TextPrimary;
            }

            foreach (var control in new Control[]
            {
                _resolutionBox,
                _widthBox,
                _heightBox,
                _dpiBox,
                _bitRateBox,
                _maxFpsBox,
                _startAppBox,
                _additionalArgumentsBox
            })
            {
                control.BackColor = _theme.CardSoft;
                control.ForeColor = _theme.TextPrimary;
            }

            _advancedToggle.BackColor = _theme.CardBackground;
            _advancedToggle.LinkColor = _theme.Accent;
            _advancedToggle.ActiveLinkColor = _theme.AccentHover;
            _applySettingsLink.LinkColor = _theme.Accent;
            _applySettingsLink.ActiveLinkColor = _theme.AccentHover;

            _sidebar.BackColor = _theme.WindowBackground;
            _sidebar.FillColor = _theme.NavigationBackground;
            _sidebar.BorderColor = _theme.CardBorder;
            foreach (Control control in _sidebar.Controls)
            {
                control.BackColor = _theme.NavigationBackground;
                var label = control as Label;
                if (label != null)
                    label.ForeColor = _theme.TextTertiary;
                var button = control as ThemedButton;
                if (button != null)
                    button.ForeColor = _theme.TextSecondary;
                control.Invalidate();
            }

            _indicatorDot.Invalidate();
            _resolutionBox.Invalidate();
            _widthBox.Invalidate();
            _heightBox.Invalidate();
            _dpiBox.Invalidate();
            _bitRateBox.Invalidate();
            _maxFpsBox.Invalidate();
            _startAppBox.Invalidate();
            _startButton.Invalidate();
            _stopButton.Invalidate();
            _loadAppsButton.Invalidate();
            Invalidate(true);
        }

        private void ApplyThemeSelection(AppTheme theme)
        {
            _theme = ThemeColors.Use(theme);
            ApplyTheme();
            if (_settingsForm != null &&
                !_settingsForm.IsDisposed)
            {
                _settingsForm.ApplyCurrentTheme();
            }
            if (_logForm != null && !_logForm.IsDisposed)
                _logForm.ApplyCurrentTheme();
            if (_environmentCheckForm != null &&
                !_environmentCheckForm.IsDisposed)
            {
                _environmentCheckForm.ApplyCurrentTheme();
            }
        }

        private void ApplyCardTheme(RoundedPanel card)
        {
            card.BackColor = _theme.WindowBackground;
            card.FillColor = _theme.CardBackground;
            card.BorderColor = _theme.CardBorder;
            foreach (Control control in card.Controls)
            {
                var label = control as Label;
                if (label != null)
                    label.BackColor = _theme.CardBackground;
            }
        }

        private void AddModeSidebar()
        {
            _sidebar = new RoundedPanel
            {
                Location = new Point(14, 14),
                Size = new Size(188, 587),
                Radius = 14,
                BackColor = _theme.NavigationBackground,
                FillColor = _theme.NavigationBackground,
                BorderColor = _theme.CardBorder
            };

            _sidebar.Controls.Add(new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 18),
                Text = LocalizationService.Get("Main.Mode")
            });

            _dexModeButton = CreateSidebarButton(
                LocalizationService.Get("Main.Dex"), 52, true);
            _dexModeButton.Click += delegate { SelectDexMode(); };
            _sidebar.Controls.Add(_dexModeButton);

            _singleModeButton1 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 1),
                94,
                false);
            _singleModeButton1.Click += delegate { SelectSingleWindowPreview(1); };
            _sidebar.Controls.Add(_singleModeButton1);

            _singleModeButton2 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 2),
                136,
                false);
            _singleModeButton2.Click += delegate { SelectSingleWindowPreview(2); };
            _sidebar.Controls.Add(_singleModeButton2);

            _singleModeButton3 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 3),
                178,
                false);
            _singleModeButton3.Click += delegate { SelectSingleWindowPreview(3); };
            _sidebar.Controls.Add(_singleModeButton3);

            _sidebar.Controls.Add(new Label
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 238),
                Size = new Size(148, 70),
                Text = LocalizationService.Get("Main.SidebarHint")
            });

            var settingsButton = CreateSidebarButton(
                LocalizationService.Get("Main.Settings"),
                _sidebar.Height - 48,
                false,
                false);
            settingsButton.ShowSettingsIcon = true;
            settingsButton.TrailingText =
                "v" + Application.ProductVersion;
            settingsButton.Anchor =
                AnchorStyles.Left | AnchorStyles.Bottom;
            settingsButton.Click += delegate { ShowSettingsForm(); };
            _sidebar.Controls.Add(settingsButton);

            Controls.Add(_sidebar);
            _sidebar.BringToFront();
        }

        private ThemedButton CreateSidebarButton(
            string text,
            int y,
            bool selected,
            bool showDot = true)
        {
            return new ThemedButton
            {
                Text = text,
                Primary = selected,
                CornerRadius = 18,
                NavigationStyle = true,
                ShowNavigationDot = showDot,
                TabStop = false,
                Location = new Point(10, y),
                Size = new Size(168, 34),
                BackColor = _theme.NavigationBackground,
                ForeColor = _theme.TextSecondary
            };
        }
    }
}

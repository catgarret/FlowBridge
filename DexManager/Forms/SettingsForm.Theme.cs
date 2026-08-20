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
        public void ApplyCurrentTheme()
        {
            _theme = ThemeColors.Current;
            BackColor = _theme.WindowBackground;
            _contentHost.BackColor = _theme.WindowBackground;
            _bottomPanel.BackColor = _theme.WindowBackground;
            _titleLabel.ForeColor = _theme.TextPrimary;
            _titleLabel.BackColor = _theme.WindowBackground;
            _descriptionLabel.ForeColor = _theme.TextTertiary;
            _descriptionLabel.BackColor = _theme.WindowBackground;
            _saveStatusLabel.BackColor = _theme.WindowBackground;
            if (!_saveStatusLabel.Visible)
                _saveStatusLabel.ForeColor = _theme.TextTertiary;
            ApplySurfaceTheme(
                _bottomPanel,
                _theme.WindowBackground);

            _sidebar.BackColor = _theme.WindowBackground;
            _sidebar.FillColor = _theme.NavigationBackground;
            _sidebar.BorderColor = _theme.CardBorder;
            ApplySurfaceTheme(
                _sidebar,
                _theme.NavigationBackground);

            foreach (var page in _pages)
            {
                page.BackColor = _theme.WindowBackground;
                foreach (Control control in page.Controls)
                {
                    var card = control as RoundedPanel;
                    if (card == null) continue;
                    card.BackColor = _theme.WindowBackground;
                    card.FillColor = _theme.CardBackground;
                    card.BorderColor = _theme.CardBorder;
                    ApplySurfaceTheme(card, _theme.CardBackground);
                }
            }

            Invalidate(true);
        }

        private void ApplySurfaceTheme(
            Control parent,
            Color surface)
        {
            foreach (Control control in parent.Controls)
            {
                var panel = control as Panel;
                if (panel != null && !(control is RoundedPanel))
                    panel.BackColor = surface;

                var label = control as Label;
                if (label != null)
                {
                    label.BackColor = surface;
                    if (label != _saveStatusLabel)
                    {
                        label.ForeColor = label.Font.Bold
                            ? _theme.TextSecondary
                            : _theme.TextTertiary;
                    }
                }

                var radio = control as RadioButton;
                if (radio != null)
                {
                    radio.BackColor = surface;
                    radio.ForeColor = _theme.TextPrimary;
                }

                var check = control as ThemedCheckBox;
                if (check != null)
                {
                    check.BackColor = surface;
                    check.ForeColor = _theme.TextPrimary;
                }

                var button = control as ThemedButton;
                if (button != null)
                {
                    button.BackColor = surface;
                    button.ForeColor = _theme.TextSecondary;
                }

                if (control.HasChildren)
                    ApplySurfaceTheme(control, surface);
                control.Invalidate();
            }
        }

        private Control BuildSidebar()
        {
            _sidebar = new RoundedPanel
            {
                Location = new Point(16, 16),
                Size = new Size(188, 664),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                    AnchorStyles.Left,
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
                Text = LocalizationService.Get("Main.Settings")
            });

            var labels = new[]
            {
                LocalizationService.Get("Settings.General"),
                LocalizationService.Get("Settings.Connection"),
                LocalizationService.Get("Settings.Paths"),
                LocalizationService.Get("Settings.Keyboard"),
                LocalizationService.Get("Settings.Diagnostics"),
                LocalizationService.Get("Settings.About")
            };
            for (var index = 0; index < labels.Length; index++)
            {
                var pageIndex = index;
                var button = new ThemedButton
                {
                    Text = labels[index],
                    Primary = index == 0,
                    CornerRadius = 18,
                    NavigationStyle = true,
                    ShowNavigationDot = true,
                    TabStop = true,
                    Location = new Point(10, 52 + index * 42),
                    Size = new Size(168, 34),
                    BackColor = _theme.NavigationBackground,
                    ForeColor = _theme.TextSecondary
                };
                button.Click += delegate { ShowPage(pageIndex); };
                _navigationButtons.Add(button);
                _sidebar.Controls.Add(button);
            }
            return _sidebar;
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            _activePageIndex = index;
            for (var i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visible = i == index;
                _navigationButtons[i].Primary = i == index;
                _navigationButtons[i].Invalidate();
            }
            _pages[index].BringToFront();
            if (index == 4)
            {
                RefreshDeviceDiagnosticsAsync();
                RefreshDisplayCleanupStatusAsync();
            }
        }
    }
}

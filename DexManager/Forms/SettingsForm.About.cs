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
        private Control BuildAboutPage()
        {
            var page = CreatePage();
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                Width = 620,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Format(
                    "Settings.AboutVersion",
                    Application.ProductVersion),
                true,
                new Padding(0, 0, 0, 14)));
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutSummary"),
                false,
                new Padding(0, 0, 0, 8)));
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutDeveloper"),
                false,
                new Padding(0, 0, 0, 2)));
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutCopyright"),
                false,
                new Padding(0, 0, 0, 2)));
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutLicense"),
                false,
                new Padding(0, 0, 0, 8)));

            var projectLink = new LinkLabel
            {
                AutoSize = true,
                Font = UiFonts.Create(9.5F, FontStyle.Regular),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = _theme.Accent,
                ActiveLinkColor = _theme.AccentHover,
                BackColor = _theme.CardBackground,
                Margin = new Padding(0, 0, 0, 18),
                Text = ProjectUrl
            };
            projectLink.LinkClicked += delegate
            {
                OpenProjectPage();
            };
            panel.Controls.Add(projectLink);
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutScrcpy"),
                false,
                new Padding(0, 0, 0, 12)));
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutComponents"),
                false,
                new Padding(0, 0, 0, 12)));
            panel.Controls.Add(CreateAboutLabel(
                LocalizationService.Get("Settings.AboutSamsung"),
                false,
                new Padding(0, 0, 0, 18)));

            var noticesButton = CreateActionButton(
                LocalizationService.Get("Settings.OpenNotices"),
                220);
            noticesButton.Click += delegate
            {
                OpenThirdPartyNotices();
            };
            panel.Controls.Add(noticesButton);
            AddCard(
                page,
                LocalizationService.Get("Settings.About"),
                panel);
            return page;
        }

        private Label CreateAboutLabel(
            string text,
            bool bold,
            Padding margin)
        {
            return new Label
            {
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                Font = UiFonts.Create(
                    9.5F,
                    bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = bold
                    ? _theme.TextPrimary
                    : _theme.TextSecondary,
                BackColor = _theme.CardBackground,
                Margin = margin,
                Text = text
            };
        }

        private void OpenProjectPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ProjectUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(
                        LocalizationService.Get("Settings.OpenProjectFailed"),
                        ex.Message),
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenThirdPartyNotices()
        {
            if (_thirdPartyLicensesForm == null ||
                _thirdPartyLicensesForm.IsDisposed)
            {
                _thirdPartyLicensesForm =
                    new ThirdPartyLicensesForm();
                _thirdPartyLicensesForm.FormClosed += delegate
                {
                    _thirdPartyLicensesForm = null;
                };
                _thirdPartyLicensesForm.Show();
            }
            else
            {
                if (_thirdPartyLicensesForm.WindowState ==
                    FormWindowState.Minimized)
                {
                    _thirdPartyLicensesForm.WindowState =
                        FormWindowState.Normal;
                }
                _thirdPartyLicensesForm.BringToFront();
                _thirdPartyLicensesForm.Activate();
            }
        }

    }
}

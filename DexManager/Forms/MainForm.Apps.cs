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
        private void SetSelectedAppPackage(
            string packageName,
            string appName)
        {
            packageName = packageName ?? string.Empty;
            foreach (var item in _startAppBox.Items)
            {
                var app = item as ScrcpyAppInfo;
                if (app == null ||
                    !string.Equals(
                        app.PackageName ?? string.Empty,
                        packageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(appName) &&
                    string.Equals(
                        app.Name,
                        app.PackageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    app.Name = appName;
                }
                _startAppBox.SelectedItem = item;
                return;
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                _startAppBox.SelectedIndex = 0;
                return;
            }

            var placeholder = new ScrcpyAppInfo
            {
                Name = string.IsNullOrWhiteSpace(appName)
                    ? packageName
                    : appName,
                PackageName = packageName
            };
            _startAppBox.Items.Add(placeholder);
            _startAppBox.SelectedItem = placeholder;
        }

        private void StartAppBox_SelectionChangeCommitted(
            object sender,
            EventArgs e)
        {
            if (_selectedMode > 0 &&
                ApplySelectedAppProfileIfAvailable())
            {
                return;
            }

            SaveSelectedAppIdentity();
            UpdateAppProfileControls();
        }

        private void SaveSelectedAppIdentity()
        {
            var packageName = GetSelectedAppPackage();
            var appName = GetSelectedAppName(packageName);
            var selectedMode = _selectedMode;
            try
            {
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    if (selectedMode == 0)
                    {
                        candidate.Scrcpy.StartAppPackage = packageName;
                        candidate.Scrcpy.StartAppName = appName;
                    }
                    else
                    {
                        var slot = GetSingleWindowSettings(
                            candidate,
                            selectedMode);
                        slot.StartAppPackage = packageName;
                        slot.StartAppName = appName;
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppIdentitySaveFailed"),
                    ex);
            }
        }

        private void RememberStartedApp(
            string packageName,
            string appName)
        {
            packageName = (packageName ?? string.Empty).Trim();
            if (packageName.Length == 0) return;

            appName = string.IsNullOrWhiteSpace(appName)
                ? packageName
                : appName.Trim();
            try
            {
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    if (candidate.RememberedApps == null)
                    {
                        candidate.RememberedApps =
                            new List<RememberedAppSettings>();
                    }

                    for (var index = candidate.RememberedApps.Count - 1;
                        index >= 0;
                        index--)
                    {
                        var remembered = candidate.RememberedApps[index];
                        if (remembered == null ||
                            string.IsNullOrWhiteSpace(
                                remembered.PackageName) ||
                            string.Equals(
                                remembered.PackageName,
                                packageName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            candidate.RememberedApps.RemoveAt(index);
                        }
                    }

                    candidate.RememberedApps.Insert(
                        0,
                        new RememberedAppSettings
                        {
                            Name = appName,
                            PackageName = packageName
                        });
                    while (candidate.RememberedApps.Count >
                        MaxRememberedApps)
                    {
                        candidate.RememberedApps.RemoveAt(
                            candidate.RememberedApps.Count - 1);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.RememberedAppSaveFailed"),
                    ex);
                return;
            }

            AddRememberedAppItems();
        }

        private void AddRememberedAppItems()
        {
            if (_settings.RememberedApps == null) return;

            foreach (var remembered in _settings.RememberedApps)
            {
                if (remembered == null ||
                    string.IsNullOrWhiteSpace(
                        remembered.PackageName) ||
                    ContainsAppPackage(remembered.PackageName))
                {
                    continue;
                }

                _startAppBox.Items.Add(new ScrcpyAppInfo
                {
                    Name = string.IsNullOrWhiteSpace(remembered.Name)
                        ? remembered.PackageName
                        : remembered.Name,
                    PackageName = remembered.PackageName
                });
            }
        }

        private bool ContainsAppPackage(string packageName)
        {
            foreach (var item in _startAppBox.Items)
            {
                var app = item as ScrcpyAppInfo;
                if (app != null &&
                    string.Equals(
                        app.PackageName,
                        packageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void AddNoStartAppItem()
        {
            _startAppBox.Items.Add(new ScrcpyAppInfo
            {
                Name = NoStartAppText,
                PackageName = string.Empty
            });
            if (_startAppBox.SelectedIndex < 0)
                _startAppBox.SelectedIndex = 0;
        }

        private static CheckBox CreateOption(string text, int x, int y)
        {
            return new ThemedCheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(284, 30)
            };
        }

        private static ThemedSelectControl CreateCustomSelect(
            int x,
            int y,
            int width)
        {
            return new ThemedSelectControl
            {
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
        }

        private static ThemedNumberControl CreateCustomNumber(
            int min,
            int max,
            int x,
            int y,
            int width,
            bool showStepButtons)
        {
            var control = new ThemedNumberControl
            {
                Minimum = min,
                Maximum = max,
                Increment = 1,
                ShowStepButtons = showStepButtons,
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
            control.Value = min;
            return control;
        }

        private static ThemedTextControl CreateCustomText(
            int x,
            int y,
            int width)
        {
            return new ThemedTextControl
            {
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
        }

        private ThemedButton CreateThemedButton(
            string text,
            bool primary,
            int x,
            int y,
            int width)
        {
            return new ThemedButton
            {
                Text = text,
                Primary = primary,
                ForeColor = _theme.TextPrimary,
                Location = new Point(x, y),
                Size = new Size(width, 34)
            };
        }
    }
}

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
        private Control CreatePage()
        {
            return new FlowLayoutPanel
            {
                AutoScroll = true,
                BackColor = _theme.WindowBackground,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 0, 8, 16)
            };
        }

        private Control AddCard(
            Control page,
            string title,
            Control content)
        {
            content.Location = new Point(18, CardContentTop);
            content.Width = CardContentWidth;
            content.BackColor = _theme.CardBackground;
            NormalizeLastRowMargin(content);
            content.PerformLayout();
            var preferred = content.GetPreferredSize(
                new Size(CardContentWidth, 0));
            content.Size = new Size(
                CardContentWidth,
                Math.Max(preferred.Height, 32));

            var card = new RoundedPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(CardWidth, 94),
                Padding = new Padding(
                    0,
                    0,
                    0,
                    CardContentBottom),
                Margin = new Padding(0, 0, 0, 14),
                Radius = 14,
                BackColor = _theme.WindowBackground,
                FillColor = _theme.CardBackground,
                BorderColor = _theme.CardBorder
            };
            card.Controls.Add(new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(11F, FontStyle.Bold),
                ForeColor = _theme.TextSecondary,
                BackColor = _theme.CardBackground,
                Location = new Point(20, 15),
                Text = title
            });
            card.Controls.Add(content);
            page.Controls.Add(card);
            return card;
        }

        private static TableLayoutPanel CreateTable()
        {
            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Width = CardContentWidth,
                MinimumSize = new Size(CardContentWidth, 0),
                MaximumSize = new Size(CardContentWidth, 0),
                BackColor = ThemeColors.Current.CardBackground,
                Padding = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(
                SizeType.Absolute,
                205F));
            table.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent,
                100F));
            return table;
        }

        private static void NormalizeLastRowMargin(Control content)
        {
            var table = content as TableLayoutPanel;
            if (table != null)
            {
                var lastRow = -1;
                foreach (Control control in table.Controls)
                {
                    if (!control.Visible) continue;
                    lastRow = Math.Max(
                        lastRow,
                        table.GetRow(control));
                }

                foreach (Control control in table.Controls)
                {
                    if (!control.Visible ||
                        table.GetRow(control) != lastRow)
                    {
                        continue;
                    }
                    var margin = control.Margin;
                    control.Margin = new Padding(
                        margin.Left,
                        margin.Top,
                        margin.Right,
                        0);
                }
                return;
            }

            Control lastControl = null;
            foreach (Control control in content.Controls)
            {
                if (!control.Visible) continue;
                if (lastControl == null ||
                    control.Bottom + control.Margin.Bottom >
                    lastControl.Bottom + lastControl.Margin.Bottom)
                {
                    lastControl = control;
                }
            }
            if (lastControl == null) return;
            var lastMargin = lastControl.Margin;
            lastControl.Margin = new Padding(
                lastMargin.Left,
                lastMargin.Top,
                lastMargin.Right,
                0);
        }

        private static Label CreateHint(string text)
        {
            return new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                ForeColor = ThemeColors.Current.TextTertiary,
                BackColor = ThemeColors.Current.CardBackground,
                Text = text
            };
        }

        private static ThemedTextControl AddText(
            TableLayoutPanel table,
            string label)
        {
            var box = CreateTextBox();
            AddRow(table, label, box);
            return box;
        }

        private static ThemedHotkeyControl AddHotkey(
            TableLayoutPanel table,
            string label)
        {
            var box = new ThemedHotkeyControl
            {
                Dock = DockStyle.Fill,
                Height = 32
            };
            AddRow(table, label, box);
            return box;
        }

        private static ThemedTextControl AddPath(
            TableLayoutPanel table,
            string label,
            bool file)
        {
            ThemedTextControl box;
            var panel = CreatePathPanel(out box, file);
            AddRow(table, label, panel);
            return box;
        }

        private ThemedTextControl AddDevicePath(
            TableLayoutPanel table,
            string label)
        {
            var box = CreateTextBox();
            box.UseMiddleEllipsis = true;
            var button = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Browse"),
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0)
            };
            button.Click += delegate { BrowseDeviceFolder(box); };
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 32,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = ThemeColors.Current.CardBackground
            };
            panel.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent,
                100F));
            panel.ColumnStyles.Add(new ColumnStyle(
                SizeType.Absolute,
                100F));
            panel.Controls.Add(box, 0, 0);
            panel.Controls.Add(button, 1, 0);
            AddRow(table, label, panel);
            return box;
        }

        private static Panel CreatePathPanel(
            out ThemedTextControl box,
            bool file)
        {
            var textBox = CreateTextBox();
            textBox.UseMiddleEllipsis = true;
            box = textBox;
            var button = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Browse"),
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0)
            };
            button.Click += delegate
            {
                if (file)
                {
                    using (var dialog = new OpenFileDialog
                    {
                        Filter = LocalizationService.Get(
                            "Settings.ExecutableFilter")
                    })
                    {
                        if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.FileName;
                    }
                }
                else
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.SelectedPath;
                    }
                }
            };
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 32,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = ThemeColors.Current.CardBackground
            };
            panel.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent,
                100F));
            panel.ColumnStyles.Add(new ColumnStyle(
                SizeType.Absolute,
                100F));
            panel.Controls.Add(textBox, 0, 0);
            panel.Controls.Add(button, 1, 0);
            return panel;
        }

        private static ThemedTextControl CreateTextBox()
        {
            return new ThemedTextControl
            {
                Dock = DockStyle.Fill,
                Height = 32,
                Margin = Padding.Empty
            };
        }

        private static ThemedNumberControl AddNumber(
            TableLayoutPanel table,
            string label,
            int min,
            int max)
        {
            var box = new ThemedNumberControl
            {
                Minimum = min,
                Maximum = max,
                Increment = 1,
                ShowStepButtons = true,
                Dock = DockStyle.Fill,
                Height = 32
            };
            box.Value = min;
            AddRow(table, label, box);
            return box;
        }

        private static CheckBox AddCheck(TableLayoutPanel table, string label)
        {
            var box = new ThemedCheckBox
            {
                Text = label,
                Dock = DockStyle.Fill,
                Height = 30,
                BackColor = ThemeColors.Current.CardBackground
            };
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(box, 0, row);
            table.SetColumnSpan(box, 2);
            box.Margin = new Padding(3, 4, 3, 5);
            return box;
        }

        private static ThemedSelectControl AddCombo<T>(
            TableLayoutPanel table,
            string label)
        {
            var box = CreateSelect();
            foreach (var value in Enum.GetValues(typeof(T))) box.Items.Add(value);
            AddRow(table, label, box);
            return box;
        }

        private static ThemedSelectControl CreateSelect()
        {
            return new ThemedSelectControl
            {
                Dock = DockStyle.Fill,
                Height = 32
            };
        }

        private static RadioButton CreateRadio(string text)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeColors.Current.CardBackground,
                ForeColor = ThemeColors.Current.TextPrimary
            };
        }

        private static Button CreateActionButton(
            string text,
            int width)
        {
            return new ThemedButton
            {
                Text = text,
                Size = new Size(width, 34),
                Margin = new Padding(0, 0, 8, 0)
            };
        }

        private static void AddReadOnly(TableLayoutPanel table, string label, string value)
        {
            AddRow(table, label, new Label
            {
                AutoSize = true,
                MaximumSize = new Size(410, 0),
                ForeColor = ThemeColors.Current.TextSecondary,
                BackColor = ThemeColors.Current.CardBackground,
                Text = value
            });
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = ThemeColors.Current.TextTertiary,
                BackColor = ThemeColors.Current.CardBackground,
                Margin = new Padding(3, 9, 12, 9)
            }, 0, row);
            if (control.Dock == DockStyle.Fill)
            {
                control.Dock = DockStyle.None;
                control.Width = CardContentWidth - 208;
            }
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            table.Controls.Add(control, 1, row);
            control.Margin = new Padding(3, 5, 0, 6);
        }

        private static decimal Clamp(int value, ThemedNumberControl box)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }

        private static decimal MillisecondsToSeconds(
            int milliseconds,
            ThemedNumberControl box)
        {
            var seconds = (int)Math.Ceiling(
                Math.Max(milliseconds, 0) / 1000M);
            return Clamp(seconds, box);
        }

        private static int SecondsToMilliseconds(
            ThemedNumberControl box)
        {
            return checked((int)box.Value * 1000);
        }
    }
}

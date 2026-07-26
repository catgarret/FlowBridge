using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    internal enum MiniControlBarCommand
    {
        ScreenOff,
        ScreenOn,
        Power,
        Fullscreen,
        ResetSize,
        Capture,
        OpenManager
    }

    internal sealed class MiniControlBarCommandEventArgs : EventArgs
    {
        internal MiniControlBarCommandEventArgs(
            MiniControlBarCommand command,
            IntPtr targetHandle)
        {
            Command = command;
            TargetHandle = targetHandle;
        }

        internal MiniControlBarCommand Command { get; private set; }
        internal IntPtr TargetHandle { get; private set; }
    }

    internal sealed class MiniControlBarForm : Form
    {
        private const int BarWidth = 44;
        private const int ButtonSize = 36;
        private const int ButtonGap = 2;
        private const int BarPadding = 4;
        private const int CornerRadius = 9;
        private const int WmMouseActivate = 0x0021;
        private const int MaNoActivate = 3;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;

        private readonly IntPtr _targetHandle;
        private readonly List<MiniControlButton> _commandButtons =
            new List<MiniControlButton>();
        private readonly ToolTip _toolTip = new ToolTip
        {
            ShowAlways = true,
            InitialDelay = 350,
            ReshowDelay = 100,
            AutoPopDelay = 6000
        };
        private readonly MiniControlButton _collapseButton;
        private ThemePalette _theme;
        private bool _collapsed;

        internal MiniControlBarForm(
            IntPtr targetHandle,
            AppTheme theme,
            string captureHotkey)
        {
            _targetHandle = targetHandle;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;
            Size = new Size(BarWidth, BarWidth);

            AddCommandButton(
                MiniControlBarCommand.ScreenOff,
                MiniControlIcon.ScreenOff,
                "MiniBar.ScreenOff");
            AddCommandButton(
                MiniControlBarCommand.ScreenOn,
                MiniControlIcon.ScreenOn,
                "MiniBar.ScreenOn");
            AddCommandButton(
                MiniControlBarCommand.Power,
                MiniControlIcon.Power,
                "MiniBar.Power");
            AddCommandButton(
                MiniControlBarCommand.Fullscreen,
                MiniControlIcon.Fullscreen,
                "MiniBar.Fullscreen");
            AddCommandButton(
                MiniControlBarCommand.ResetSize,
                MiniControlIcon.ResetSize,
                "MiniBar.ResetSize");
            AddCommandButton(
                MiniControlBarCommand.Capture,
                MiniControlIcon.Capture,
                "MiniBar.Capture",
                captureHotkey);
            AddCommandButton(
                MiniControlBarCommand.OpenManager,
                MiniControlIcon.Manager,
                "MiniBar.OpenManager");

            _collapseButton = CreateButton(
                MiniControlIcon.Collapse,
                "MiniBar.Collapse");
            _collapseButton.Click += delegate
            {
                SetCollapsed(!_collapsed);
            };
            Controls.Add(_collapseButton);

            ApplyTheme(theme);
            LayoutButtons();
        }

        internal event EventHandler<MiniControlBarCommandEventArgs>
            CommandRequested;

        internal IntPtr TargetHandle
        {
            get { return _targetHandle; }
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

        internal void ApplyTheme(AppTheme theme)
        {
            _theme = ThemeColors.Resolve(theme);
            BackColor = _theme.CardBackground;
            foreach (var button in _commandButtons)
                button.ApplyTheme(_theme);
            if (_collapseButton != null)
                _collapseButton.ApplyTheme(_theme);
            Invalidate();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmMouseActivate)
            {
                message.Result = new IntPtr(MaNoActivate);
                return;
            }
            base.WndProc(ref message);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(_theme.CardBorder))
            using (var path = CreateRoundedRectangle(
                new RectangleF(
                    0.5F,
                    0.5F,
                    ClientSize.Width - 1F,
                    ClientSize.Height - 1F),
                CornerRadius))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            using (var path = CreateRoundedRectangle(
                new RectangleF(
                    0,
                    0,
                    ClientSize.Width,
                    ClientSize.Height),
                CornerRadius))
            {
                var previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
        }

        private void AddCommandButton(
            MiniControlBarCommand command,
            MiniControlIcon icon,
            string toolTipKey,
            params object[] toolTipValues)
        {
            var button = CreateButton(
                icon,
                toolTipKey,
                toolTipValues);
            button.Click += delegate
            {
                var handler = CommandRequested;
                if (handler != null)
                {
                    handler(
                        this,
                        new MiniControlBarCommandEventArgs(
                            command,
                            _targetHandle));
                }
            };
            _commandButtons.Add(button);
            Controls.Add(button);
        }

        private MiniControlButton CreateButton(
            MiniControlIcon icon,
            string toolTipKey,
            params object[] toolTipValues)
        {
            var button = new MiniControlButton(icon)
            {
                Size = new Size(ButtonSize, ButtonSize),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _toolTip.SetToolTip(
                button,
                toolTipValues != null && toolTipValues.Length > 0
                    ? LocalizationService.Format(
                        toolTipKey,
                        toolTipValues)
                    : LocalizationService.Get(toolTipKey));
            return button;
        }

        private void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            foreach (var button in _commandButtons)
                button.Visible = !collapsed;
            _collapseButton.Icon = collapsed
                ? MiniControlIcon.Expand
                : MiniControlIcon.Collapse;
            _toolTip.SetToolTip(
                _collapseButton,
                LocalizationService.Get(
                    collapsed
                        ? "MiniBar.Expand"
                        : "MiniBar.Collapse"));
            LayoutButtons();
        }

        private void LayoutButtons()
        {
            var top = BarPadding;
            if (!_collapsed)
            {
                foreach (var button in _commandButtons)
                {
                    button.Location = new Point(BarPadding, top);
                    top += ButtonSize + ButtonGap;
                }
            }
            _collapseButton.Location = new Point(BarPadding, top);
            top += ButtonSize + BarPadding;
            ClientSize = new Size(BarWidth, top);
        }

        private static GraphicsPath CreateRoundedRectangle(
            RectangleF bounds,
            float radius)
        {
            var diameter = Math.Min(
                radius * 2F,
                Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            var arc = new RectangleF(
                bounds.Left,
                bounds.Top,
                diameter,
                diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal enum MiniControlIcon
    {
        ScreenOff,
        ScreenOn,
        Power,
        Fullscreen,
        ResetSize,
        Capture,
        Manager,
        Collapse,
        Expand
    }

    internal sealed class MiniControlButton : Control
    {
        private ThemePalette _theme;
        private bool _hovered;
        private bool _pressed;
        private MiniControlIcon _icon;

        internal MiniControlButton(MiniControlIcon icon)
        {
            _icon = icon;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        internal MiniControlIcon Icon
        {
            get { return _icon; }
            set
            {
                _icon = value;
                Invalidate();
            }
        }

        internal void ApplyTheme(ThemePalette theme)
        {
            _theme = theme;
            BackColor = theme.CardBackground;
            ForeColor = theme.TextSecondary;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = _pressed
                ? _theme.AccentSoft
                : _hovered
                    ? _theme.CardSoft
                    : _theme.CardBackground;
            using (var brush = new SolidBrush(fill))
                e.Graphics.FillRectangle(brush, ClientRectangle);

            var color = _hovered
                ? _theme.Accent
                : _theme.TextSecondary;
            using (var pen = new Pen(color, 1.8F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                DrawIcon(e.Graphics, pen, color);
            }
        }

        private void DrawIcon(Graphics graphics, Pen pen, Color color)
        {
            var cx = ClientSize.Width / 2F;
            var cy = ClientSize.Height / 2F;
            switch (_icon)
            {
                case MiniControlIcon.ScreenOff:
                case MiniControlIcon.ScreenOn:
                    graphics.DrawRectangle(pen, cx - 7, cy - 11, 14, 22);
                    graphics.DrawLine(pen, cx - 3, cy + 7, cx + 3, cy + 7);
                    if (_icon == MiniControlIcon.ScreenOff)
                    {
                        graphics.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                        graphics.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                    }
                    else
                    {
                        using (var brush = new SolidBrush(color))
                            graphics.FillEllipse(brush, cx - 3, cy - 4, 6, 6);
                    }
                    break;
                case MiniControlIcon.Power:
                    graphics.DrawArc(pen, cx - 9, cy - 8, 18, 18, -55, 290);
                    graphics.DrawLine(pen, cx, cy - 12, cx, cy - 1);
                    break;
                case MiniControlIcon.Fullscreen:
                    DrawCorner(graphics, pen, cx - 9, cy - 9, 1, 1);
                    DrawCorner(graphics, pen, cx + 9, cy - 9, -1, 1);
                    DrawCorner(graphics, pen, cx - 9, cy + 9, 1, -1);
                    DrawCorner(graphics, pen, cx + 9, cy + 9, -1, -1);
                    break;
                case MiniControlIcon.ResetSize:
                    graphics.DrawRectangle(pen, cx - 9, cy - 7, 18, 14);
                    graphics.DrawLine(pen, cx - 5, cy - 3, cx + 5, cy + 3);
                    graphics.DrawLine(pen, cx + 2, cy + 3, cx + 5, cy + 3);
                    graphics.DrawLine(pen, cx + 5, cy, cx + 5, cy + 3);
                    break;
                case MiniControlIcon.Capture:
                    graphics.DrawRectangle(pen, cx - 10, cy - 7, 20, 15);
                    graphics.DrawRectangle(pen, cx - 4, cy - 10, 8, 3);
                    graphics.DrawEllipse(pen, cx - 4, cy - 4, 8, 8);
                    break;
                case MiniControlIcon.Manager:
                    using (var font = UiFonts.Create(8.5F, FontStyle.Bold))
                    using (var brush = new SolidBrush(color))
                    {
                        var text = "DX";
                        var size = graphics.MeasureString(text, font);
                        graphics.DrawString(
                            text,
                            font,
                            brush,
                            cx - size.Width / 2F,
                            cy - size.Height / 2F);
                    }
                    break;
                case MiniControlIcon.Collapse:
                    graphics.DrawLine(pen, cx - 7, cy + 4, cx, cy - 3);
                    graphics.DrawLine(pen, cx, cy - 3, cx + 7, cy + 4);
                    break;
                case MiniControlIcon.Expand:
                    graphics.DrawLine(pen, cx - 7, cy - 4, cx, cy + 3);
                    graphics.DrawLine(pen, cx, cy + 3, cx + 7, cy - 4);
                    break;
            }
        }

        private static void DrawCorner(
            Graphics graphics,
            Pen pen,
            float x,
            float y,
            int horizontal,
            int vertical)
        {
            graphics.DrawLine(pen, x, y, x + horizontal * 6, y);
            graphics.DrawLine(pen, x, y, x, y + vertical * 6);
        }
    }
}

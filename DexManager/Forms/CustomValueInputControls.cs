using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal sealed class ThemedHotkeyControl : ThemedPaintedTextControl
    {
        private string _valueBeforeCapture = string.Empty;

        public ThemedHotkeyControl()
        {
            MaxLength = 80;
        }

        protected override void OnEnter(EventArgs e)
        {
            _valueBeforeCapture = Text;
            base.OnEnter(e);
        }

        protected override bool ProcessCmdKey(
            ref Message message,
            Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Tab)
                return base.ProcessCmdKey(ref message, keyData);

            if (key == Keys.Escape)
            {
                Text = _valueBeforeCapture;
                SelectAll();
                return true;
            }

            if (key == Keys.Delete || key == Keys.Back)
            {
                Text = string.Empty;
                SelectAll();
                return true;
            }

            if (IsModifierKey(key))
                return true;

            Text = BuildShortcut(key, keyData);
            _valueBeforeCapture = Text;
            SelectAll();
            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private static string BuildShortcut(Keys key, Keys keyData)
        {
            var parts = new List<string>();
            AppendModifier(
                parts,
                NativeMethods.VkLControl,
                NativeMethods.VkRControl,
                (keyData & Keys.Control) == Keys.Control,
                "LeftCtrl",
                "RightCtrl",
                "Ctrl");
            AppendModifier(
                parts,
                NativeMethods.VkLMenu,
                NativeMethods.VkRMenu,
                (keyData & Keys.Alt) == Keys.Alt,
                "LeftAlt",
                "RightAlt",
                "Alt");
            AppendModifier(
                parts,
                NativeMethods.VkLShift,
                NativeMethods.VkRShift,
                (keyData & Keys.Shift) == Keys.Shift,
                "LeftShift",
                "RightShift",
                "Shift");

            if (IsDown(Keys.LWin)) parts.Add("LeftWindows");
            if (IsDown(Keys.RWin)) parts.Add("RightWindows");
            parts.Add(key.ToString());
            return string.Join("+", parts.ToArray());
        }

        private static void AppendModifier(
            ICollection<string> parts,
            int leftKey,
            int rightKey,
            bool genericDown,
            string leftName,
            string rightName,
            string genericName)
        {
            var leftDown = IsDown((Keys)leftKey);
            var rightDown = IsDown((Keys)rightKey);
            if (leftDown) parts.Add(leftName);
            if (rightDown) parts.Add(rightName);
            if (!leftDown && !rightDown && genericDown)
                parts.Add(genericName);
        }

        private static bool IsDown(Keys key)
        {
            return (NativeMethods.GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.ShiftKey ||
                key == Keys.LShiftKey ||
                key == Keys.RShiftKey ||
                key == Keys.ControlKey ||
                key == Keys.LControlKey ||
                key == Keys.RControlKey ||
                key == Keys.Menu ||
                key == Keys.LMenu ||
                key == Keys.RMenu ||
                key == Keys.LWin ||
                key == Keys.RWin;
        }
    }

    internal sealed class ThemedNumberControl : ThemedPaintedTextControl
    {
        private decimal _value;
        private decimal _valueBeforeEdit;
        private bool _updatingText;

        public ThemedNumberControl()
        {
            Minimum = 0;
            Maximum = 100;
            Increment = 1;
            ShowStepButtons = true;
            MaxLength = 8;
            Value = 0;
        }

        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
        public decimal Increment { get; set; }
        public bool ShowStepButtons { get; set; }
        public bool RestorePreviousValueOnMinimumReject { get; set; }
        public bool RestorePreviousValueOnMaximumReject { get; set; }

        public decimal Value
        {
            get { return _value; }
            set
            {
                var normalized = Math.Max(Minimum, Math.Min(Maximum, value));
                if (_value == normalized && Text == normalized.ToString()) return;
                _value = normalized;
                _updatingText = true;
                Text = normalized.ToString();
                _updatingText = false;
                var handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler ValueChanged;
        public event EventHandler MinimumValueRejected;
        public event EventHandler MaximumValueRejected;

        protected override bool AcceptCharacter(char value)
        {
            return char.IsDigit(value);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_updatingText) return;
            decimal parsed;
            if (!decimal.TryParse(Text, out parsed)) return;
            if ((parsed < Minimum && RestorePreviousValueOnMinimumReject) ||
                (parsed > Maximum && RestorePreviousValueOnMaximumReject))
            {
                return;
            }
            parsed = Math.Max(Minimum, Math.Min(Maximum, parsed));
            if (_value == parsed) return;
            _value = parsed;
            var handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnEnter(EventArgs e)
        {
            _valueBeforeEdit = _value;
            base.OnEnter(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            CommitTextValue();
            base.OnLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (ShowStepButtons && e.X >= Width - 30)
            {
                Focus();
                Value += e.Y < Height / 2 ? Increment : -Increment;
                _valueBeforeEdit = _value;
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = ShowStepButtons && e.X >= Width - 30
                ? Cursors.Default
                : Cursors.IBeam;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Cursor = Cursors.IBeam;
            base.OnMouseLeave(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitTextValue();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                Value += Increment;
                _valueBeforeEdit = _value;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                Value -= Increment;
                _valueBeforeEdit = _value;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        private void CommitTextValue()
        {
            decimal parsed;
            var hasParsedValue = decimal.TryParse(Text, out parsed);
            var belowMinimum = hasParsedValue && parsed < Minimum;
            var aboveMaximum = hasParsedValue && parsed > Maximum;
            var restorePrevious =
                (belowMinimum && RestorePreviousValueOnMinimumReject) ||
                (aboveMaximum && RestorePreviousValueOnMaximumReject);
            Value = restorePrevious
                ? _valueBeforeEdit
                : hasParsedValue ? parsed : Minimum;

            if (!belowMinimum && !aboveMaximum)
                _valueBeforeEdit = _value;

            if (belowMinimum)
            {
                var minimumHandler = MinimumValueRejected;
                if (minimumHandler != null)
                    minimumHandler(this, EventArgs.Empty);
            }
            if (aboveMaximum)
            {
                var maximumHandler = MaximumValueRejected;
                if (maximumHandler != null)
                    maximumHandler(this, EventArgs.Empty);
            }
        }

        protected override void DrawAdornment(Graphics graphics)
        {
            if (!ShowStepButtons) return;
            var colors = ThemeColors.Current;
            using (var pen = new Pen(
                Enabled ? colors.TextTertiary : colors.DisabledText,
                1.4F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                DrawChevron(graphics, pen, Width - 17, Height / 2 - 5, true);
                DrawChevron(graphics, pen, Width - 17, Height / 2 + 5, false);
            }
        }

        private static void DrawChevron(
            Graphics graphics,
            Pen pen,
            int centerX,
            int centerY,
            bool up)
        {
            var direction = up ? -1 : 1;
            graphics.DrawLines(pen, new[]
            {
                new Point(centerX - 3, centerY - direction),
                new Point(centerX, centerY + (2 * direction)),
                new Point(centerX + 3, centerY - direction)
            });
        }
    }

}

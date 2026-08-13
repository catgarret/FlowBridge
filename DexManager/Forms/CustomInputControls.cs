using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal sealed class ThemedSelectControl : Control
    {
        private int _selectedIndex = -1;
        private ThemedDropDownForm _dropDown;

        public ThemedSelectControl()
        {
            Items = new List<object>();
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = UiFonts.Create(9.5F);
            Size = new Size(200, 32);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
        }

        public IList<object> Items { get; private set; }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                var normalized = value >= 0 && value < Items.Count ? value : -1;
                if (_selectedIndex == normalized) return;
                _selectedIndex = normalized;
                Invalidate();
                OnSelectedIndexChanged(EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get
            {
                return _selectedIndex >= 0 && _selectedIndex < Items.Count
                    ? Items[_selectedIndex]
                    : null;
            }
            set
            {
                var index = -1;
                for (var i = 0; i < Items.Count; i++)
                {
                    if (ReferenceEquals(Items[i], value) ||
                        Equals(Items[i], value))
                    {
                        index = i;
                        break;
                    }
                }
                SelectedIndex = index;
            }
        }

        public event EventHandler SelectedIndexChanged;
        public event EventHandler SelectionChangeCommitted;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            ShowDropDown();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.KeyCode == Keys.Enter ||
                e.KeyCode == Keys.Space ||
                (e.Alt && e.KeyCode == Keys.Down))
            {
                ShowDropDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && Items.Count > 0)
            {
                CommitSelection(Math.Min(Items.Count - 1, SelectedIndex + 1));
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && Items.Count > 0)
            {
                CommitSelection(Math.Max(0, SelectedIndex - 1));
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Up ||
                key == Keys.Down ||
                key == Keys.Enter ||
                key == Keys.Space)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnEnter(EventArgs e)
        {
            Invalidate();
            base.OnEnter(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            Invalidate();
            base.OnLeave(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawFieldBackground(e.Graphics, ClientRectangle, Focused, Enabled);

            var text = SelectedItem == null
                ? string.Empty
                : SelectedItem.ToString();
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Font,
                new Rectangle(10, 0, Math.Max(Width - 42, 0), Height),
                Enabled ? colors.TextPrimary : colors.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            using (var pen = new Pen(
                Enabled ? colors.TextTertiary : colors.DisabledText,
                1.4F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                var centerX = Width - 17;
                var centerY = Height / 2;
                e.Graphics.DrawLines(pen, new[]
                {
                    new Point(centerX - 3, centerY - 1),
                    new Point(centerX, centerY + 2),
                    new Point(centerX + 3, centerY - 1)
                });
            }
        }

        private void ShowDropDown()
        {
            if (Items.Count == 0) return;
            if (_dropDown != null && !_dropDown.IsDisposed)
            {
                _dropDown.Close();
                return;
            }

            _dropDown = new ThemedDropDownForm(
                Items,
                SelectedIndex,
                Width);
            _dropDown.ItemSelected += delegate(int index)
            {
                CommitSelection(index);
            };
            _dropDown.FormClosed += delegate
            {
                _dropDown = null;
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)delegate { Focus(); });
            };
            var screenPoint = PointToScreen(new Point(0, Height + 3));
            var workingArea = Screen.FromControl(this).WorkingArea;
            if (screenPoint.Y + _dropDown.Height > workingArea.Bottom)
                screenPoint.Y = PointToScreen(Point.Empty).Y -
                    _dropDown.Height - 3;
            _dropDown.Location = screenPoint;
            _dropDown.Show(FindForm());
        }

        private void CommitSelection(int index)
        {
            SelectedIndex = index;
            var handler = SelectionChangeCommitted;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnSelectedIndexChanged(EventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, e);
        }

        internal static void DrawFieldBackground(
            Graphics graphics,
            Rectangle rectangle,
            bool focused,
            bool enabled)
        {
            var colors = ThemeColors.Current;
            var bounds = rectangle;
            bounds.Width--;
            bounds.Height--;
            var fill = enabled ? colors.CardSoft : colors.DisabledBackground;
            var border = focused ? colors.Accent : colors.ControlBorder;
            using (var path = RoundedPath(bounds, 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ThemedTextControl : UserControl
    {
        private readonly TextBox _editor;
        private readonly ThemedTextDisplay _display;
        private bool _syncingText;
        private bool _selectAllOnFocus;
        private int _maxLength = 256;
        private bool _usePasswordMask;
        private bool _useMiddleEllipsis;

        public ThemedTextControl()
        {
            TabStop = false;
            Cursor = Cursors.IBeam;
            Font = UiFonts.Create(9.5F);
            Size = new Size(200, 32);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;

            _editor = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Multiline = false,
                TabStop = true,
                MaxLength = _maxLength,
                Font = Font
            };
            _editor.TextChanged += Editor_TextChanged;
            _editor.Enter += Editor_Enter;
            _editor.Leave += Editor_Leave;

            _display = new ThemedTextDisplay
            {
                TabStop = false,
                Cursor = Cursors.IBeam,
                Font = Font
            };
            _display.MouseDown += Display_MouseDown;

            Controls.Add(_editor);
            Controls.Add(_display);
            LayoutChildren();
            ApplyTheme();
        }

        public int MaxLength
        {
            get { return _maxLength; }
            set
            {
                _maxLength = Math.Max(0, value);
                _editor.MaxLength = _maxLength;
            }
        }

        public bool UsePasswordMask
        {
            get { return _usePasswordMask; }
            set
            {
                _usePasswordMask = value;
                _editor.UseSystemPasswordChar = value;
                _display.UsePasswordMask = value;
                _display.Invalidate();
            }
        }

        public bool UseMiddleEllipsis
        {
            get { return _useMiddleEllipsis; }
            set
            {
                _useMiddleEllipsis = value;
                _display.UseMiddleEllipsis = value;
                _display.Invalidate();
            }
        }

        public void SelectAll()
        {
            _editor.Focus();
            _editor.SelectAll();
        }

        public void Clear()
        {
            _editor.Clear();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (!_syncingText && _editor != null &&
                !string.Equals(_editor.Text, base.Text, StringComparison.Ordinal))
            {
                _syncingText = true;
                _editor.Text = base.Text ?? string.Empty;
                _syncingText = false;
            }

            if (_display != null)
            {
                _display.DisplayText = base.Text ?? string.Empty;
                _display.Invalidate();
            }
            base.OnTextChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_editor != null) _editor.Font = Font;
            if (_display != null) _display.Font = Font;
            LayoutChildren();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _editor.Enabled = Enabled;
            _display.Enabled = Enabled;
            ApplyTheme();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            _selectAllOnFocus = true;
            _editor.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ApplyTheme();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            ThemedSelectControl.DrawFieldBackground(
                e.Graphics,
                ClientRectangle,
                _editor.Focused,
                Enabled);
            base.OnPaint(e);
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (_syncingText) return;
            _syncingText = true;
            base.Text = _editor.Text;
            _syncingText = false;
        }

        private void Editor_Enter(object sender, EventArgs e)
        {
            _display.Visible = false;
            _selectAllOnFocus = true;
            BeginInvoke((Action)delegate
            {
                if (IsDisposed || !_editor.Focused || !_selectAllOnFocus) return;
                _editor.SelectAll();
                _selectAllOnFocus = false;
            });
            Invalidate();
        }

        private void Editor_Leave(object sender, EventArgs e)
        {
            _selectAllOnFocus = false;
            _display.Visible = true;
            _display.BringToFront();
            Invalidate();
        }

        private void Display_MouseDown(object sender, MouseEventArgs e)
        {
            if (!Enabled || e.Button != MouseButtons.Left) return;
            _selectAllOnFocus = true;
            _display.Visible = false;
            _editor.Focus();
        }

        private void LayoutChildren()
        {
            if (_editor == null || _display == null) return;

            var editorHeight = _editor.PreferredHeight;
            _editor.SetBounds(
                10,
                Math.Max((Height - editorHeight) / 2, 1),
                Math.Max(Width - 20, 0),
                editorHeight);
            _display.SetBounds(
                10,
                1,
                Math.Max(Width - 20, 0),
                Math.Max(Height - 2, 0));
        }

        private void ApplyTheme()
        {
            if (_editor == null || _display == null) return;

            var colors = ThemeColors.Current;
            var background = Enabled
                ? colors.CardSoft
                : colors.DisabledBackground;
            var foreground = Enabled
                ? colors.TextPrimary
                : colors.DisabledText;
            _editor.BackColor = background;
            _editor.ForeColor = foreground;
            _display.BackColor = background;
            _display.ForeColor = foreground;
        }

        private sealed class ThemedTextDisplay : Control
        {
            public string DisplayText { get; set; }
            public bool UsePasswordMask { get; set; }
            public bool UseMiddleEllipsis { get; set; }

            public ThemedTextDisplay()
            {
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var value = DisplayText ?? string.Empty;
                if (UsePasswordMask)
                    value = new string('\u2022', value.Length);

                var flags = TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix |
                    (UseMiddleEllipsis
                        ? TextFormatFlags.PathEllipsis
                        : TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(
                    e.Graphics,
                    value,
                    Font,
                    ClientRectangle,
                    ForeColor,
                    BackColor,
                    flags);
            }
        }
    }

    internal class ThemedPaintedTextControl : Control
    {
        private int _selectionStart;
        private int _selectionLength;

        public ThemedPaintedTextControl()
        {
            TabStop = true;
            Cursor = Cursors.IBeam;
            Font = UiFonts.Create(9.5F);
            Size = new Size(200, 32);
            MaxLength = 256;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
        }

        public int MaxLength { get; set; }
        public bool UsePasswordMask { get; set; }
        public bool UseMiddleEllipsis { get; set; }

        public void SelectAll()
        {
            _selectionStart = 0;
            _selectionLength = Text.Length;
            Invalidate();
        }

        public void Clear()
        {
            Text = string.Empty;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            _selectionStart = Math.Min(_selectionStart, Text.Length);
            _selectionLength = Math.Min(
                _selectionLength,
                Text.Length - _selectionStart);
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnEnter(EventArgs e)
        {
            SelectAll();
            base.OnEnter(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            _selectionLength = 0;
            Invalidate();
            base.OnLeave(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            var alreadyFocused = Focused;
            Focus();
            if (!alreadyFocused)
            {
                SelectAll();
                return;
            }
            _selectionStart = FindCharacterIndex(e.X - 10);
            _selectionLength = 0;
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!Enabled || char.IsControl(e.KeyChar))
            {
                base.OnKeyPress(e);
                return;
            }
            if (!AcceptCharacter(e.KeyChar) ||
                Text.Length - _selectionLength >= MaxLength)
            {
                e.Handled = true;
                return;
            }
            ReplaceSelection(e.KeyChar.ToString());
            e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelection();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                PasteText();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                Backspace();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                Delete();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                MoveCaret(-1);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                MoveCaret(1);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                _selectionStart = 0;
                _selectionLength = 0;
                Invalidate();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                _selectionStart = Text.Length;
                _selectionLength = 0;
                Invalidate();
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Left ||
                key == Keys.Right ||
                key == Keys.Up ||
                key == Keys.Down ||
                key == Keys.Home ||
                key == Keys.End ||
                key == Keys.Delete)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            ThemedSelectControl.DrawFieldBackground(
                e.Graphics,
                ClientRectangle,
                Focused,
                Enabled);

            var value = Text ?? string.Empty;
            var displayValue = UsePasswordMask
                ? new string('\u2022', value.Length)
                : value;
            var selectionStart = Math.Max(
                0,
                Math.Min(_selectionStart, value.Length));
            var selectionLength = Math.Max(
                0,
                Math.Min(_selectionLength, value.Length - selectionStart));
            var textY = (Height - Font.Height) / 2;
            var textColor = Enabled ? colors.TextPrimary : colors.DisabledText;
            using (var format = CreateTextFormat())
            {
                if (UseMiddleEllipsis && !Focused)
                {
                    displayValue = FitMiddleEllipsis(
                        e.Graphics,
                        displayValue,
                        Math.Max(Width - 20, 0),
                        format);
                }
                DrawText(
                    e.Graphics,
                    displayValue,
                    10,
                    textY,
                    textColor,
                    format);

                if (Focused && selectionLength > 0)
                {
                    var prefix = displayValue.Substring(0, selectionStart);
                    var selected = displayValue.Substring(
                        selectionStart,
                        selectionLength);
                    var selectionX = 10F + MeasureText(
                        e.Graphics,
                        prefix,
                        format);
                    var selectionWidth = Math.Max(
                        MeasureText(e.Graphics, selected, format),
                        2F);
                    var selectionBounds = new RectangleF(
                        selectionX,
                        textY - 2,
                        selectionWidth,
                        Font.Height + 4);
                    using (var brush = new SolidBrush(colors.Accent))
                        e.Graphics.FillRectangle(brush, selectionBounds);
                    DrawText(
                        e.Graphics,
                        selected,
                        selectionX,
                        textY,
                        Color.White,
                        format);
                }
                else if (Focused)
                {
                    var caretX = 10F + MeasureText(
                        e.Graphics,
                        displayValue.Substring(0, selectionStart),
                        format);
                    using (var pen = new Pen(colors.TextPrimary))
                        e.Graphics.DrawLine(
                            pen,
                            caretX,
                            textY - 1,
                            caretX,
                            textY + Font.Height + 1);
                }
            }

            DrawAdornment(e.Graphics);
        }

        private string FitMiddleEllipsis(
            Graphics graphics,
            string value,
            float maximumWidth,
            StringFormat format)
        {
            if (string.IsNullOrEmpty(value) ||
                MeasureText(graphics, value, format) <= maximumWidth)
            {
                return value;
            }

            const string ellipsis = "...";
            var low = 0;
            var high = value.Length;
            var best = ellipsis;
            while (low <= high)
            {
                var keep = (low + high) / 2;
                var left = (keep + 1) / 2;
                var right = keep / 2;
                var candidate = value.Substring(0, left) +
                    ellipsis +
                    value.Substring(value.Length - right);
                if (MeasureText(graphics, candidate, format) <= maximumWidth)
                {
                    best = candidate;
                    low = keep + 1;
                }
                else
                {
                    high = keep - 1;
                }
            }
            return best;
        }

        protected virtual bool AcceptCharacter(char value)
        {
            return !char.IsControl(value);
        }

        protected virtual void DrawAdornment(Graphics graphics)
        {
        }

        protected void ReplaceSelection(string value)
        {
            var filtered = FilterText(value);
            if (string.IsNullOrEmpty(filtered)) return;
            var available = MaxLength - (Text.Length - _selectionLength);
            if (available <= 0) return;
            if (filtered.Length > available)
                filtered = filtered.Substring(0, available);
            Text = Text.Remove(_selectionStart, _selectionLength)
                .Insert(_selectionStart, filtered);
            _selectionStart += filtered.Length;
            _selectionLength = 0;
            Invalidate();
        }

        protected virtual string FilterText(string value)
        {
            var result = string.Empty;
            foreach (var character in value ?? string.Empty)
            {
                if (AcceptCharacter(character))
                    result += character;
            }
            return result;
        }

        private void Backspace()
        {
            if (_selectionLength > 0)
            {
                ReplaceSelectionWithEmpty();
                return;
            }
            if (_selectionStart <= 0) return;
            _selectionStart--;
            Text = Text.Remove(_selectionStart, 1);
            Invalidate();
        }

        private void Delete()
        {
            if (_selectionLength > 0)
            {
                ReplaceSelectionWithEmpty();
                return;
            }
            if (_selectionStart >= Text.Length) return;
            Text = Text.Remove(_selectionStart, 1);
            Invalidate();
        }

        private void ReplaceSelectionWithEmpty()
        {
            Text = Text.Remove(_selectionStart, _selectionLength);
            _selectionLength = 0;
            Invalidate();
        }

        private void MoveCaret(int offset)
        {
            if (_selectionLength > 0)
            {
                _selectionStart = offset < 0
                    ? _selectionStart
                    : _selectionStart + _selectionLength;
                _selectionLength = 0;
            }
            else
            {
                _selectionStart = Math.Max(
                    0,
                    Math.Min(Text.Length, _selectionStart + offset));
            }
            Invalidate();
        }

        private void CopySelection()
        {
            if (_selectionLength <= 0) return;
            try
            {
                Clipboard.SetText(Text.Substring(
                    _selectionStart,
                    _selectionLength));
            }
            catch
            {
            }
        }

        private void PasteText()
        {
            try
            {
                if (Clipboard.ContainsText())
                    ReplaceSelection(Clipboard.GetText());
            }
            catch
            {
            }
        }

        private int FindCharacterIndex(int targetX)
        {
            if (targetX <= 0) return 0;
            using (var graphics = CreateGraphics())
            using (var format = CreateTextFormat())
            {
                var previousWidth = 0F;
                for (var index = 1; index <= Text.Length; index++)
                {
                    var currentWidth = MeasureText(
                        graphics,
                        Text.Substring(0, index),
                        format);
                    if (targetX < (previousWidth + currentWidth) / 2F)
                        return index - 1;
                    previousWidth = currentWidth;
                }
            }
            return Text.Length;
        }

        private float MeasureText(
            Graphics graphics,
            string value,
            StringFormat format)
        {
            if (string.IsNullOrEmpty(value)) return 0F;
            return graphics.MeasureString(
                value,
                Font,
                int.MaxValue,
                format).Width;
        }

        private void DrawText(
            Graphics graphics,
            string value,
            float x,
            float y,
            Color color,
            StringFormat format)
        {
            using (var brush = new SolidBrush(color))
                graphics.DrawString(
                    value ?? string.Empty,
                    Font,
                    brush,
                    new PointF(x, y),
                    format);
        }

        private static StringFormat CreateTextFormat()
        {
            var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces |
                StringFormatFlags.NoWrap;
            return format;
        }
    }

}

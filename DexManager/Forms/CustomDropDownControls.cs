using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal sealed class ThemedDropDownForm : Form
    {
        public ThemedDropDownForm(
            IList<object> items,
            int selectedIndex,
            int width)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = ThemeColors.Current.CardBackground;

            var list = new ThemedDropDownList(items, selectedIndex)
            {
                Dock = DockStyle.Fill
            };
            list.ItemCommitted += delegate(int index)
            {
                SelectedIndex = index;
                var handler = ItemSelected;
                if (handler != null) handler(index);
                Close();
            };
            Controls.Add(list);
            ClientSize = new Size(
                Math.Max(width, 120),
                Math.Min(items.Count, 8) * 30 + 2);
            Shown += delegate { list.Focus(); };
            Deactivate += delegate { Close(); };
        }

        public int SelectedIndex { get; private set; }
        public event Action<int> ItemSelected;

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }
    }

    internal sealed class ThemedDropDownList : Control
    {
        private const int ItemHeight = 30;
        private readonly IList<object> _items;
        private int _hoverIndex;
        private int _topIndex;

        public ThemedDropDownList(IList<object> items, int selectedIndex)
        {
            _items = items;
            _hoverIndex = selectedIndex >= 0
                ? selectedIndex
                : (items.Count > 0 ? 0 : -1);
            _topIndex = Math.Max(0, _hoverIndex - 3);
            TabStop = true;
            Font = UiFonts.Create(9.5F);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);
        }

        public event Action<int> ItemCommitted;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                _hoverIndex = Math.Min(_items.Count - 1, _hoverIndex + 1);
                EnsureHoverVisible();
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                _hoverIndex = Math.Max(0, _hoverIndex - 1);
                EnsureHoverVisible();
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                CommitHoveredItem();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                var form = FindForm();
                if (form != null) form.Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_items.Count == 0)
            {
                base.OnMouseMove(e);
                return;
            }
            var row = Math.Max(
                0,
                Math.Min(
                    VisibleRowCount - 1,
                    Math.Max(e.Y - 1, 0) / ItemHeight));
            var index = Math.Min(_items.Count - 1, _topIndex + row);
            if (_hoverIndex != index)
            {
                _hoverIndex = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left &&
                _hoverIndex >= 0 &&
                _hoverIndex < _items.Count)
            {
                CommitHoveredItem();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_items.Count <= VisibleRowCount)
            {
                base.OnMouseWheel(e);
                return;
            }
            var direction = e.Delta > 0 ? -1 : 1;
            _topIndex = Math.Max(
                0,
                Math.Min(MaxTopIndex, _topIndex + direction * 3));
            _hoverIndex = Math.Max(
                _topIndex,
                Math.Min(
                    _topIndex + VisibleRowCount - 1,
                    _hoverIndex));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e)
        {
            EnsureHoverVisible();
            base.OnResize(e);
        }

        private void CommitHoveredItem()
        {
            if (_hoverIndex < 0 || _hoverIndex >= _items.Count) return;
            var handler = ItemCommitted;
            if (handler != null) handler(_hoverIndex);
        }

        private int VisibleRowCount
        {
            get { return Math.Max(1, Height / ItemHeight); }
        }

        private int MaxTopIndex
        {
            get { return Math.Max(0, _items.Count - VisibleRowCount); }
        }

        private void EnsureHoverVisible()
        {
            _topIndex = Math.Max(0, Math.Min(MaxTopIndex, _topIndex));
            if (_hoverIndex < 0) return;
            if (_hoverIndex < _topIndex)
                _topIndex = _hoverIndex;
            else if (_hoverIndex >= _topIndex + VisibleRowCount)
                _topIndex = _hoverIndex - VisibleRowCount + 1;
            _topIndex = Math.Max(0, Math.Min(MaxTopIndex, _topIndex));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.Clear(colors.CardBackground);
            var lastIndex = Math.Min(
                _items.Count,
                _topIndex + VisibleRowCount);
            for (var index = _topIndex; index < lastIndex; index++)
            {
                var row = index - _topIndex;
                var bounds = new Rectangle(
                    1,
                    1 + row * ItemHeight,
                    Width - 2,
                    ItemHeight);
                if (index == _hoverIndex)
                {
                    using (var brush = new SolidBrush(colors.AccentSoft))
                        e.Graphics.FillRectangle(brush, bounds);
                }
                TextRenderer.DrawText(
                    e.Graphics,
                    _items[index] == null ? string.Empty : _items[index].ToString(),
                    Font,
                    new Rectangle(10, bounds.Top, bounds.Width - 20, bounds.Height),
                    colors.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            if (_items.Count > VisibleRowCount)
            {
                var trackHeight = Math.Max(Height - 8, 1);
                var thumbHeight = Math.Max(
                    24,
                    trackHeight * VisibleRowCount / _items.Count);
                var travel = Math.Max(trackHeight - thumbHeight, 0);
                var thumbTop = 4 + (MaxTopIndex == 0
                    ? 0
                    : travel * _topIndex / MaxTopIndex);
                using (var brush = new SolidBrush(colors.ControlBorder))
                {
                    e.Graphics.FillRectangle(
                        brush,
                        Width - 5,
                        thumbTop,
                        2,
                        thumbHeight);
                }
            }
            using (var pen = new Pen(colors.ControlBorder))
            {
                var border = ClientRectangle;
                border.Width--;
                border.Height--;
                e.Graphics.DrawRectangle(pen, border);
            }
        }
    }
}


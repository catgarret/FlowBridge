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
        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != WmMouseWheel ||
                !Visible ||
                _contentHost == null ||
                !_contentHost.RectangleToScreen(
                    _contentHost.ClientRectangle).Contains(Cursor.Position))
            {
                return false;
            }

            ScrollableControl page = null;
            foreach (var candidate in _pages)
            {
                if (candidate.Visible)
                {
                    page = candidate as ScrollableControl;
                    break;
                }
            }

            if (page == null || !page.VerticalScroll.Visible)
                return false;

            var delta = unchecked((short)(
                ((long)message.WParam >> 16) & 0xFFFF));
            if (delta == 0) return false;

            var maximum = Math.Max(
                0,
                page.VerticalScroll.Maximum -
                page.VerticalScroll.LargeChange + 1);
            var current = Math.Max(
                0,
                Math.Min(maximum, -page.AutoScrollPosition.Y));
            var lines = SystemInformation.MouseWheelScrollLines;
            var step = lines < 0
                ? Math.Max(1, page.ClientSize.Height)
                : Math.Max(1, lines) * 16;
            var notches = delta / 120;
            if (notches == 0) notches = Math.Sign(delta);
            var target = Math.Max(
                0,
                Math.Min(maximum, current - notches * step));

            page.AutoScrollPosition = new Point(0, target);
            return true;
        }
    }
}

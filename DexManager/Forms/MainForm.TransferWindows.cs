using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
        private const int TransferWindowGap = 8;
        private const int TransferWindowMargin = 16;

        private IntPtr PositionTransferStatusWindow(
            Form window,
            Form otherWindow,
            IntPtr preferredTarget)
        {
            if (window == null || window.IsDisposed) return IntPtr.Zero;

            var targetHandle = ResolveTransferTarget(preferredTarget);
            Rectangle barBounds;
            Rectangle workArea;
            var x = 0;
            var y = 0;
            if (_miniControlBarManager.TryGetBarBounds(
                    targetHandle,
                    out barBounds) ||
                _miniControlBarManager.TryGetActiveBarBounds(
                    out barBounds))
            {
                workArea = Screen.FromRectangle(barBounds).WorkingArea;
                x = _settings.Features.MiniControlBarSide ==
                    MiniControlBarSide.Left
                    ? barBounds.Right - window.Width
                    : barBounds.Left;
                y = barBounds.Bottom + TransferWindowGap;
            }
            else if (TryPositionBesideTarget(
                window,
                targetHandle,
                out workArea,
                out x,
                out y))
            {
            }
            else
            {
                workArea = Screen.FromControl(this).WorkingArea;
                x = workArea.Right - window.Width -
                    TransferWindowMargin;
                y = workArea.Top + TransferWindowMargin;
            }

            x = Math.Max(
                workArea.Left,
                Math.Min(workArea.Right - window.Width, x));
            y = Math.Max(
                workArea.Top,
                Math.Min(workArea.Bottom - window.Height, y));

            var candidate = new Rectangle(
                x,
                y,
                window.Width,
                window.Height);
            if (otherWindow != null &&
                !otherWindow.IsDisposed &&
                otherWindow.Visible &&
                candidate.IntersectsWith(otherWindow.Bounds))
            {
                var below = otherWindow.Bottom + TransferWindowGap;
                if (below + window.Height <= workArea.Bottom)
                {
                    y = below;
                }
                else
                {
                    y = Math.Max(
                        workArea.Top,
                        otherWindow.Top - window.Height -
                        TransferWindowGap);
                }
            }

            window.Location = new Point(x, y);
            return targetHandle;
        }

        private static void ShowTransferStatusWindow(
            Form window,
            IntPtr targetHandle)
        {
            if (window == null || window.IsDisposed || window.Visible)
                return;
            if (targetHandle != IntPtr.Zero &&
                NativeMethods.IsWindow(targetHandle))
            {
                window.Show(new WindowHandleOwner(targetHandle));
                return;
            }
            window.Show();
        }

        private IntPtr ResolveTransferTarget(IntPtr preferredTarget)
        {
            if (preferredTarget != IntPtr.Zero &&
                NativeMethods.IsWindow(preferredTarget))
            {
                return preferredTarget;
            }

            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero &&
                (foreground == _scrcpyService.MainWindowHandle ||
                 _singleWindowService.ContainsWindowHandle(foreground)))
            {
                return foreground;
            }

            var dexHandle = _scrcpyService.MainWindowHandle;
            if (dexHandle != IntPtr.Zero &&
                NativeMethods.IsWindow(dexHandle))
            {
                return dexHandle;
            }

            foreach (var handle in _singleWindowService.GetWindowHandles())
            {
                if (handle != IntPtr.Zero && NativeMethods.IsWindow(handle))
                    return handle;
            }
            return IntPtr.Zero;
        }

        private static bool TryPositionBesideTarget(
            Form window,
            IntPtr targetHandle,
            out Rectangle workArea,
            out int x,
            out int y)
        {
            workArea = Rectangle.Empty;
            x = 0;
            y = 0;
            NativeRect targetBounds;
            if (targetHandle == IntPtr.Zero ||
                !NativeMethods.IsWindow(targetHandle) ||
                NativeMethods.IsIconic(targetHandle) ||
                !NativeMethods.GetWindowRect(
                    targetHandle,
                    out targetBounds))
            {
                return false;
            }

            workArea = Screen.FromHandle(targetHandle).WorkingArea;
            x = targetBounds.Right + TransferWindowGap;
            y = targetBounds.Top;
            if (x + window.Width > workArea.Right)
                x = targetBounds.Left - window.Width - TransferWindowGap;
            if (x < workArea.Left)
            {
                x = Math.Max(
                    workArea.Left,
                    Math.Min(
                        targetBounds.Left,
                        workArea.Right - window.Width));
                y = targetBounds.Bottom + TransferWindowGap;
            }
            return true;
        }
    }
}

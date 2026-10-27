using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Framelet {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } }
        [StructLayout(LayoutKind.Sequential)] internal struct CURSORINFO { public int Size, Flags; public IntPtr Cursor; public Point Position; }
        [StructLayout(LayoutKind.Sequential)] internal struct ICONINFO { [MarshalAs(UnmanagedType.Bool)] public bool IsIcon; public uint HotspotX, HotspotY; public IntPtr Mask, Color; }
        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lparam);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT rect, int size);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] internal static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] internal static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
        [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CURSORINFO info);
        [DllImport("user32.dll")] internal static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);
        [DllImport("user32.dll")] internal static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int width, int height, int frame, IntPtr brush, uint flags);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr handle);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();

        internal static Rectangle WindowBounds(IntPtr hwnd) {
            RECT rect;
            if (DwmGetWindowAttribute(hwnd, 9, out rect, Marshal.SizeOf(typeof(RECT))) == 0) return rect.ToRectangle();
            if (GetWindowRect(hwnd, out rect)) return rect.ToRectangle();
            return Rectangle.Empty;
        }
        internal static List<WindowTarget> Windows() {
            var list = new List<WindowTarget>();
            uint own = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr hwnd, IntPtr ignored) {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindowTextLength(hwnd) == 0) return true;
                uint pid; GetWindowThreadProcessId(hwnd, out pid); if (pid == own) return true;
                Rectangle r = WindowBounds(hwnd); if (r.Width < 80 || r.Height < 50) return true;
                var text = new StringBuilder(1024); GetWindowText(hwnd, text, text.Capacity);
                if (text.ToString() != "Program Manager") list.Add(new WindowTarget { Handle = hwnd, Title = text.ToString() });
                return true;
            }, IntPtr.Zero);
            return list;
        }
        internal static bool ValidRegion(Rectangle region) {
            return region.Width >= 16 && region.Height >= 16 && SystemInformation.VirtualScreen.Contains(region);
        }
        internal static Point Clamp(Point p, Rectangle r) { return new Point(Math.Max(r.Left, Math.Min(r.Right, p.X)), Math.Max(r.Top, Math.Min(r.Bottom, p.Y))); }
        internal static Rectangle Normalize(Point a, Point b) { return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)); }
        internal static Rectangle MoveWithin(Rectangle r, int dx, int dy, Rectangle bounds) {
            r.X = Math.Max(bounds.Left, Math.Min(bounds.Right - r.Width, r.X + dx));
            r.Y = Math.Max(bounds.Top, Math.Min(bounds.Bottom - r.Height, r.Y + dy)); return r;
        }
        internal static bool Exclude(Form form) {
            try { return SetWindowDisplayAffinity(form.Handle, 0x11); } catch { return false; }
        }
        internal static void Dpi() {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }
    }
    internal sealed class WindowTarget {
        public IntPtr Handle; public string Title;
        public override string ToString() { return Title; }
    }
}

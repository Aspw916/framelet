using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Framelet {
    internal sealed class SelectionOverlay : Form {
        private Bitmap screenshot;
        private Rectangle selected;
        private Point origin;
        private Rectangle original;
        private int drag = -1; // 0 draw; 1 move; 2..9 resize handles
        private readonly Rectangle desktop;
        internal Rectangle SelectedRegion { get { return new Rectangle(desktop.X + selected.X, desktop.Y + selected.Y, selected.Width, selected.Height); } }
        internal SelectionOverlay(Rectangle previous) {
            desktop = SystemInformation.VirtualScreen; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; Bounds = desktop; TopMost = true; ShowInTaskbar = false; KeyPreview = true; DoubleBuffered = true; Cursor = Cursors.Cross; AutoScaleMode = AutoScaleMode.None;
            screenshot = new Bitmap(desktop.Width, desktop.Height);
            using (var g = Graphics.FromImage(screenshot)) g.CopyFromScreen(desktop.Location, Point.Empty, desktop.Size);
            if (Native.ValidRegion(previous)) selected = new Rectangle(previous.X - desktop.X, previous.Y - desktop.Y, previous.Width, previous.Height);
            KeyDown += delegate(object s, KeyEventArgs e) {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                if (e.KeyCode == Keys.Enter && selected.Width >= 16 && selected.Height >= 16) Accept();
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { int n = e.Shift ? 10 : 1; selected = Native.MoveWithin(selected, e.KeyCode == Keys.Left ? -n : e.KeyCode == Keys.Right ? n : 0, e.KeyCode == Keys.Up ? -n : e.KeyCode == Keys.Down ? n : 0, ClientRectangle); Invalidate(); e.Handled = true; }
            };
        }
        private Point[] Handles() { int x = selected.Left, y = selected.Top, r = selected.Right, b = selected.Bottom, mx = x + selected.Width / 2, my = y + selected.Height / 2; return new Point[] { new Point(x,y), new Point(mx,y), new Point(r,y), new Point(r,my), new Point(r,b), new Point(mx,b), new Point(x,b), new Point(x,my) }; }
        private int Hit(Point p) { if (selected.Width < 1) return 0; Point[] hs = Handles(); for (int i = 0; i < hs.Length; i++) if (Math.Abs(p.X - hs[i].X) <= 9 && Math.Abs(p.Y - hs[i].Y) <= 9) return i + 2; return selected.Contains(p) ? 1 : 0; }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; } if (e.Button != MouseButtons.Left) return; drag = Hit(e.Location); origin = e.Location; original = selected; Capture = true; if (drag == 0) selected = new Rectangle(e.Location, Size.Empty); Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e); Point p = Native.Clamp(e.Location, ClientRectangle);
            if (!Capture) { int h = Hit(p); Cursor = h == 1 ? Cursors.SizeAll : h == 2 || h == 6 ? Cursors.SizeNWSE : h == 4 || h == 8 ? Cursors.SizeNESW : h == 3 || h == 7 ? Cursors.SizeNS : h == 5 || h == 9 ? Cursors.SizeWE : Cursors.Cross; return; }
            if (drag == 0) selected = Native.Normalize(origin, p);
            else if (drag == 1) selected = Native.MoveWithin(original, p.X - origin.X, p.Y - origin.Y, ClientRectangle);
            else {
                int l = original.Left, t = original.Top, r = original.Right, b = original.Bottom, h = drag - 2;
                if (h == 0 || h == 6 || h == 7) l = p.X; if (h == 2 || h == 3 || h == 4) r = p.X;
                if (h == 0 || h == 1 || h == 2) t = p.Y; if (h == 4 || h == 5 || h == 6) b = p.Y;
                selected = Native.Normalize(new Point(l, t), new Point(r, b));
            }
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; drag = -1; Invalidate(); }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); if (selected.Contains(e.Location) && selected.Width >= 16 && selected.Height >= 16) Accept(); }
        private void Accept() { DialogResult = DialogResult.OK; Close(); }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.DrawImageUnscaled(screenshot, 0, 0); using (var shade = new SolidBrush(Color.FromArgb(145, 5, 12, 20))) g.FillRectangle(shade, ClientRectangle);
            if (selected.Width > 0 && selected.Height > 0) {
                g.DrawImage(screenshot, selected, selected, GraphicsUnit.Pixel); using (var p = new Pen(Theme.Accent, 2)) g.DrawRectangle(p, selected);
                foreach (Point point in Handles()) { using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, point.X - 4, point.Y - 4, 8, 8); using (var p = new Pen(Theme.Background)) g.DrawRectangle(p, point.X - 4, point.Y - 4, 8, 8); }
            }
            string text = selected.Width >= 16 && selected.Height >= 16 ? selected.Width + " × " + selected.Height + " px   ·   拖动调整   ·   Enter / 双击确认   ·   Esc 取消" : "拖动框选捕获区域   ·   Esc / 右键取消";
            using (var f = Theme.Font(11, FontStyle.Bold)) {
                Size s = TextRenderer.MeasureText(text, f); int w = s.Width + 36, h = 46;
                Screen screen = Screen.FromPoint(Cursor.Position); Rectangle area = screen.Bounds; area.Offset(-desktop.X, -desktop.Y);
                int x = Math.Max(area.Left + 10, Math.Min(area.Right - w - 10, selected.Width > 0 ? selected.X : area.Left + (area.Width - w) / 2));
                int y = selected.Height > 0 ? (selected.Top > area.Top + 62 ? selected.Top - h - 12 : selected.Bottom + 14) : area.Top + 34; y = Math.Max(area.Top + 10, Math.Min(area.Bottom - h - 10, y));
                g.SmoothingMode = SmoothingMode.AntiAlias; using (var p = Theme.Round(new RectangleF(x,y,w,h), 9)) using (var b = new SolidBrush(Theme.Card)) g.FillPath(b,p);
                TextRenderer.DrawText(g, text, f, new Rectangle(x + 12, y, w - 24, h), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing && screenshot != null) { screenshot.Dispose(); screenshot = null; } base.Dispose(disposing); }
    }
}

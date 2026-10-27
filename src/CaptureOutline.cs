using System;
using System.Drawing;
using System.Windows.Forms;

namespace Framelet {
    // A live, hollow, click-through window. Only the boundary and badge exist as
    // window pixels; the selected content stays visible and interactive underneath.
    internal sealed class CaptureOutline : Form {
        private readonly Rectangle captureArea;
        private Rectangle border, hole, badge;
        private int stroke = 3;
        private string state = "准备录制";
        private bool excluded;
        internal Rectangle CaptureArea { get { return captureArea; } }
        internal string BadgeText { get { return state + " · " + captureArea.Width + " × " + captureArea.Height + " px"; } }
        internal bool IsRecording { get; private set; }
        internal CaptureOutline(Rectangle area) {
            captureArea = area;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = TransparencyKey = Color.Fuchsia;
            Font = Theme.Font(9, FontStyle.Bold);
            Bounds = Rectangle.Inflate(area, 6, 6);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleName = "局部录制范围";
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get {
                var p = base.CreateParams;
                p.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000 | 0x00000080; // layered, mouse pass-through, no activation, tool window
                return p;
            }
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            excluded = Native.Exclude(this);
            BuildShape();
        }
        private void BuildShape() {
            float scale;
            Size textSize;
            using(var g=CreateGraphics()) {
                scale = g.DpiX/96;
                textSize = TextRenderer.MeasureText(g, "录制中 00:00 · " + captureArea.Width + " × " + captureArea.Height + " px", Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
            stroke = Math.Max(2, (int)Math.Ceiling(2*scale));
            int gap = Math.Max(4, (int)(5*scale));
            Rectangle desktop = SystemInformation.VirtualScreen;
            Rectangle outer = Rectangle.Inflate(captureArea, stroke, stroke);
            Rectangle inner = captureArea;
            // At a physical screen edge there is no space outside. An inner edge
            // is used only when Windows has accepted exclusion from capture.
            if (excluded) {
                if(captureArea.Left<=desktop.Left) inner.X+=stroke;
                if(captureArea.Top<=desktop.Top) inner.Y+=stroke;
                inner.Width=captureArea.Right-inner.Left-(captureArea.Right>=desktop.Right?stroke:0);
                inner.Height=captureArea.Bottom-inner.Top-(captureArea.Bottom>=desktop.Bottom?stroke:0);
            }
            Rectangle screen = Screen.FromRectangle(captureArea).Bounds;
            int badgeWidth = Math.Min(screen.Width, textSize.Width+(int)(28*scale));
            int badgeHeight = textSize.Height+(int)(14*scale);
            int x = Math.Max(screen.Left, Math.Min(screen.Right-badgeWidth, captureArea.Left));
            Rectangle label = Rectangle.Empty;
            if(captureArea.Top-screen.Top>=badgeHeight+gap+stroke) label=new Rectangle(x,captureArea.Top-stroke-gap-badgeHeight,badgeWidth,badgeHeight);
            else if(screen.Bottom-captureArea.Bottom>=badgeHeight+gap+stroke) label=new Rectangle(x,captureArea.Bottom+stroke+gap,badgeWidth,badgeHeight);
            Rectangle bounds = label.IsEmpty ? outer : Rectangle.Union(outer,label);
            Bounds = bounds;
            border=outer; border.Offset(-bounds.X,-bounds.Y);
            hole=inner; hole.Offset(-bounds.X,-bounds.Y);
            badge=label; if(!badge.IsEmpty)badge.Offset(-bounds.X,-bounds.Y);
            var shape = new Region(border);
            shape.Exclude(hole);
            if(!badge.IsEmpty)shape.Union(badge);
            Region previous=Region;Region=shape;if(previous!=null)previous.Dispose();
        }
        internal void Countdown(int seconds) { IsRecording=false;state=seconds+" 秒后开始";Invalidate(); }
        internal void Recording(double elapsed) { IsRecording=true;state="录制中 "+TimeSpan.FromSeconds(Math.Max(0,elapsed)).ToString(@"mm\:ss");Invalidate(); }
        internal void Finishing() { IsRecording=false;state="正在停止";Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(TransparencyKey);
            Color color=IsRecording?Theme.Accent:Color.FromArgb(255,197,96);
            using(var brush=new SolidBrush(color))e.Graphics.FillRectangle(brush,border);
            if(!badge.IsEmpty) {
                using(var brush=new SolidBrush(Theme.Card))e.Graphics.FillRectangle(brush,badge);
                TextRenderer.DrawText(e.Graphics,"● "+BadgeText,Font,badge,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            }
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x0084){m.Result=new IntPtr(-1);return;} // HTTRANSPARENT
            if(m.Msg==0x0021){m.Result=new IntPtr(3);return;} // MA_NOACTIVATE
            base.WndProc(ref m);
        }
    }
}

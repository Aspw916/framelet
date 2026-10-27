using System;
using System.Drawing;
using System.Windows.Forms;

namespace Framelet {
    internal sealed class RecordingBar : Form {
        private readonly Label time;
        private readonly Label detail;
        private readonly SoftButton stop;
        private readonly bool screenshot;
        internal event EventHandler StopRequested, CancelRequested;
        internal RecordingBar(Rectangle area, bool canHide, bool isScreenshot=false) {
            screenshot=isScreenshot;
            SuspendLayout(); FormBorderStyle = FormBorderStyle.None; AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96,96); BackColor = Theme.Card; ForeColor = Theme.Text;
            ClientSize = new Size(528, 72); StartPosition = FormStartPosition.Manual; TopMost = true; ShowInTaskbar = false;
            Rectangle work = Screen.FromRectangle(area).WorkingArea;
            Location = new Point(Math.Max(work.Left, work.Left + (work.Width - Width) / 2), work.Bottom - Height - 16);
            Rectangle above = new Rectangle(area.Left, area.Top - Height - 12, Width, Height);
            Rectangle below = new Rectangle(area.Left, area.Bottom + 12, Width, Height);
            if (work.Contains(above)) Location = above.Location; else if (work.Contains(below)) Location = below.Location;
            time = Theme.Label("准备录制", 13, Theme.Accent); time.SetBounds(16, 7, 170, 31); time.Font = Theme.Font(13, FontStyle.Bold); Controls.Add(time);
            detail = Theme.Label("Ctrl + Shift + F8 停止", 8, Theme.Muted); detail.SetBounds(16, 39, 205, 24); Controls.Add(detail);
            stop = new SoftButton("■  停止") { Primary = true }; stop.SetBounds(232, 16, 96, 40); stop.Click += delegate { if (StopRequested != null) StopRequested(this, EventArgs.Empty); }; Controls.Add(stop);
            var cancel = new SoftButton("取消"); cancel.SetBounds(338, 16, 76, 40); cancel.Click += delegate { if (CancelRequested != null) CancelRequested(this, EventArgs.Empty); }; Controls.Add(cancel);
            var hide = new SoftButton("隐藏") { Enabled = canHide }; hide.SetBounds(424, 16, 88, 40); hide.Click += delegate { Hide(); }; Controls.Add(hide);
            if(!canHide) detail.Text="快捷键被占用，请点击停止";
            if(screenshot){time.Text="准备截图";detail.Text="截图倒计时 · 可点击取消";stop.Enabled=false;}
            MouseDown += Drag; time.MouseDown += Drag; detail.MouseDown += Drag;
            Shown += delegate { Place(area); bool excluded = Native.Exclude(this); if (!excluded && canHide) detail.Text = "可隐藏控制条，用快捷键停止"; };
            Paint += delegate(object s, PaintEventArgs e) { using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); };
            ResumeLayout(false); PerformAutoScale(); PerformLayout();
        }
        private void Place(Rectangle area) {
            Rectangle work=Screen.FromRectangle(area).WorkingArea;Location=new Point(work.Left+(work.Width-Width)/2,work.Bottom-Height-16);
            Rectangle above=new Rectangle(Math.Max(work.Left,Math.Min(work.Right-Width,area.Left)),area.Top-Height-12,Width,Height);
            Rectangle below=new Rectangle(above.X,area.Bottom+12,Width,Height);
            if(work.Contains(above))Location=above.Location;else if(work.Contains(below))Location=below.Location;
        }
        private void Drag(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } }
        internal void Countdown(int seconds) { time.Text = seconds + (screenshot?" 秒后截图":" 秒后开始"); stop.Enabled = false; }
        internal void Recording(double elapsed) { stop.Enabled = true; time.Text = "●  " + TimeSpan.FromSeconds(Math.Max(0, elapsed)).ToString(@"mm\:ss") + " / 01:00"; }
        internal void Finishing() { time.Text = "正在结束录制…"; stop.Enabled = false; }
    }
}

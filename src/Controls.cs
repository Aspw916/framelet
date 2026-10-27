using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Framelet {
    internal enum LineIcon { None, Photo, Video, Area, Monitor, Window, Refresh, Folder, Copy, Fit, Play, Pause, History, Save, Stop, Hide }
    internal static class Theme {
        internal static readonly Color Background=Color.FromArgb(243,246,245),Card=Color.White,Field=Color.FromArgb(240,244,243),Canvas=Color.FromArgb(234,240,238);
        internal static readonly Color Text=Color.FromArgb(29,44,40),Muted=Color.FromArgb(110,128,121),Accent=Color.FromArgb(22,131,105),AccentSoft=Color.FromArgb(226,241,234),Border=Color.FromArgb(218,227,222),Red=Color.FromArgb(181,71,67);
        internal static Font Font(float size,FontStyle style){return new Font("Microsoft YaHei UI",size,style,GraphicsUnit.Point);}
        internal static GraphicsPath Round(RectangleF r,float radius){
            radius=Math.Min(radius,Math.Min(r.Width,r.Height)/2);float d=Math.Max(1,radius*2);var p=new GraphicsPath();
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        internal static Label Label(string text,float size,Color color){return new Label{Text=text,ForeColor=color,Font=Font(size,FontStyle.Regular),AutoSize=false,AutoEllipsis=true,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft};}
        internal static void Style(ComboBox box){
            box.DropDownStyle=ComboBoxStyle.DropDownList;box.FlatStyle=FlatStyle.Flat;box.BackColor=Field;box.ForeColor=Text;box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=27;box.Font=Font(9,FontStyle.Regular);
            box.DrawItem+=delegate(object s,DrawItemEventArgs e){
                using(var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?AccentSoft:Field))e.Graphics.FillRectangle(b,e.Bounds);
                if(e.Index>=0)TextRenderer.DrawText(e.Graphics,box.GetItemText(box.Items[e.Index]),box.Font,new Rectangle(e.Bounds.X+7,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
            };
        }
        internal static void Icon(Graphics g,LineIcon icon,RectangleF bounds,Color color){
            if(icon==LineIcon.None)return;var saved=g.Save();g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24,bounds.Height/24);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var p=new Pen(color,1.7f)){p.StartCap=p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
                if(icon==LineIcon.Photo){g.DrawRectangle(p,3,4,18,16);g.DrawEllipse(p,15,7,2,2);g.DrawLines(p,new Point[]{new Point(4,17),new Point(10,11),new Point(14,15),new Point(17,12),new Point(20,16)});}
                else if(icon==LineIcon.Video){g.DrawRectangle(p,3,5,13,14);g.DrawPolygon(p,new Point[]{new Point(16,9),new Point(22,6),new Point(22,18),new Point(16,15)});}
                else if(icon==LineIcon.Monitor){g.DrawRectangle(p,3,4,18,13);g.DrawLine(p,12,17,12,21);g.DrawLine(p,8,21,16,21);}
                else if(icon==LineIcon.Window){g.DrawRectangle(p,3,4,18,16);g.DrawLine(p,3,9,21,9);g.DrawLine(p,6,6.5f,7,6.5f);}
                else if(icon==LineIcon.Area||icon==LineIcon.Fit){g.DrawLines(p,new Point[]{new Point(3,9),new Point(3,3),new Point(9,3)});g.DrawLines(p,new Point[]{new Point(15,3),new Point(21,3),new Point(21,9)});g.DrawLines(p,new Point[]{new Point(21,15),new Point(21,21),new Point(15,21)});g.DrawLines(p,new Point[]{new Point(9,21),new Point(3,21),new Point(3,15)});}
                else if(icon==LineIcon.Folder){g.DrawPolygon(p,new Point[]{new Point(3,7),new Point(9,7),new Point(11,10),new Point(21,10),new Point(19,20),new Point(3,20)});g.DrawLines(p,new Point[]{new Point(3,12),new Point(3,4),new Point(9,4),new Point(12,7),new Point(20,7)});}
                else if(icon==LineIcon.Copy){g.DrawRectangle(p,8,8,12,13);g.DrawLines(p,new Point[]{new Point(5,16),new Point(3,16),new Point(3,3),new Point(15,3),new Point(15,5)});}
                else if(icon==LineIcon.Refresh||icon==LineIcon.History){g.DrawArc(p,4,4,16,16,205,300);g.DrawLines(p,new Point[]{new Point(3,3),new Point(3,9),new Point(9,9)});if(icon==LineIcon.History){g.DrawLine(p,12,8,12,12);g.DrawLine(p,12,12,16,14);}}
                else if(icon==LineIcon.Play){g.DrawPolygon(p,new Point[]{new Point(8,4),new Point(20,12),new Point(8,20)});}
                else if(icon==LineIcon.Pause){g.DrawLine(p,8,5,8,19);g.DrawLine(p,16,5,16,19);}
                else if(icon==LineIcon.Save){g.DrawPolygon(p,new Point[]{new Point(4,3),new Point(17,3),new Point(21,7),new Point(21,21),new Point(3,21),new Point(3,3)});g.DrawRectangle(p,7,3,9,6);g.DrawRectangle(p,7,14,10,7);}
                else if(icon==LineIcon.Stop){g.DrawRectangle(p,5,5,14,14);}
                else if(icon==LineIcon.Hide){g.DrawLine(p,4,12,20,12);}
            }g.Restore(saved);
        }
    }
    internal sealed class SoftButton:Control {
        internal bool Primary,Danger,Selected,Ghost;
        internal LineIcon IconKind;
        private bool hover,pressed;
        internal SoftButton(string text){
            Text=text;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.PushButton;Font=Theme.Font(9,FontStyle.Bold);ForeColor=Theme.Text;BackColor=Theme.Card;Height=40;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            MouseEnter+=delegate{hover=true;Invalidate();};MouseLeave+=delegate{hover=false;pressed=false;Invalidate();};
            MouseDown+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}};MouseUp+=delegate{pressed=false;Invalidate();};
        }
        protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
        protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
        internal void PerformClick(){if(Enabled)OnClick(EventArgs.Empty);}
        protected override AccessibleObject CreateAccessibilityInstance(){return new ButtonAccess(this);}
        private sealed class ButtonAccess:ControlAccessibleObject {
            private readonly SoftButton owner;
            internal ButtonAccess(SoftButton button):base(button){owner=button;}
            public override string DefaultAction{get{return "按下";}}
            public override void DoDefaultAction(){if(owner.InvokeRequired)owner.BeginInvoke((Action)delegate{owner.PerformClick();});else owner.PerformClick();}
        }
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(Enabled&&(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space)){OnClick(EventArgs.Empty);e.Handled=true;}}
        protected override void OnPaint(PaintEventArgs e){
            float s=DeviceDpi/96f;var g=e.Graphics;g.Clear(Parent==null?Theme.Card:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            Color bg=Primary?Theme.Accent:Danger?Color.FromArgb(251,237,235):Selected?Theme.AccentSoft:Ghost?BackColor:Theme.Field;
            if(hover&&Enabled)bg=Primary?ControlPaint.Dark(bg,.07f):Selected?Color.FromArgb(213,234,223):Theme.Border;
            if(!Enabled)bg=Ghost?BackColor:Theme.Field;
            using(var path=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),8*s))using(var b=new SolidBrush(bg)){g.FillPath(b,path);if(Selected)using(var p=new Pen(Theme.Accent,1*s))g.DrawPath(p,path);}
            Color fg=!Enabled?Color.FromArgb(158,172,165):Primary?Color.White:Danger?Theme.Red:Selected?Theme.Accent:Theme.Text;
            float offset=pressed&&Enabled?s:0;
            if(IconKind==LineIcon.None)TextRenderer.DrawText(g,Text,Font,new Rectangle(0,(int)offset,Width,Height),fg,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
            else{
                int icon=(int)(18*s),gap=String.IsNullOrEmpty(Text)?0:(int)(7*s);int textWidth=String.IsNullOrEmpty(Text)?0:Math.Min(TextRenderer.MeasureText(Text,Font).Width,Math.Max(0,Width-icon-gap-(int)(14*s)));
                int start=(Width-icon-gap-textWidth)/2;Theme.Icon(g,IconKind,new RectangleF(start,(Height-icon)/2+offset,icon,icon),fg);
                if(textWidth>0)TextRenderer.DrawText(g,Text,Font,new Rectangle(start+icon+gap,(int)offset,textWidth,Height),fg,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
            }
            if(Focused&&ShowFocusCues){var r=ClientRectangle;r.Inflate(-(int)(4*s),-(int)(4*s));ControlPaint.DrawFocusRectangle(g,r,Theme.Accent,bg);}
        }
    }
    internal sealed class ToggleSwitch:CheckBox {
        internal ToggleSwitch(){AutoSize=false;Height=34;BackColor=Theme.Card;ForeColor=Theme.Text;Cursor=Cursors.Hand;Font=Theme.Font(9,FontStyle.Regular);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
        protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            float s=DeviceDpi/96f;var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            int w=(int)(32*s),h=(int)(19*s),x=Width-w-1,y=(Height-h)/2;
            using(var path=Theme.Round(new RectangleF(x,y,w,h),h/2f))using(var b=new SolidBrush(Enabled&&Checked?Theme.Accent:Theme.Border))g.FillPath(b,path);
            int circle=h-(int)(5*s),left=Checked?x+w-circle-(int)(2.5*s):x+(int)(2.5*s);
            using(var b=new SolidBrush(Color.White))g.FillEllipse(b,left,y+(int)(2.5*s),circle,circle);
            TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,x-(int)(12*s),Height),Enabled?Theme.Text:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(0,2,Width-1,Height-4),Theme.Accent,BackColor);
        }
    }
    internal sealed class DarkComboBox:ComboBox {
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ResizeItems();}
        protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(IsHandleCreated)ResizeItems();}
        protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);ResizeItems();}
        private void ResizeItems(){ItemHeight=Math.Max(Font.Height+8,(int)(25*DeviceDpi/96f));}
    }
    internal sealed class Card:Panel {
        internal Card(){BackColor=Theme.Card;Padding=new Padding(18);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){
            e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var p=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),12*DeviceDpi/96f))using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),12*DeviceDpi/96f))using(var pen=new Pen(Theme.Border))e.Graphics.DrawPath(pen,p);}
    }
    internal sealed class RangeSlider:Control {
        internal double Duration=1,Start,End=1;
        internal event EventHandler RangeChanged,EditCompleted;
        private int active;
        internal RangeSlider(){Height=44;TabStop=true;Cursor=Cursors.Hand;AccessibleName="裁剪范围，方向键调整，空格切换首尾";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        private float S{get{return DeviceDpi/96f;}}
        private float X(double seconds){return 12*S+(float)(seconds/Math.Max(.01,Duration)*Math.Max(1,Width-24*S));}
        private double Seconds(int x){return Math.Max(0,Math.Min(Duration,(x-12.0*S)/Math.Max(1,Width-24*S)*Duration));}
        internal void SetRange(double duration,double start,double end){Duration=duration;Start=start;End=end;Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=S;
            using(var p=Theme.Round(new RectangleF(12*s,9*s,Math.Max(1,Width-24*s),16*s),4*s))using(var b=new SolidBrush(Theme.Field))g.FillPath(b,p);
            using(var b=new SolidBrush(Theme.AccentSoft))g.FillRectangle(b,X(Start),9*s,Math.Max(1,X(End)-X(Start)),16*s);
            using(var p=new Pen(Theme.Border))for(int i=0;i<=32;i++){float x=12*s+(Width-24*s)*i/32f;g.DrawLine(p,x,13*s,x,21*s);}
            foreach(double n in new double[]{Start,End}){using(var p=Theme.Round(new RectangleF(X(n)-4*s,5*s,8*s,24*s),3*s))using(var b=new SolidBrush(Enabled?Theme.Accent:Theme.Muted))g.FillPath(b,p);using(var p=new Pen(Color.White,s))g.DrawLine(p,X(n),13*s,X(n),21*s);}
            using(var f=Theme.Font(8,FontStyle.Regular)){TextRenderer.DrawText(g,"0s",f,new Point((int)(8*s),(int)(29*s)),Theme.Muted);string total=Duration.ToString("0.00")+"s";Size size=TextRenderer.MeasureText(total,f);TextRenderer.DrawText(g,total,f,new Point(Width-size.Width-(int)(8*s),(int)(29*s)),Theme.Muted);}
            if(Focused)using(var p=new Pen(Theme.Accent)){p.DashStyle=DashStyle.Dot;g.DrawRectangle(p,X(active==2?End:Start)-7*s,2*s,14*s,30*s);}
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!Enabled)return;Focus();active=Math.Abs(e.X-X(Start))<=Math.Abs(e.X-X(End))?1:2;Capture=true;Change(e.X);}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&active>0)Change(e.X);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Capture=false;if(EditCompleted!=null)EditCompleted(this,EventArgs.Empty);}
        private void Change(int x){double v=Seconds(x),gap=Math.Min(.1,Duration);if(active==1)Start=Math.Min(End-gap,v);else End=Math.Max(Start+gap,v);Invalidate();if(RangeChanged!=null)RangeChanged(this,EventArgs.Empty);}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){active=active==2?1:2;Invalidate();e.Handled=true;}if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){if(active==0)active=1;Change((int)X((active==1?Start:End)+(e.KeyCode==Keys.Left?-.1:.1)));if(EditCompleted!=null)EditCompleted(this,EventArgs.Empty);e.Handled=true;}}
    }
    internal sealed class PreviewView:Control {
        private Image image;
        private Stream stream;
        private bool playing=true,processing,actual;
        private string processingText="";
        private int percent;
        private Point dragOrigin;
        private PointF pan;
        internal string EmptyTitle="还没有截图",EmptyDescription="在左侧选择范围，然后点击「开始截图」。";
        internal LineIcon EmptyIcon=LineIcon.Photo;
        internal event EventHandler ViewChanged;
        internal Rectangle ImageBounds{get;private set;}
        internal bool ActualSize{get{return actual;}}
        internal PreviewView(){BackColor=Theme.Canvas;Dock=DockStyle.Fill;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AccessibleName="捕获预览";}
        internal bool HasImage{get{return image!=null;}}
        internal bool Playing{get{return playing;}}
        internal void LoadGif(string path){Clear();stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);try{image=Image.FromStream(stream);playing=true;ImageAnimator.Animate(image,FrameChanged);Invalidate();}catch{Clear();throw;}}
        internal void LoadStill(Bitmap bitmap){Clear();image=new Bitmap(bitmap);playing=false;Invalidate();}
        internal void Toggle(){playing=!playing;if(image!=null){if(playing)ImageAnimator.Animate(image,FrameChanged);else ImageAnimator.StopAnimate(image,FrameChanged);}Invalidate();}
        internal void ToggleSize(){actual=!actual;pan=PointF.Empty;Invalidate();if(ViewChanged!=null)ViewChanged(this,EventArgs.Empty);}
        internal void SetProcessing(bool value,string text,int progress){processing=value;processingText=text;percent=progress;Invalidate();}
        private void FrameChanged(object s,EventArgs e){try{if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)delegate{if(!IsDisposed)Invalidate();});}catch{}}
        internal void Clear(){if(image!=null){ImageAnimator.StopAnimate(image,FrameChanged);image.Dispose();image=null;}if(stream!=null){stream.Dispose();stream=null;}pan=PointF.Empty;ImageBounds=Rectangle.Empty;Invalidate();}
        protected override void Dispose(bool disposing){if(disposing)Clear();base.Dispose(disposing);}
        protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(image!=null)ToggleSize();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(actual&&image!=null&&e.Button==MouseButtons.Left){dragOrigin=e.Location;Capture=true;Cursor=Cursors.SizeAll;}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&actual&&image!=null){pan.X+=e.X-dragOrigin.X;pan.Y+=e.Y-dragOrigin.Y;dragOrigin=e.Location;ClampPan();Invalidate();}}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Capture=false;Cursor=Cursors.Default;}
        private void ClampPan(){if(image==null)return;float x=Math.Max(0,(image.Width-Width+24)/2f),y=Math.Max(0,(image.Height-Height+24)/2f);pan.X=Math.Max(-x,Math.Min(x,pan.X));pan.Y=Math.Max(-y,Math.Min(y,pan.Y));}
        protected override void OnPaint(PaintEventArgs e){
            var g=e.Graphics;g.Clear(BackColor);float s=g.DpiX/96;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var dot=new SolidBrush(Color.FromArgb(211,222,216)))for(float x=18*s;x<Width;x+=24*s)for(float y=18*s;y<Height;y+=24*s)g.FillEllipse(dot,x,y,1.1f*s,1.1f*s);
            if(image!=null){
                if(playing&&!processing)ImageAnimator.UpdateFrames(image);
                double scale=actual?1:Math.Min(Math.Max(1,s),Math.Min((Width-40*s)/image.Width,(Height-40*s)/image.Height));scale=Math.Max(.01,scale);
                int w=Math.Max(1,(int)(image.Width*scale)),h=Math.Max(1,(int)(image.Height*scale));ClampPan();
                ImageBounds=new Rectangle((Width-w)/2+(int)(actual?pan.X:0),(Height-h)/2+(int)(actual?pan.Y:0),w,h);
                using(var b=new SolidBrush(Color.FromArgb(24,70,85,75))){Rectangle shadow=ImageBounds;shadow.Offset(0,(int)(4*s));shadow.Inflate((int)(3*s),(int)(3*s));g.FillRectangle(b,shadow);}
                g.InterpolationMode=actual?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;g.DrawImage(image,ImageBounds);
                using(var p=new Pen(Color.White,1*s))g.DrawRectangle(p,ImageBounds);
            }else{
                float y=Height/2-64*s;using(var p=Theme.Round(new RectangleF(Width/2-34*s,y-12*s,68*s,68*s),18*s))using(var b=new SolidBrush(Theme.Card))g.FillPath(b,p);
                Theme.Icon(g,EmptyIcon,new RectangleF(Width/2-18*s,y+4*s,36*s,36*s),Theme.Accent);
                using(var f=Theme.Font(14,FontStyle.Bold))TextRenderer.DrawText(g,EmptyTitle,f,new Rectangle(0,(int)(y+78*s),Width,(int)(30*s)),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
                using(var f=Theme.Font(9,FontStyle.Regular))TextRenderer.DrawText(g,EmptyDescription,f,new Rectangle((int)(20*s),(int)(y+115*s),Width-(int)(40*s),(int)(30*s)),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            }
            if(processing){
                using(var b=new SolidBrush(Color.FromArgb(220,Theme.Card)))g.FillRectangle(b,ClientRectangle);
                using(var f=Theme.Font(11,FontStyle.Bold))TextRenderer.DrawText(g,processingText,f,new Rectangle(0,Height/2-(int)(24*s),Width,(int)(30*s)),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine);
                int width=(int)(180*s),left=(Width-width)/2,top=Height/2+(int)(22*s);
                using(var b=new SolidBrush(Theme.Border))g.FillRectangle(b,left,top,width,(int)(3*s));using(var b=new SolidBrush(Theme.Accent))g.FillRectangle(b,left,top,Math.Max((int)(5*s),width*Math.Max(0,percent)/100),(int)(3*s));
            }
        }
    }
}

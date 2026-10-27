using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Framelet {
    internal static class SelfTest {
        private static readonly List<string> checks=new List<string>();
        private static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);checks.Add("PASS: "+name);}
        internal static async Task Run(string root) {
            if(File.Exists(Path.Combine(root,"failure.txt")))File.Delete(Path.Combine(root,"failure.txt"));
            var sw=Stopwatch.StartNew();
            Check(Native.Normalize(new Point(-300,200),new Point(80,-50))==new Rectangle(-300,-50,380,250),"跨原点、反方向框选坐标");
            Check(Native.MoveWithin(new Rectangle(-200,-100,80,60),-999,999,new Rectangle(-500,-300,1000,600))==new Rectangle(-500,240,80,60),"移动选区边界约束（含负坐标）");
            Check(Paths.Quote("C:\\目录 with spaces\\") == "\"C:\\目录 with spaces\\\\\"","命令行路径含空格、中文和末尾斜杠");
            Check(Paths.Quote("a\"b") == "\"a\\\"b\"","命令行引号转义");
            string settings=Path.Combine(root,"prefs.json");var pref=new Preferences{Mode=2,Cursor=false,Countdown=true,Width=960,Fps=15,SaveDirectory="C:\\中文 & folder"};pref.Region=new Rectangle(-320,10,320,241);Paths.Save(settings,pref);var loaded=Paths.Load<Preferences>(settings);
            Check(loaded!=null&&loaded.Region==pref.Region&&!loaded.Cursor&&loaded.SaveDirectory==pref.SaveDirectory,"设置 JSON 往返保存");
            Check(File.Exists(Paths.Ffmpeg),"便携目录自带 FFmpeg");
            string dir=Paths.NewSession(),source=Path.Combine(dir,"recording.mkv");
            await Media.Run("-y -loglevel error -f lavfi -i testsrc2=size=640x360:rate=20 -t 3.2 -an -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -threads 2 "+Paths.Quote(source),CancellationToken.None,null,0);
            MediaInfo info=await Media.Probe(source);Check(info.Width==640&&info.Height==360&&Math.Abs(info.Duration-3.2)<.1,"读取实际媒体尺寸与时长");
            var clip=new Clip{DirectoryPath=dir,Width=info.Width,Height=info.Height,Duration=info.Duration,Start=.6,End=2.2,CapturedAt=DateTime.Now.ToString("s")};clip.Write();var recovered=Clip.Open(dir);
            Check(recovered!=null&&recovered.Start==.6&&recovered.End==2.2,"恢复原片和裁剪范围");
            var values=new List<int>();string gif=Path.Combine(root,"测试 & trimmed output.gif");
            await Media.Gif(clip,.6,2.2,320,15,gif,CancellationToken.None,delegate(int p){lock(values)values.Add(p);});
            MediaInfo gi=await Media.Probe(gif);Check(gi.Width==320&&gi.Height==180&&Math.Abs(gi.Duration-1.6)<.16,"真实裁剪导出：尺寸 320×180、时长 1.6 秒");
            int frames;using(var image=Image.FromFile(gif)){frames=image.GetFrameCount(FrameDimension.Time);Check(frames>=22&&frames<=26,"GIF 动画帧数与 15fps 一致");var prop=image.GetPropertyItem(0x5101);Check(prop!=null&&prop.Value.Length>=2&&prop.Value[0]==0&&prop.Value[1]==0,"GIF 无限循环标记");}
            Check(values.Count>2&&values[values.Count-1]==100,"两遍导出进度完成");
            string raw=Path.Combine(root,"original-width.gif");await Media.Gif(clip,0,.3,960,10,raw,CancellationToken.None,null);var original=await Media.Probe(raw);Check(original.Width==640&&original.Height==360,"导出宽度不放大原图");
            string target=Path.Combine(root,"existing.gif");File.WriteAllText(target,"previous file");string originalText=File.ReadAllText(target);
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool cancelled=false;try{await Media.Gif(clip,0,3,640,20,Path.Combine(root,"cancelled.gif"),cancel.Token,null);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&File.ReadAllText(target)==originalText&&File.Exists(source),"取消导出保留源文件和原目标");}
            using(var cancel=new CancellationTokenSource()){cancel.CancelAfter(400);bool cancelled=false;try{await Media.Run("-loglevel error -re -f lavfi -i testsrc2=size=640x360:rate=20 -t 30 -f null -",cancel.Token,null,30);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"运行中的编码进程可取消并退出");}
            Media.Commit(gif,target);Check(new FileInfo(target).Length==new FileInfo(gif).Length,"成功后原子替换已存在目标");
            bool failed=false;try{Media.Commit(gif,Path.Combine(root,"missing-folder","file.gif"));}catch(IOException){failed=true;}Check(failed&&File.Exists(source),"保存路径失败后源文件仍在，可重试");
            string retry=Path.Combine(root,"retry.gif");Media.Commit(gif,retry);Check(File.Exists(retry),"保存失败后可换路径重试");
            // Exercise capture device, wall-clock timing, stop signal, and odd input dimensions.
            Rectangle screen=Screen.PrimaryScreen.Bounds;string captureDir=Paths.NewSession();string cap=Path.Combine(captureDir,"recording.mkv");
            var area=new Rectangle(screen.Left+24,screen.Top+24,321,241);
            using(var recorder=new Recorder()){recorder.Start(area,false,20,cap,3);await Task.Delay(1700);await recorder.Stop();}
            var ci=await Media.Probe(cap);Check(ci.Width==322&&ci.Height==242&&ci.Duration>.3,"真实屏幕局部录制与奇数尺寸补齐");
            string full=Path.Combine(root,"fullscreen.mkv");using(var recorder=new Recorder()){recorder.Start(screen,true,10,full,1);await Task.Delay(2400);await recorder.Stop();}
            var fi=await Media.Probe(full);Check(fi.Width==(screen.Width+screen.Width%2)&&fi.Height==(screen.Height+screen.Height%2)&&fi.Duration<=1.2,"完整显示器捕获、鼠标选项、自动时长上限");
            Check(Directory.GetFiles(dir,"palette-*.png").Length==0,"调色板临时文件已清理");
            File.WriteAllLines(Path.Combine(root,"results.txt"),checks,Encoding.UTF8);File.AppendAllText(Path.Combine(root,"results.txt"),"Elapsed: "+sw.Elapsed.TotalSeconds.ToString("0.00")+" seconds\n");
        }
        internal static int RunUi(string root) {
            if(File.Exists(Path.Combine(root,"ui-failure.txt")))File.Delete(Path.Combine(root,"ui-failure.txt"));
            if(File.Exists(Path.Combine(root,"ui-results.txt")))File.Delete(Path.Combine(root,"ui-results.txt"));
            int code=0;using(var form=new MainForm()) {
                form.Icon=Program.AppIcon();form.Shown+=async delegate {
                    try{Native.ShowWindow(form.Handle,5);Native.SetForegroundWindow(form.Handle);await Task.Delay(600);await VerifyUi(form,root);File.WriteAllLines(Path.Combine(root,"ui-results.txt"),checks,Encoding.UTF8);}
                    catch(Exception ex){code=1;File.WriteAllText(Path.Combine(root,"ui-failure.txt"),ex.ToString());}
                    try{if(Field<bool>(form,"recording"))await (Task)Invoke(form,"FinishRecording",true);var pending=Field<CancellationTokenSource>(form,"job");if(pending!=null)pending.Cancel();for(int wait=0;wait<100&&Field<bool>(form,"busy");wait++)await Task.Delay(50);}catch{}
                    form.Close();
                };Application.Run(form);
            }return code;
        }
        private static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
        private static object Invoke(MainForm form,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,args);}
        private static void Shot(Form form,string path){using(var bitmap=new Bitmap(form.Width,form.Height)){using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{if(!Native.PrintWindow(form.Handle,dc,2))throw new Exception("窗口快照失败");}finally{g.ReleaseHdc(dc);}}bitmap.Save(path,ImageFormat.Png);}}
        private static async Task VerifyUi(MainForm form,string root) {
            Shot(form,Path.Combine(root,"01-ready.png"));Check(form.Visible&&Field<SoftButton>(form,"record").Enabled,"主界面可用、开始按钮可操作");
            using(var g=form.CreateGraphics()){var dump=new StringBuilder("DPI: "+g.DpiX+"\nForm: "+form.Bounds+"\n");Dump(form,dump,0);File.WriteAllText(Path.Combine(root,"dpi.txt"),dump.ToString());Check(Field<SoftButton>(form,"startCapture").Height>=40*g.DpiY/96,"高 DPI 主操作按钮尺寸正确缩放");}
            Check(Field<PreviewView>(form,"preview").Height>form.ClientSize.Height*.6,"空白状态以大预览为主");
            Check(!Field<Panel>(form,"exportArea").Visible,"未捕获前不显示无效导出设置");
            using(var g=form.CreateGraphics())Check(Field<SoftButton>(form,"record").Width>=90*g.DpiX/96&&Field<SoftButton>(form,"screenshotButton").Width>=90*g.DpiX/96,"顶部任务切换按钮完整可见");
            Field<SoftButton>(form,"record").PerformClick();Check(Field<int>(form,"captureTask")==1&&Field<SoftButton>(form,"startCapture").Text=="开始录制","顶部录制入口切换主操作");
            Field<SoftButton>(form,"screenshotButton").PerformClick();Check(Field<int>(form,"captureTask")==0&&Field<SoftButton>(form,"startCapture").Text=="开始截图","顶部截图入口切换主操作");
            Check(Field<Panel>(form,"trimArea").Enabled==false,"录制前禁用裁剪");
            var targets=Native.Windows();Check(targets.Count>0,"可见窗口列表枚举");
            using(var animation=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--fixture"){UseShellExecute=false,CreateNoWindow=true})) {
                try {
                await Task.Delay(850);animation.Refresh();IntPtr handle=animation.MainWindowHandle;Rectangle targetRect=Native.WindowBounds(handle);
                Check(targetRect.Width>400&&targetRect.Height>200,"读取测试窗口物理坐标");
                Invoke(form,"SetMode",1);var combo=Field<ComboBox>(form,"target");int selected=-1;for(int i=0;i<combo.Items.Count;i++){var w=combo.Items[i] as WindowTarget;if(w!=null&&w.Handle==handle)selected=i;}
                Check(selected>=0,"窗口模式可选择动态测试窗口");combo.SelectedIndex=selected;Field<CheckBox>(form,"countdown").Checked=false;
                await (Task)Invoke(form,"BeginRecording");await Task.Delay(2100);Check(Field<bool>(form,"recording"),"主界面启动实际窗口录制");
                Native.SendMessage(form.Handle,0x0312,new IntPtr(101),IntPtr.Zero);
                for(int wait=0;wait<200&&(Field<bool>(form,"recording")||Field<bool>(form,"busy")||Field<bool>(form,"stopping"));wait++)await Task.Delay(50);
                var c=Field<Clip>(form,"clip");Check(c!=null&&c.Duration>.5&&c.Width>=400,"窗口录制、停止快捷键处理和元数据");Check(Field<PreviewView>(form,"preview").HasImage,"录制后自动生成循环预览");Native.ShowWindow(handle,0);
                Shot(form,Path.Combine(root,"02-recorded.png"));
                Field<NumericUpDown>(form,"trimStart").Value=.2M;Field<NumericUpDown>(form,"trimEnd").Value=(decimal)Math.Min(c.Duration,1.2);
                await (Task)Invoke(form,"MakePreview");Check(c.Start==.2&&c.End<=1.21,"界面首尾裁剪写入实际会话");Shot(form,Path.Combine(root,"03-trimmed.png"));
                string previousPreview=Field<string>(form,"currentPreview");Field<NumericUpDown>(form,"trimStart").Value=.25M;
                for(int wait=0;wait<240&&(Field<string>(form,"currentPreview")==previousPreview||Field<bool>(form,"busy"));wait++)await Task.Delay(50);
                Check(Field<string>(form,"currentPreview")!=previousPreview&&!Field<bool>(form,"busy")&&c.Start==.25,"裁剪停止调整后自动更新预览");Shot(form,Path.Combine(root,"03-trimmed.png"));
                string output=Path.Combine(root,"window-demo.gif");await Media.Gif(c,c.Start,c.End,640,15,output,CancellationToken.None,null);var mi=await Media.Probe(output);Check(Math.Abs(mi.Duration-(c.End-c.Start))<.2,"窗口动态内容导出成功");
                using(var img=Image.FromFile(output)){img.SelectActiveFrame(FrameDimension.Time,0);using(var first=new Bitmap(img)){img.SelectActiveFrame(FrameDimension.Time,Math.Max(1,img.GetFrameCount(FrameDimension.Time)-1));using(var last=new Bitmap(img)){long difference=0;for(int y=30;y<Math.Min(first.Height,last.Height);y+=13)for(int x=20;x<Math.Min(first.Width,last.Width);x+=13){Color a=first.GetPixel(x,y),b=last.GetPixel(x,y);difference+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);}Check(difference>2000,"导出确实包含变化的动画帧");}}}
                Field<PreviewView>(form,"preview").Toggle();Check(!Field<PreviewView>(form,"preview").Playing,"预览暂停");Field<PreviewView>(form,"preview").Toggle();
                await (Task)Invoke(form,"Restore");Check(Field<Clip>(form,"clip")!=null&&Field<PreviewView>(form,"preview").HasImage,"界面恢复上次录制和预览");
                Native.ShowWindow(handle,5);await Task.Delay(100);await VerifyScreenshots(form,root,handle);
                animation.CloseMainWindow();
                } finally { try { if(!animation.HasExited)animation.Kill(); } catch { } }
            }
            form.Hide();await Task.Delay(180);
            try { using(var overlay=new SelectionOverlay(Rectangle.Empty)) {
                overlay.Show();await Task.Delay(100);
                var down=typeof(SelectionOverlay).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic);var move=typeof(SelectionOverlay).GetMethod("OnMouseMove",BindingFlags.Instance|BindingFlags.NonPublic);var up=typeof(SelectionOverlay).GetMethod("OnMouseUp",BindingFlags.Instance|BindingFlags.NonPublic);
                down.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,100,100,0)});move.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,0,500,340,0)});up.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,500,340,0)});
                Rectangle initial=overlay.SelectedRegion;Check(initial.Width==400&&initial.Height==240,"真实选区覆盖层拖动框选");
                down.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,200,200,0)});move.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,0,260,230,0)});up.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,260,230,0)});
                Rectangle moved=overlay.SelectedRegion;Check(moved.X==initial.X+60&&moved.Y==initial.Y+30,"覆盖层移动选区");
                down.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,560,370,0)});move.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,0,640,410,0)});up.Invoke(overlay,new object[]{new MouseEventArgs(MouseButtons.Left,1,640,410,0)});
                Check(overlay.SelectedRegion.Width==480&&overlay.SelectedRegion.Height==280,"覆盖层拖角缩放选区");
                typeof(SelectionOverlay).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,new object[]{new KeyEventArgs(Keys.Enter)});Check(overlay.DialogResult==DialogResult.OK,"覆盖层 Enter 确认");
            } } finally { form.Show(); }
            await VerifyLiveOutline(form,root);
            // Cancelling a new recording must preserve the previous clip and discard only the new source.
            Clip previousClip=Field<Clip>(form,"clip");string previousSession=Field<Preferences>(form,"prefs").LastSession;
            typeof(MainForm).GetField("region",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,new Rectangle(Screen.PrimaryScreen.Bounds.Left+30,Screen.PrimaryScreen.Bounds.Top+30,320,180));
            Invoke(form,"SetMode",2);Field<CheckBox>(form,"countdown").Checked=false;
            await (Task)Invoke(form,"BeginRecording");await Task.Delay(800);string cancelledDirectory=Field<string>(form,"pendingDirectory");await (Task)Invoke(form,"FinishRecording",true);
            Check(Object.ReferenceEquals(previousClip,Field<Clip>(form,"clip"))&&previousSession==Field<Preferences>(form,"prefs").LastSession&&!Directory.Exists(cancelledDirectory),"取消录制丢弃本次文件，并保留上一次录制");
            // Cancel from the recording bar while countdown is running.
            Invoke(form,"SetMode",2);Field<CheckBox>(form,"countdown").Checked=true;Task begin=(Task)Invoke(form,"BeginRecording");await Task.Delay(550);var cts=Field<CancellationTokenSource>(form,"countdownJob");Check(cts!=null,"倒计时已开始");Check(Field<CaptureOutline>(form,"outline")!=null&&!Field<CaptureOutline>(form,"outline").IsRecording,"局部倒计时显示选区及准备状态");cts.Cancel();await begin;Check(!Field<bool>(form,"recording")&&form.Visible,"取消倒计时后回到主界面");Check(Field<CaptureOutline>(form,"outline")==null,"取消倒计时清除选区边框");
            // Export cancel leaves all UI controls reusable.
            var previewTask=(Task)Invoke(form,"MakePreview");await Task.Delay(20);var active=Field<CancellationTokenSource>(form,"job");if(active!=null)active.Cancel();await previewTask;Check(!Field<bool>(form,"busy")&&Field<SoftButton>(form,"record").Enabled,"取消媒体处理后可以继续录制");
            using(var g=form.CreateGraphics()){float scale=g.DpiX/96;form.ClientSize=new Size((int)(960*scale),(int)(710*scale));}await Task.Delay(150);Shot(form,Path.Combine(root,"04-small-window.png"));Check(Field<SoftButton>(form,"exportButton").RectangleToScreen(Field<SoftButton>(form,"exportButton").ClientRectangle).Right<=form.Right,"小窗口导出按钮未溢出");
        }
        private static async Task VerifyScreenshots(MainForm form,string root,IntPtr window) {
            Clip originalClip=Field<Clip>(form,"clip");double start=originalClip.Start,end=originalClip.End;
            Field<CheckBox>(form,"countdown").Checked=false;Field<CheckBox>(form,"includeCursor").Checked=false;
            for(int mode=0;mode<3;mode++) {
                Invoke(form,"SetMode",mode);Rectangle expected;
                if(mode==0)expected=Screen.PrimaryScreen.Bounds;
                else if(mode==1){var target=Field<ComboBox>(form,"target");for(int i=0;i<target.Items.Count;i++){var item=target.Items[i] as WindowTarget;if(item!=null&&item.Handle==window)target.SelectedIndex=i;}expected=Native.WindowBounds(window);}
                else {Rectangle w=Native.WindowBounds(window);expected=new Rectangle(w.Left+24,w.Top+80,321,241);typeof(MainForm).GetField("region",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,expected);}
                await (Task)Invoke(form,"TakeScreenshot");
                Bitmap image=Field<Bitmap>(form,"screenshot");
                Check(image!=null&&image.Size==expected.Size&&Field<PreviewView>(form,"preview").HasImage,(mode==0?"全屏":mode==1?"窗口":"局部")+"静态截图在主工作区预览，物理尺寸准确");
                Check(!Field<Panel>(form,"trimArea").Visible&&Field<Panel>(form,"exportArea").Visible,"截图只显示保存与复制，不显示时间轴");
                if(mode==2) {
                    Shot(form,Path.Combine(root,"06-screenshot-preview.png"));
                    Check(!await (Task<bool>)Invoke(form,"SaveScreenshotTo",Path.Combine(root,"missing-output","capture.png")),"截图保存失败后保留预览");
                    string path=Path.Combine(root,"截图 & exact-size.png");
                    Check(await (Task<bool>)Invoke(form,"SaveScreenshotTo",path),"截图保存失败后可换路径重试");
                    using(var saved=Image.FromFile(path)){Check(saved.RawFormat.Guid==ImageFormat.Png.Guid&&saved.Width==321&&saved.Height==241,"PNG 无损保存且奇数尺寸不补边");using(var bitmap=new Bitmap(saved))Check(bitmap.GetPixel(0,0).A==255,"截图保存为不透明 PNG");}
                    Check(await (Task<bool>)Invoke(form,"SaveScreenshotTo",path)&&File.Exists(path),"PNG 可以原子覆盖已有文件");
                    VerifyScreenshotClipboard(form);
                    var view=Field<PreviewView>(form,"preview");view.ToggleSize();form.Refresh();
                    Check(view.ActualSize&&view.ImageBounds.Width==321&&view.ImageBounds.Height==241,"原大预览使用实际像素");view.ToggleSize();
                }
                Check(!Field<bool>(form,"busy")&&Field<SoftButton>(form,"startCapture").Enabled,"截图完成后可继续捕获（模式 "+mode+"）");
            }
            Invoke(form,"SetTask",1);
            Check(Object.ReferenceEquals(originalClip,Field<Clip>(form,"clip"))&&originalClip.Start==start&&originalClip.End==end&&Field<PreviewView>(form,"preview").HasImage,"切回录制后恢复 GIF 及首尾裁剪");
            Check(Field<Panel>(form,"trimArea").Visible&&!Field<SoftButton>(form,"copyScreenshot").Visible,"录制只显示时间轴与 GIF 导出");
            Invoke(form,"SetTask",0);Check(Field<Bitmap>(form,"screenshot")!=null&&Field<PreviewView>(form,"preview").HasImage,"切回截图后保留图片");
            Field<CheckBox>(form,"countdown").Checked=true;Task countdown=(Task)Invoke(form,"TakeScreenshot");await Task.Delay(450);
            var pending=Field<CancellationTokenSource>(form,"countdownJob");Check(pending!=null&&!Field<bool>(form,"recording"),"静态截图支持倒计时");pending.Cancel();await countdown;
            Check(form.Visible&&!Field<bool>(form,"busy")&&Field<RecordingBar>(form,"bar")==null,"取消截图倒计时后清理控制条");Field<CheckBox>(form,"countdown").Checked=false;
        }
        private static void VerifyScreenshotClipboard(MainForm form) {
            var backup=new DataObject();var disposable=new List<IDisposable>();bool populated=false;
            try {
                IDataObject original=Clipboard.GetDataObject();
                if(original!=null)foreach(string format in original.GetFormats(false)) {
                    object value=original.GetData(format,false);
                    if(value==null)throw new InvalidOperationException("无法完整保存剪贴板格式，跳过剪贴板复验。");
                    var image=value as Image;if(image!=null){value=new Bitmap(image);disposable.Add((IDisposable)value);}
                    var stream=value as MemoryStream;if(stream!=null){value=new MemoryStream(stream.ToArray());disposable.Add((IDisposable)value);}
                    backup.SetData(format,false,value);populated=true;
                }
            } catch { foreach(var item in disposable)item.Dispose();return; }
            uint written=0;
            try {
                bool copied=(bool)Invoke(form,"CopyScreenshot");written=Native.GetClipboardSequenceNumber();Check(copied&&Field<bool>(form,"screenshotCopied"),"静态截图可复制到剪贴板");
                using(var image=Clipboard.GetImage())Check(image!=null&&image.Size==Field<Bitmap>(form,"screenshot").Size,"剪贴板图片保留原始尺寸");
            } finally {
                if(written!=0&&Native.GetClipboardSequenceNumber()==written){if(populated)Clipboard.SetDataObject(backup,true,5,100);else Clipboard.Clear();}
                foreach(var item in disposable)item.Dispose();
            }
        }
        private static async Task VerifyLiveOutline(MainForm form,string root) {
            Color background=Color.FromArgb(35,48,68);
            using(var target=new Form{FormBorderStyle=FormBorderStyle.None,AutoScaleMode=AutoScaleMode.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(100,100,1000,600),BackColor=background,TopMost=true,ShowInTaskbar=false}) {
                target.Show();await Task.Delay(120);
                Rectangle selected=new Rectangle(350,260,480,280);
                typeof(MainForm).GetField("region",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,selected);
                Invoke(form,"SetMode",2);Field<CheckBox>(form,"countdown").Checked=false;Field<CheckBox>(form,"includeCursor").Checked=false;
                await (Task)Invoke(form,"BeginRecording");
                CaptureOutline indicator=Field<CaptureOutline>(form,"outline");
                Check(indicator!=null&&Native.IsWindowVisible(indicator.Handle)&&indicator.CaptureArea==selected,"局部录制持续显示准确选区");
                Check(indicator.IsRecording&&indicator.BadgeText.Contains("录制中")&&indicator.BadgeText.Contains("480 × 280"),"选区标记显示录制状态和尺寸");
                Check(!indicator.Region.IsVisible(indicator.PointToClient(new Point(selected.Left+selected.Width/2,selected.Top+selected.Height/2)))&&Native.SendMessage(indicator.Handle,0x0084,IntPtr.Zero,IntPtr.Zero)==new IntPtr(-1),"选区内部镂空，边框鼠标穿透");
                Field<RecordingBar>(form,"bar").Hide();Check(indicator.Visible,"隐藏控制条后选区边框仍可见");
                // Deliberately remove capture exclusion for this normal, external
                // outline. Its pixels must still stay out of the recorded area.
                Native.SetWindowDisplayAffinity(indicator.Handle,0);await Task.Delay(220);
                using(var image=new Bitmap(target.Width,target.Height)) {
                    using(var g=Graphics.FromImage(image)) {
                        IntPtr screen=Native.GetDC(IntPtr.Zero),destination=g.GetHdc();
                        try{if(!Native.BitBlt(destination,0,0,image.Width,image.Height,screen,target.Left,target.Top,0x40CC0020))throw new IOException("录制边框快照失败");}
                        finally{g.ReleaseHdc(destination);Native.ReleaseDC(IntPtr.Zero,screen);}
                    }
                    image.Save(Path.Combine(root,"05-local-range.png"),ImageFormat.Png);
                    Color edge=image.GetPixel(selected.Left-target.Left-1,selected.Top-target.Top+selected.Height/2);
                    Check(Math.Abs(edge.G-Theme.Accent.G)<8&&Math.Abs(edge.R-Theme.Accent.R)<8,"录制边框实际显示在屏幕上");
                }
                await Task.Delay(650);await (Task)Invoke(form,"FinishRecording",false);
                Check(Field<CaptureOutline>(form,"outline")==null&&indicator.IsDisposed,"停止录制后选区边框消失");
                await CheckUniformFrame(Field<Clip>(form,"clip"),background,Path.Combine(root,"outline-frame.png"),"实际录制不含边框及录制标签（无捕获排除依赖）");
                // Screen-edge selections can only draw their inner edge when
                // Windows supports excluding this overlay from capture.
                target.Location=Point.Empty;await Task.Delay(120);selected=new Rectangle(0,0,480,280);
                typeof(MainForm).GetField("region",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,selected);
                await (Task)Invoke(form,"BeginRecording");await Task.Delay(850);await (Task)Invoke(form,"FinishRecording",false);
                await CheckUniformFrame(Field<Clip>(form,"clip"),background,Path.Combine(root,"outline-edge-frame.png"),"贴近屏幕边缘录制时边框不进入画面");
            }
        }
        private static async Task CheckUniformFrame(Clip clip,Color expected,string path,string name) {
            await Media.Run("-y -loglevel error -ss 0.1 -i "+Paths.Quote(clip.Video)+" -frames:v 1 -update 1 -threads 2 "+Paths.Quote(path),CancellationToken.None,null,0);
            int difference=0;
            using(var image=new Bitmap(path))for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++) {
                Color c=image.GetPixel(x,y);difference=Math.Max(difference,Math.Max(Math.Abs(c.R-expected.R),Math.Max(Math.Abs(c.G-expected.G),Math.Abs(c.B-expected.B))));
            }
            Check(difference<=10,name);
        }
        private static void Dump(Control control,StringBuilder dump,int depth){Native.RECT r;Native.GetWindowRect(control.Handle,out r);dump.AppendLine(new String(' ',depth)+control.GetType().Name+" "+control.Text+" b="+control.Bounds+" c="+control.ClientSize+" native="+r.ToRectangle()+" visible="+control.Visible+" font="+control.Font.Size+" "+control.Font.Unit);foreach(Control child in control.Controls)Dump(child,dump,depth+1);}
        internal sealed class AnimationFixture:Form {
            private readonly System.Windows.Forms.Timer clock=new System.Windows.Forms.Timer{Interval=40};private int frame;
            internal AnimationFixture(){Text="Framelet QA · moving shapes";AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(600,330);StartPosition=FormStartPosition.Manual;Location=new Point(60,70);TopMost=true;DoubleBuffered=true;Shown+=delegate{using(var g=CreateGraphics())ClientSize=new Size((int)(600*g.DpiX/96),(int)(330*g.DpiY/96));};clock.Tick+=delegate{frame++;Invalidate();};clock.Start();}
            protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Color.FromArgb(35,48,68));float scale=g.DpiX/96;using(var b=new SolidBrush(Theme.Accent))g.FillRectangle(b,(20+(frame*9)%420)*scale,110*scale,110*scale,110*scale);using(var b=new SolidBrush(Theme.Red))g.FillEllipse(b,(400-(frame*5)%350)*scale,80*scale,90*scale,90*scale);using(var f=Theme.Font(20,FontStyle.Bold))g.DrawString("FRAMELET / 动态捕获测试",f,Brushes.White,20*scale,20*scale);using(var f=Theme.Font(11,FontStyle.Regular))g.DrawString("Frame "+frame,f,Brushes.White,20*scale,270*scale);}
            protected override void Dispose(bool disposing){if(disposing)clock.Dispose();base.Dispose(disposing);}
        }
    }
}

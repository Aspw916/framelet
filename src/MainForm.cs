using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Framelet {
    internal sealed partial class MainForm : Form {
        private readonly Preferences prefs;
        private int mode;
        private readonly SoftButton[] modes = new SoftButton[3];
        private ComboBox target, preset, outputWidth, outputFps;
        private SoftButton choose, record, screenshotButton, restore, previewButton, exportButton, folderButton, playButton, cancelJob;
        private CheckBox includeCursor, countdown;
        private Label captureInfo, status, previewInfo, trimInfo, progressText, captureHint, targetLabel;
        private PreviewView preview;
        private RangeSlider range;
        private NumericUpDown trimStart, trimEnd;
        private ProgressBar progress;
        private Panel trimArea, exportArea;
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 10000 };
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 150 };
        private Rectangle region;
        private Clip clip;
        private Recorder recorder;
        private RecordingBar bar;
        private CaptureOutline outline;
        private CancellationTokenSource job, countdownJob;
        private bool busy, recording, stopping, choosing, syncing, hotkey, closing;
        private DateTime recordingStart;
        private string lastExport = "", currentPreview = "", pendingDirectory = "";
        private readonly Dictionary<ColumnStyle,float> columnUnits = new Dictionary<ColumnStyle,float>();
        private readonly Dictionary<RowStyle,float> rowUnits = new Dictionary<RowStyle,float>();
        internal MainForm() {
            SuspendLayout();
            prefs = Preferences.Read(); mode = Math.Max(0, Math.Min(2, prefs.Mode)); region = prefs.Region;captureTask=prefs.CaptureTask==1?1:0;
            Text = "帧刻 Framelet · 截图与动图工具"; Font = Theme.Font(9, FontStyle.Regular); BackColor = Theme.Background; ForeColor = Theme.Text;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96); ClientSize = new Size(1128, 760); MinimumSize = new Size(960, 690); StartPosition = FormStartPosition.CenterScreen;
            Build(); ApplyPreferences(); SetMode(mode);SetTask(captureTask); UpdateState();
            RememberUnits(this); ResumeLayout(false); PerformAutoScale();
            using(var graphics=CreateGraphics()) {
                float scale=graphics.DpiX/96; foreach(var item in columnUnits)item.Key.Width=item.Value*scale;foreach(var item in rowUnits)item.Key.Height=item.Value*scale;
                Rectangle work=Screen.FromControl(this).WorkingArea;
                MinimumSize=new Size(Math.Min((int)(960*scale),work.Width-24),Math.Min((int)(710*scale),work.Height-24));
                Size=new Size(Math.Min(Width,work.Width-24),Math.Min(Height,work.Height-24));
            }
            PerformLayout();UpdateState();
            timer.Tick += async delegate {
                if (!recording || recorder == null || stopping) return;
                double elapsed = Math.Max(0, (DateTime.Now - recordingStart).TotalSeconds);
                if (bar != null && !bar.IsDisposed) bar.Recording(Math.Min(60, elapsed));
                if (outline != null && !outline.IsDisposed) outline.Recording(Math.Min(60, elapsed));
                if (recorder.HasExited) await FinishRecording(false);
            };
            Shown += delegate {
                hotkey = Native.RegisterHotKey(Handle, 101, 0x0002 | 0x0004 | 0x4000, (uint)Keys.F8);
                if (!hotkey) SetStatus("停止快捷键被其他程序占用。请保留录制控制条，点击停止。");
                if (!File.Exists(Paths.Ffmpeg)) { SetStatus("缺少 bin\\ffmpeg.exe，请解压完整工具目录。"); record.Enabled = false; }
            };
            FormClosing += OnClosing;
        }
        private void RememberUnits(Control control) { var table=control as TableLayoutPanel;if(table!=null){foreach(ColumnStyle c in table.ColumnStyles)if(c.SizeType==SizeType.Absolute)columnUnits[c]=c.Width;foreach(RowStyle r in table.RowStyles)if(r.SizeType==SizeType.Absolute)rowUnits[r]=r.Height;}foreach(Control child in control.Controls)RememberUnits(child); }
        private NumericUpDown NumberBox() { return new NumericUpDown { DecimalPlaces = 2, Increment = .1M, Minimum = 0, Maximum = 60, Dock = DockStyle.Fill, BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0,3,0,1), Font=Theme.Font(8.5f,FontStyle.Regular), AccessibleName = "裁剪时间（秒）" }; }
        private void ApplyPreferences() {
            syncing = true; includeCursor.Checked = prefs.Cursor; countdown.Checked = prefs.Countdown;
            preset.SelectedIndex = Math.Max(0,Math.Min(3,prefs.Preset)); outputWidth.SelectedIndex = prefs.Width == 640 ? 0 : prefs.Width == 1280 ? 2 : prefs.Width == 0 ? 3 : 1; outputFps.SelectedIndex = prefs.Fps == 10 ? 0 : prefs.Fps == 20 ? 2 : 1; syncing = false;
            includeCursor.CheckedChanged += delegate { Persist(); }; countdown.CheckedChanged += delegate { Persist(); };
        }
        private int WidthSetting { get { return outputWidth.SelectedIndex == 0 ? 640 : outputWidth.SelectedIndex == 2 ? 1280 : outputWidth.SelectedIndex == 3 ? 16384 : 960; } }
        private int FpsSetting { get { return outputFps.SelectedIndex == 0 ? 10 : outputFps.SelectedIndex == 2 ? 20 : 15; } }
        private void SetPreset() {
            if (preset.SelectedIndex < 0 || preset.SelectedIndex == 3) return;
            syncing = true; outputWidth.SelectedIndex = preset.SelectedIndex; outputFps.SelectedIndex = preset.SelectedIndex; syncing = false; Persist();
        }
        private void SetMode(int value) {
            mode = value; for (int i=0; i<3; i++) { modes[i].Selected = i == mode; modes[i].Invalidate(); }
            targetLabel.Text = mode == 0 ? "目标显示器" : mode == 1 ? "目标窗口" : "选取区域";
            target.Visible = mode != 2;
            captureHint.Text = mode == 0 ? "捕获这台显示器的完整画面。" : mode == 1 ? "保持目标窗口可见，录制时不要移动。" : "框选后可调整大小，Enter 确认。";
            if (mode != 2) FillTargets(); UpdateCaptureInfo();UpdateWorkspace(); if (includeCursor != null) Persist();
        }
        private void FillTargets() {
            syncing = true; target.Items.Clear();
            if (mode == 0) { foreach (Screen s in Screen.AllScreens) target.Items.Add(new ScreenTarget(s)); if (target.Items.Count > 0) { target.SelectedIndex = 0; for (int i=0; i<target.Items.Count;i++) if (((ScreenTarget)target.Items[i]).Screen.DeviceName == prefs.ScreenName) target.SelectedIndex=i; } }
            else if (mode == 1) { foreach (WindowTarget w in Native.Windows()) target.Items.Add(w); if(target.Items.Count>0) target.SelectedIndex=0; }
            syncing = false; UpdateCaptureInfo();
        }
        private void TargetChanged() { if (syncing) return; UpdateCaptureInfo(); Persist(); }
        private Rectangle CaptureRectangle() {
            if (mode == 2) return region;
            if (mode == 0) { var s = target.SelectedItem as ScreenTarget; return s == null ? Rectangle.Empty : s.Screen.Bounds; }
            var w = target.SelectedItem as WindowTarget; return w == null || !Native.IsWindow(w.Handle) || Native.IsIconic(w.Handle) ? Rectangle.Empty : Native.WindowBounds(w.Handle);
        }
        private void UpdateCaptureInfo() { if (captureInfo == null) return; Rectangle r = CaptureRectangle();bool valid=Native.ValidRegion(r); captureInfo.Text = valid ? r.Width + " × " + r.Height + " px" : mode == 2 ? "尚未选择区域" : "请选择有效的目标";choose.Text=valid?"重新选择区域":"选择捕获区域"; }
        private async Task ChooseRegion() {
            if (busy || recording || choosing) return; choosing = true; UpdateState(); Hide();
            try { await Task.Delay(220); using(var overlay = new SelectionOverlay(region)) if (overlay.ShowDialog() == DialogResult.OK) { region = overlay.SelectedRegion; Persist(); } }
            catch(Exception ex) { Error(ex, "无法选择区域"); }
            finally { choosing = false; Show(); Activate(); UpdateCaptureInfo(); UpdateState(); }
        }
        private async Task BeginRecording() {
            if (busy || recording || choosing || countdownJob != null) return;
            SetTask(1);autoPreview.Stop();
            if (!File.Exists(Paths.Ffmpeg)) { SetStatus("请将完整工具目录解压后再打开，缺少 bin\\ffmpeg.exe。"); return; }
            if (mode == 2 && !Native.ValidRegion(region)) { await ChooseRegion(); if (!Native.ValidRegion(region)) return; }
            Rectangle r = CaptureRectangle();
            UpdateCaptureInfo();
            if (!Native.ValidRegion(r)) { SetStatus("目标超出屏幕或不可见。请将窗口移回屏幕，或重新选择区域。"); return; }
            if (!hotkey && !Screen.FromRectangle(r).WorkingArea.Contains(new Rectangle(r.Left,r.Bottom+12,528,72))) {
                MessageBox.Show(this,"停止快捷键已被占用，请保留录制控制条并点击停止。", "快捷键提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Persist(); pendingDirectory = Paths.NewSession(); countdownJob = new CancellationTokenSource(); CancellationToken token = countdownJob.Token; busy = true; UpdateState();
            try {
                var w = target.SelectedItem as WindowTarget; Hide(); if(mode==1 && w!=null) Native.SetForegroundWindow(w.Handle);
                await Task.Delay(200,token);
                if(mode==2){outline=new CaptureOutline(r);outline.Show();}
                bar = new RecordingBar(outline==null?r:outline.Bounds,hotkey); bar.StopRequested += async delegate { await FinishRecording(false); }; bar.CancelRequested += async delegate { if (countdownJob != null && !recording) countdownJob.Cancel(); else await FinishRecording(true); }; bar.Show();
                if(countdown.Checked) for(int n=3;n>0;n--) { bar.Countdown(n);if(outline!=null)outline.Countdown(n); await Task.Delay(1000,token); }
                token.ThrowIfCancellationRequested();
                // Window may have moved during the countdown: use its current physical bounds.
                if(mode==1) { r=CaptureRectangle(); if(!Native.ValidRegion(r)) throw new IOException("窗口位置已改变或已关闭，请重新选择。"); }
                recorder = new Recorder(); recorder.Start(r,includeCursor.Checked,20,Path.Combine(pendingDirectory,"recording.mkv"),60);
                recordingStart=DateTime.Now; recording=true; busy=false; bar.Recording(0);if(outline!=null)outline.Recording(0); timer.Start();
            } catch(OperationCanceledException) { CloseBar(); DiscardPending(); Show(); Activate(); SetStatus("已取消开始录制。"); }
            catch(Exception ex) { if(recorder!=null){recorder.Dispose();recorder=null;} CloseBar(); Show(); Activate(); Error(ex,"无法开始录制"); }
            finally { if(countdownJob!=null){countdownJob.Dispose();countdownJob=null;} if(!recording)busy=false; UpdateState(); }
        }
        private async Task TakeScreenshot() {
            if(busy||recording||choosing||countdownJob!=null)return;
            SetTask(0);autoPreview.Stop();
            if(mode==2&&!Native.ValidRegion(region)){await ChooseRegion();if(!Native.ValidRegion(region))return;}
            Rectangle area=CaptureRectangle();UpdateCaptureInfo();if(!Native.ValidRegion(area)){SetStatus("截图目标超出屏幕或不可见，请重新选择。");return;}
            Persist();countdownJob=new CancellationTokenSource();var token=countdownJob.Token;busy=true;UpdateState();
            try {
                var window=target.SelectedItem as WindowTarget;Hide();if(mode==1&&window!=null)Native.SetForegroundWindow(window.Handle);
                await Task.Delay(220,token);
                if(countdown.Checked){bar=new RecordingBar(area,false,true);bar.CancelRequested+=delegate{if(countdownJob!=null)countdownJob.Cancel();};bar.Show();for(int n=3;n>0;n--){bar.Countdown(n);await Task.Delay(1000,token);}}
                CloseBar();await Task.Delay(160,token);token.ThrowIfCancellationRequested();if(mode==1)area=CaptureRectangle();
                using(var image=ScreenSnapshot.Capture(area,includeCursor.Checked)) {
                    Bitmap previous=screenshot;screenshot=new Bitmap(image);if(previous!=null)previous.Dispose();
                    screenshotSavedPath="";screenshotCopied=false;preview.LoadStill(screenshot);
                }
                SetStatus("截图完成。可以复制、保存，或继续捕获。");
            } catch(OperationCanceledException){SetStatus("截图已取消。");}
            catch(Exception ex){Error(ex,"截图失败，请重试");}
            finally{CloseBar();if(countdownJob!=null){countdownJob.Dispose();countdownJob=null;}busy=false;if(!Visible){Show();Activate();}UpdateState();}
        }
        private bool CopyScreenshot() {
            if(screenshot==null||busy)return false;
            try { var data=new DataObject();data.SetData(DataFormats.Bitmap,true,screenshot);Clipboard.SetDataObject(data,true,5,100);screenshotCopied=true;SetStatus("已复制图片，可以直接粘贴。");UpdateState();return true; }
            catch(Exception ex){Error(ex,"复制失败，剪贴板可能被占用，请重试");return false;}
        }
        private async Task ChooseScreenshotSave() {
            if(screenshot==null||busy)return;
            using(var dialog=new SaveFileDialog{Title="保存截图",Filter="PNG 图片 (*.png)|*.png",DefaultExt="png",AddExtension=true,OverwritePrompt=true,FileName="帧刻-截图-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"}) {
                if(Directory.Exists(prefs.SaveDirectory))dialog.InitialDirectory=prefs.SaveDirectory;
                if(dialog.ShowDialog(this)==DialogResult.OK)await SaveScreenshotTo(dialog.FileName);
            }
        }
        private async Task<bool> SaveScreenshotTo(string path) {
            if(screenshot==null||busy)return false;autoPreview.Stop();busy=true;UpdateState();preview.SetProcessing(true,"正在保存图片…",0);
            try {
                Bitmap image=screenshot;await Task.Run(delegate{ScreenSnapshot.SavePng(image,path);});
                screenshotSavedPath=lastExport=path;prefs.SaveDirectory=Path.GetDirectoryName(path);Persist();SetStatus("已保存截图："+path);return true;
            } catch(Exception ex){Error(ex,"保存失败，图片仍在，可以换个位置重试");return false;}
            finally{busy=false;UpdateState();}
        }
        private void CloseBar() { if(bar!=null){bar.Close();bar.Dispose();bar=null;}if(outline!=null){outline.Close();outline.Dispose();outline=null;} }
        private async Task FinishRecording(bool cancelled) {
            if(!recording||stopping||recorder==null)return; stopping=true; timer.Stop(); if(bar!=null)bar.Finishing();if(outline!=null)outline.Finishing();
            try {
                await recorder.Stop(); recorder.Dispose(); recorder=null; CloseBar(); recording=false; Show(); Activate();
                if(cancelled){DiscardPending();SetStatus("本次录制已取消。上一次录制仍可恢复。"); return;}
                MediaInfo info=await Media.Probe(Path.Combine(pendingDirectory,"recording.mkv"));
                if(info.Duration<.08) throw new IOException("录制时间太短，请录制至少 0.1 秒。");
                clip=new Clip{DirectoryPath=pendingDirectory,Duration=info.Duration,Width=info.Width,Height=info.Height,CapturedAt=DateTime.Now.ToString("s"),Start=0,End=info.Duration}; clip.Write(); prefs.LastSession=clip.DirectoryPath; Persist(); LoadClipControls(); SetStatus("录制完成 · "+clip.Duration.ToString("0.00")+" 秒 · 正在生成循环预览");
            } catch(Exception ex){Error(ex,"录制未完成");}
            finally{ if(recorder!=null){recorder.Dispose();recorder=null;} recording=false; stopping=false; busy=false; CloseBar(); if(!Visible){Show();Activate();} UpdateState(); }
            if(!cancelled&&clip!=null) await MakePreview();
        }
        private async Task Restore() {
            if(busy||recording)return; Clip restored=Clip.Open(prefs.LastSession);
            if(restored==null){SetStatus("没有可恢复的录制。请先录制一段画面。");return;}
            SetTask(1);clip=restored; LoadClipControls(); UpdateState(); SetStatus("已恢复上次录制。"); await MakePreview();
        }
        private void LoadClipControls() {
            autoPreview.Stop();preview.Clear(); currentPreview="";previewDirty=false; syncing=true; trimStart.Maximum=trimEnd.Maximum=(decimal)clip.Duration; trimStart.Value=(decimal)clip.Start; trimEnd.Value=(decimal)clip.End; range.SetRange(clip.Duration,clip.Start,clip.End); syncing=false; UpdateTrimText();
        }
        private void SyncTrimFromRange() { if(clip==null)return; syncing=true; trimStart.Value=Math.Max(0,Math.Min(trimStart.Maximum,(decimal)range.Start)); trimEnd.Value=Math.Max(0,Math.Min(trimEnd.Maximum,(decimal)range.End)); syncing=false; SaveTrim(range.Start,range.End); }
        private void NumericTrim(bool changedStart) {
            if(syncing||clip==null)return; double a=(double)trimStart.Value,b=(double)trimEnd.Value,gap=Math.Min(.1,clip.Duration);
            if(b-a<gap) { if(changedStart)a=Math.Max(0,b-gap); else b=Math.Min(clip.Duration,a+gap); }
            syncing=true; trimStart.Value=(decimal)a; trimEnd.Value=(decimal)b; range.SetRange(clip.Duration,a,b); syncing=false; SaveTrim(a,b);
        }
        private void SaveTrim(double a,double b) { clip.Start=a;clip.End=b;try{clip.Write();}catch(Exception ex){Paths.Log(ex);}UpdateTrimText();SchedulePreview(); }
        private void UpdateTrimText() { if(clip==null)return;trimInfo.Text="保留 "+(clip.End-clip.Start).ToString("0.00")+"s / 共 "+clip.Duration.ToString("0.00")+"s"; }
        private async Task MakePreview() {
            autoPreview.Stop();if(clip==null||busy||recording)return;if(captureTask!=1)SetTask(1);busy=true; job=new CancellationTokenSource(); var token=job.Token; string path=Path.Combine(clip.DirectoryPath,"preview-"+Guid.NewGuid().ToString("N")+".gif"); UpdateState(); SetProgress(0,"正在更新预览");
            try {
                double a=clip.Start,b=clip.End; await Media.Gif(clip,a,b,Math.Min(760,WidthSetting),Math.Min(12,FpsSetting),path,token,delegate(int n){UiProgress(n,"正在生成预览");});
                preview.LoadGif(path);previewDirty=false; string old=currentPreview;currentPreview=path;try{if(File.Exists(old))File.Delete(old);}catch{}playButton.Text="暂停";
                SetStatus("预览已更新 · "+a.ToString("0.00")+"–"+b.ToString("0.00")+"s。可以继续裁剪或保存 GIF。");
            }catch(OperationCanceledException){SetStatus("已取消生成预览，源录制保留，可再次预览或直接导出。");TryDelete(path);}
            catch(Exception ex){Error(ex,"预览生成失败，可直接重试或保存 GIF");TryDelete(path);}
            finally{job.Dispose();job=null;busy=false;progress.Visible=false;progressText.Text="录制和导出均在本机完成";UpdateState();}
        }
        private async Task Export() {
            if(clip==null||busy||recording)return;
            autoPreview.Stop();
            string destination; using(var dialog=new SaveFileDialog{Title="保存循环 GIF",Filter="GIF 动图 (*.gif)|*.gif",DefaultExt="gif",AddExtension=true,OverwritePrompt=true,FileName="帧刻-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".gif"}) {
                if(Directory.Exists(prefs.SaveDirectory))dialog.InitialDirectory=prefs.SaveDirectory;
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;destination=dialog.FileName;
            }
            prefs.SaveDirectory=Path.GetDirectoryName(destination);Persist();busy=true;job=new CancellationTokenSource();var token=job.Token;string temp=Path.Combine(clip.DirectoryPath,"export-"+Guid.NewGuid().ToString("N")+".gif");UpdateState();SetProgress(0,"正在导出 GIF");
            try {
                await Media.Gif(clip,clip.Start,clip.End,WidthSetting,FpsSetting,temp,token,delegate(int n){UiProgress(n,"正在导出 GIF");});
                token.ThrowIfCancellationRequested(); cancelJob.Enabled=false; progressText.Text="正在保存文件…";
                await Task.Run(delegate{Media.Commit(temp,destination);});
                lastExport=destination;SetStatus("已保存："+destination);progressText.Text="完成 · "+FileSize(new FileInfo(destination).Length);
            } catch(OperationCanceledException){SetStatus("导出已取消。源录制保留，目标文件没有改动。");progressText.Text="已取消，可重新导出";}
            catch(Exception ex){Error(ex,"导出失败，源录制仍在，请重试");progressText.Text="导出失败，可重试";}
            finally{TryDelete(temp);job.Dispose();job=null;busy=false;progress.Visible=false;UpdateState();}
        }
        private void OpenFolder() { try { if(!String.IsNullOrEmpty(lastExport)&&File.Exists(lastExport))Process.Start("explorer.exe","/select,"+Paths.Quote(lastExport)); else if(Directory.Exists(prefs.SaveDirectory))Process.Start("explorer.exe",Paths.Quote(prefs.SaveDirectory)); }catch(Exception ex){Error(ex,"无法打开文件夹");} }
        internal static string FileSize(long n){return n>=1024*1024?(n/1024.0/1024).ToString("0.00")+" MB":(n/1024.0).ToString("0.0")+" KB";}
        private static void TryDelete(string p){try{File.Delete(p);}catch{}}
        private void DiscardPending(){if(String.IsNullOrEmpty(pendingDirectory)||pendingDirectory==prefs.LastSession)return;TryDelete(Path.Combine(pendingDirectory,"recording.mkv"));try{Directory.Delete(pendingDirectory,false);}catch{}}
        private void SetProgress(int n,string text){progress.Visible=true;progress.Value=Math.Max(0,Math.Min(100,n));progressText.Text=text+"  "+n+"%";preview.SetProcessing(true,text,n);}
        private void UiProgress(int n,string text){try{if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)delegate{if(busy&&!IsDisposed)SetProgress(n,text);});}catch{}}
        private void SetStatus(string text){status.Text=text;status.ForeColor=Theme.Muted;tips.SetToolTip(status,text);}
        private void Error(Exception ex,string title){Paths.Log(ex);SetStatus(title+"："+ex.Message.Split('\n')[0]);status.ForeColor=Theme.Red;tips.SetToolTip(status,title+"\n"+ex.Message);}
        private void UpdateState() {
            bool ready=!busy&&!recording&&!choosing&&!stopping;
            foreach(var b in modes)b.Enabled=ready;target.Enabled=choose.Enabled=includeCursor.Enabled=countdown.Enabled=ready;
            record.Enabled=screenshotButton.Enabled=ready;startCapture.Enabled=ready&&(captureTask==0||File.Exists(Paths.Ffmpeg));refreshTargets.Enabled=ready;restore.Enabled=ready&&!String.IsNullOrEmpty(prefs.LastSession);
            trimArea.Enabled=ready&&clip!=null&&captureTask==1;preset.Enabled=outputWidth.Enabled=outputFps.Enabled=ready;exportButton.Enabled=ready&&(captureTask==0?screenshot!=null:clip!=null);copyScreenshot.Enabled=ready&&screenshot!=null;
            folderButton.Enabled=ready&&(!String.IsNullOrEmpty(lastExport)||Directory.Exists(prefs.SaveDirectory));playButton.Enabled=preview.HasImage&&!busy;cancelJob.Visible=busy&&job!=null;cancelJob.Enabled=true;fitPreview.Enabled=preview.HasImage&&!busy;UpdateWorkspace();
        }
        private void Persist() {
            if(includeCursor==null||preset==null)return;
            prefs.Mode=mode;prefs.CaptureTask=captureTask;prefs.Region=region;prefs.Cursor=includeCursor.Checked;prefs.Countdown=countdown.Checked;prefs.Fps=FpsSetting;prefs.Width=outputWidth.SelectedIndex==3?0:WidthSetting;prefs.Preset=preset.SelectedIndex;
            var st=target.SelectedItem as ScreenTarget;if(st!=null)prefs.ScreenName=st.Screen.DeviceName;
            try{prefs.Write();}catch(Exception ex){Paths.Log(ex);SetStatus("设置保存失败，本次仍可使用："+ex.Message);}
        }
        private async void OnClosing(object sender,FormClosingEventArgs e) {
            if(closing)return;
            if(recording||busy||countdownJob!=null||stopping){e.Cancel=true;SetStatus("请先停止录制或取消当前处理任务，再关闭工具。");return;}
            closing=true;Persist();timer.Stop();timer.Dispose();autoPreview.Stop();autoPreview.Dispose();if(hotkey)Native.UnregisterHotKey(Handle,101);preview.Clear();if(screenshot!=null){screenshot.Dispose();screenshot=null;}tips.Dispose();await Task.FromResult(0);
        }
        protected override void WndProc(ref Message m) { if(m.Msg==0x0312&&m.WParam.ToInt32()==101){if(recording&&!stopping){var ignored=FinishRecording(false);}else if(countdownJob!=null)countdownJob.Cancel();}base.WndProc(ref m); }
        private sealed class ScreenTarget { internal Screen Screen; internal ScreenTarget(Screen s){Screen=s;}public override string ToString(){return Screen.DeviceName.Replace("\\\\.\\","")+"  ·  "+Screen.Bounds.Width+" × "+Screen.Bounds.Height+(Screen.Primary?"  主屏":"");} }
    }
}

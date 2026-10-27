using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Framelet {
    internal sealed partial class MainForm {
        private TableLayoutPanel workspace,sidebarStack,exportGrid;
        private Panel targetPanel,exportOptions,exportProgressPanel;
        private Card taskShell;
        private SoftButton startCapture,copyScreenshot,fitPreview,refreshTargets;
        private Label workspaceTitle,shortcutHint;
        private int captureTask;
        private Bitmap screenshot;
        private string screenshotSavedPath="";
        private bool screenshotCopied,previewDirty;
        private readonly System.Windows.Forms.Timer autoPreview=new System.Windows.Forms.Timer{Interval=700};

        private void Build() {
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(22,14,22,6),BackColor=Theme.Background};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));Controls.Add(root);
            var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,156));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(header,0,0);
            var brand=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};header.Controls.Add(brand,0,0);
            var logo=new SoftButton(""){Primary=true,IconKind=LineIcon.Photo,TabStop=false};logo.SetBounds(0,9,38,38);brand.Controls.Add(logo);
            var title=Theme.Label("帧刻",17,Theme.Text);title.Font=Theme.Font(17,FontStyle.Bold);title.SetBounds(51,2,140,32);brand.Controls.Add(title);
            var subtitle=Theme.Label("截图与动态录制",8.5f,Theme.Muted);subtitle.SetBounds(52,36,160,20);brand.Controls.Add(subtitle);
            taskShell=new Card{Size=new Size(244,44),MinimumSize=new Size(244,44),Anchor=AnchorStyles.None,Padding=new Padding(3),Margin=Padding.Empty};header.Controls.Add(taskShell,1,0);
            var tasks=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};tasks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));tasks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));tasks.RowStyles.Add(new RowStyle(SizeType.Percent,100));taskShell.Controls.Add(tasks);
            screenshotButton=new SoftButton("截图"){IconKind=LineIcon.Photo,Dock=DockStyle.Fill,Margin=Padding.Empty,Ghost=true};screenshotButton.Click+=delegate{SetTask(0);};tasks.Controls.Add(screenshotButton,0,0);
            record=new SoftButton("录制"){IconKind=LineIcon.Video,Dock=DockStyle.Fill,Margin=Padding.Empty,Ghost=true};record.Click+=delegate{SetTask(1);};tasks.Controls.Add(record,1,0);
            var privacy=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};header.Controls.Add(privacy,2,0);
            var local=Theme.Label("本机处理  /  无需联网",8.5f,Theme.Accent);local.SetBounds(0,8,156,22);local.TextAlign=ContentAlignment.MiddleRight;privacy.Controls.Add(local);
            var version=Theme.Label("FRAMELET  1.2",8,Theme.Muted);version.SetBounds(0,32,156,18);version.TextAlign=ContentAlignment.MiddleRight;privacy.Controls.Add(version);

            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,12,0,0)};body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,268));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(body,0,1);
            var sidebar=new Card{Dock=DockStyle.Fill,Margin=new Padding(0,0,16,0),Padding=new Padding(18)};body.Controls.Add(sidebar,0,0);
            var side=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};side.RowStyles.Add(new RowStyle(SizeType.Absolute,40));side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,108));sidebar.Controls.Add(side);
            var settingTitle=Theme.Label("捕获设置",11,Theme.Text);settingTitle.Font=Theme.Font(11,FontStyle.Bold);settingTitle.Dock=DockStyle.Fill;side.Controls.Add(settingTitle,0,0);
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty,BackColor=Theme.Card};side.Controls.Add(scroll,0,1);
            sidebarStack=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Margin=Padding.Empty,Padding=Padding.Empty};scroll.Controls.Add(sidebarStack);
            AddSideRow(Theme.Label("捕获范围",8.5f,Theme.Muted),22);
            var ranges=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};for(int i=0;i<3;i++)ranges.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));ranges.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            string[] names={"全屏","窗口","局部"};for(int i=0;i<3;i++){int index=i;modes[i]=new SoftButton(names[i]){Dock=DockStyle.Fill,Margin=new Padding(0,0,i==2?0:5,0),Font=Theme.Font(8.5f,FontStyle.Bold)};modes[i].Click+=delegate{SetMode(index);};ranges.Controls.Add(modes[i],i,0);}AddSideRow(ranges,36);
            AddSideRow(new Panel{BackColor=Theme.Card},14);
            targetLabel=Theme.Label("目标显示器",8.5f,Theme.Muted);AddSideRow(targetLabel,22);
            targetPanel=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
            var targetLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,34));targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));targetPanel.Controls.Add(targetLayout);
            target=new DarkComboBox{Dock=DockStyle.Fill,Margin=new Padding(0,2,5,0)};Theme.Style(target);target.SelectedIndexChanged+=delegate{TargetChanged();};targetLayout.Controls.Add(target,0,0);
            refreshTargets=new SoftButton(""){IconKind=LineIcon.Refresh,Dock=DockStyle.Fill,Margin=Padding.Empty,Ghost=true,AccessibleName="刷新目标"};refreshTargets.Click+=delegate{FillTargets();SetStatus("目标列表已刷新。");};targetLayout.Controls.Add(refreshTargets,1,0);AddSideRow(targetPanel,36);
            choose=new SoftButton("选择捕获区域"){IconKind=LineIcon.Area,Dock=DockStyle.Fill,Margin=Padding.Empty};choose.Click+=async delegate{await ChooseRegion();};AddSideRow(choose,36);
            captureInfo=Theme.Label("尚未选择区域",10,Theme.Accent);captureInfo.Font=Theme.Font(10,FontStyle.Bold);AddSideRow(captureInfo,36);
            captureHint=Theme.Label("",8.2f,Theme.Muted);captureHint.AutoEllipsis=false;captureHint.TextAlign=ContentAlignment.TopLeft;AddSideRow(captureHint,40);
            var divider=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,11,0,12),BackColor=Theme.Border};AddSideRow(divider,24,false);
            includeCursor=new ToggleSwitch{Text="显示鼠标指针",Checked=true,Dock=DockStyle.Fill,Margin=Padding.Empty};AddSideRow(includeCursor,34);
            countdown=new ToggleSwitch{Text="延迟 3 秒开始",Checked=true,Dock=DockStyle.Fill,Margin=Padding.Empty};AddSideRow(countdown,34);
            var captureActions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};captureActions.RowStyles.Add(new RowStyle(SizeType.Absolute,44));captureActions.RowStyles.Add(new RowStyle(SizeType.Absolute,36));captureActions.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.Controls.Add(captureActions,0,2);
            startCapture=new SoftButton("开始截图"){Primary=true,IconKind=LineIcon.Photo,Dock=DockStyle.Fill,Margin=Padding.Empty};startCapture.Click+=async delegate{if(captureTask==0)await TakeScreenshot();else await BeginRecording();};captureActions.Controls.Add(startCapture,0,0);
            restore=new SoftButton("恢复上次录制"){Ghost=true,IconKind=LineIcon.History,Dock=DockStyle.Fill,Margin=new Padding(0,5,0,0),Font=Theme.Font(8,FontStyle.Regular)};restore.Click+=async delegate{await Restore();};captureActions.Controls.Add(restore,0,1);
            shortcutHint=Theme.Label("截图后可直接复制",7.8f,Theme.Muted);shortcutHint.Dock=DockStyle.Fill;shortcutHint.TextAlign=ContentAlignment.MiddleCenter;captureActions.Controls.Add(shortcutHint,0,2);

            var desk=new Card{Dock=DockStyle.Fill,Padding=new Padding(12),Margin=Padding.Empty};body.Controls.Add(desk,1,0);
            workspace=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty,Padding=Padding.Empty};workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,48));workspace.RowStyles.Add(new RowStyle(SizeType.Percent,100));workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,0));workspace.RowStyles.Add(new RowStyle(SizeType.Absolute,0));desk.Controls.Add(workspace);
            var previewHead=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};previewHead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));previewHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));previewHead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80));previewHead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,74));previewHead.RowStyles.Add(new RowStyle(SizeType.Percent,100));workspace.Controls.Add(previewHead,0,0);
            workspaceTitle=Theme.Label("截图预览",10,Theme.Text);workspaceTitle.Font=Theme.Font(10,FontStyle.Bold);workspaceTitle.Dock=DockStyle.Fill;previewHead.Controls.Add(workspaceTitle,0,0);
            previewInfo=Theme.Label("PNG · 原始尺寸",8,Theme.Muted);previewInfo.Dock=DockStyle.Fill;previewHead.Controls.Add(previewInfo,1,0);
            fitPreview=new SoftButton("适应"){IconKind=LineIcon.Fit,Ghost=true,Dock=DockStyle.Fill,Margin=new Padding(0,6,5,6),Font=Theme.Font(8,FontStyle.Regular)};fitPreview.Click+=delegate{preview.ToggleSize();};previewHead.Controls.Add(fitPreview,2,0);
            playButton=new SoftButton("暂停"){IconKind=LineIcon.Pause,Ghost=true,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,6),Font=Theme.Font(8,FontStyle.Regular)};playButton.Click+=delegate{preview.Toggle();UpdateState();};previewHead.Controls.Add(playButton,3,0);
            preview=new PreviewView{Margin=new Padding(0,0,0,8)};preview.ViewChanged+=delegate{UpdateState();};workspace.Controls.Add(preview,0,1);

            trimArea=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(4,8,4,5),BackColor=Theme.Card};AddDivider(trimArea);workspace.Controls.Add(trimArea,0,2);
            var trimLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};trimLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,22));trimLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));trimLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));trimArea.Controls.Add(trimLayout);
            var trimHead=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};trimHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));trimHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));trimHead.RowStyles.Add(new RowStyle(SizeType.Percent,100));trimLayout.Controls.Add(trimHead,0,0);
            var trimTitle=Theme.Label("保留片段",8.5f,Theme.Text);trimTitle.Dock=DockStyle.Fill;trimHead.Controls.Add(trimTitle,0,0);trimInfo=Theme.Label("",8,Theme.Accent);trimInfo.Dock=DockStyle.Fill;trimInfo.TextAlign=ContentAlignment.MiddleRight;trimHead.Controls.Add(trimInfo,1,0);
            range=new RangeSlider{Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=Theme.Card};range.RangeChanged+=delegate{if(!syncing)SyncTrimFromRange();};range.EditCompleted+=delegate{SchedulePreview();};trimLayout.Controls.Add(range,0,1);
            var trimBottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=6,RowCount=1,Margin=Padding.Empty};trimBottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));trimBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));trimBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));trimBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,52));trimBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));trimBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));trimBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));trimLayout.Controls.Add(trimBottom,0,2);
            var from=Theme.Label("开始",8,Theme.Muted);from.Dock=DockStyle.Fill;from.Margin=Padding.Empty;from.AutoEllipsis=false;trimBottom.Controls.Add(from,0,0);trimStart=NumberBox();trimStart.AccessibleName="开始时间（秒）";trimStart.ValueChanged+=delegate{NumericTrim(true);};trimBottom.Controls.Add(trimStart,1,0);
            var to=Theme.Label("结束",8,Theme.Muted);to.Dock=DockStyle.Fill;to.Padding=new Padding(9,0,0,0);to.Margin=Padding.Empty;to.AutoEllipsis=false;trimBottom.Controls.Add(to,2,0);trimEnd=NumberBox();trimEnd.AccessibleName="结束时间（秒）";trimEnd.ValueChanged+=delegate{NumericTrim(false);};trimBottom.Controls.Add(trimEnd,3,0);
            previewButton=new SoftButton("更新预览"){Ghost=true,IconKind=LineIcon.Refresh,Dock=DockStyle.Fill,Margin=Padding.Empty,Font=Theme.Font(8,FontStyle.Regular)};previewButton.Click+=async delegate{await MakePreview();};trimBottom.Controls.Add(previewButton,5,0);

            exportArea=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(4,10,4,6),BackColor=Theme.Card};AddDivider(exportArea);workspace.Controls.Add(exportArea,0,3);
            exportGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,RowCount=1,Margin=Padding.Empty};exportGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,0));exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,126));exportArea.Controls.Add(exportGrid);
            var output=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,0,10,0)};exportGrid.Controls.Add(output,0,0);
            exportOptions=new Panel{Dock=DockStyle.Fill};output.Controls.Add(exportOptions);
            var options=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Margin=Padding.Empty};options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,36));options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));options.RowStyles.Add(new RowStyle(SizeType.Absolute,20));options.RowStyles.Add(new RowStyle(SizeType.Percent,100));exportOptions.Controls.Add(options);
            preset=new DarkComboBox();Theme.Style(preset);preset.Items.AddRange(new object[]{"体积小","均衡","清晰","自定义"});preset.SelectedIndexChanged+=delegate{if(!syncing)SetPreset();};
            outputWidth=new DarkComboBox();Theme.Style(outputWidth);outputWidth.Items.AddRange(new object[]{"640 px","960 px","1280 px","原始尺寸"});outputWidth.SelectedIndexChanged+=delegate{if(!syncing){preset.SelectedIndex=3;Persist();}};
            outputFps=new DarkComboBox();Theme.Style(outputFps);outputFps.Items.AddRange(new object[]{"10 帧/秒","15 帧/秒","20 帧/秒"});outputFps.SelectedIndexChanged+=delegate{if(!syncing){preset.SelectedIndex=3;Persist();}};
            ComboBox[] inputs={preset,outputWidth,outputFps};string[] labels={"画质","最大宽度","流畅度"};for(int i=0;i<3;i++){var label=Theme.Label(labels[i],7.5f,Theme.Muted);label.Dock=DockStyle.Fill;label.Margin=Padding.Empty;options.Controls.Add(label,i,0);inputs[i].Dock=DockStyle.Fill;inputs[i].Margin=new Padding(0,0,i==2?0:8,0);options.Controls.Add(inputs[i],i,1);}
            exportProgressPanel=new Panel{Dock=DockStyle.Fill,Visible=false};output.Controls.Add(exportProgressPanel);
            progressText=Theme.Label("PNG · 原始尺寸无损保存",8.5f,Theme.Muted);progressText.Dock=DockStyle.Fill;exportProgressPanel.Controls.Add(progressText);progress=new ProgressBar{Dock=DockStyle.Bottom,Height=3,Maximum=100,Visible=false,Style=ProgressBarStyle.Continuous};exportProgressPanel.Controls.Add(progress);
            cancelJob=new SoftButton("取消"){Danger=true,Dock=DockStyle.Fill,Margin=new Padding(0,14,6,8),Visible=false,Font=Theme.Font(8,FontStyle.Regular)};cancelJob.Click+=delegate{if(job!=null)job.Cancel();};exportGrid.Controls.Add(cancelJob,1,0);
            folderButton=new SoftButton(""){IconKind=LineIcon.Folder,Ghost=true,Dock=DockStyle.Fill,Margin=new Padding(0,14,6,8),AccessibleName="打开文件夹"};folderButton.Click+=delegate{OpenFolder();};exportGrid.Controls.Add(folderButton,2,0);
            copyScreenshot=new SoftButton("复制图片"){IconKind=LineIcon.Copy,Dock=DockStyle.Fill,Margin=new Padding(0,14,8,8),Font=Theme.Font(8.5f,FontStyle.Bold)};copyScreenshot.Click+=delegate{CopyScreenshot();};exportGrid.Controls.Add(copyScreenshot,3,0);
            exportButton=new SoftButton("保存 PNG"){Primary=true,IconKind=LineIcon.Save,Dock=DockStyle.Fill,Margin=new Padding(0,14,0,8)};exportButton.Click+=async delegate{if(captureTask==0)await ChooseScreenshotSave();else await Export();};exportGrid.Controls.Add(exportButton,4,0);
            status=Theme.Label("选择捕获范围，开始截图或录制。",8,Theme.Muted);status.Dock=DockStyle.Fill;status.Margin=Padding.Empty;root.Controls.Add(status,0,2);
            tips.SetToolTip(fitPreview,"切换适应窗口 / 实际像素。双击画面也可切换，原大时可拖动查看。");tips.SetToolTip(folderButton,"打开上次保存文件所在的文件夹");tips.SetToolTip(refreshTargets,"刷新目标列表");tips.SetToolTip(restore,"恢复最近一次成功录制");tips.SetToolTip(previewButton,"裁剪停止调整后会自动更新，也可以手动刷新");
            autoPreview.Tick+=async delegate{autoPreview.Stop();if(!busy&&captureTask==1&&previewDirty)await MakePreview();else if(busy&&captureTask==1&&previewDirty)autoPreview.Start();};
            KeyPreview=true;KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape&&countdownJob!=null){countdownJob.Cancel();e.Handled=true;}else if(e.Control&&e.KeyCode==Keys.C&&captureTask==0&&!busy&&screenshot!=null){e.SuppressKeyPress=true;CopyScreenshot();}else if(e.Control&&e.KeyCode==Keys.S&&!busy){e.SuppressKeyPress=true;if(captureTask==0)await ChooseScreenshotSave();else await Export();}};
        }
        private void AddSideRow(Control control,int height,bool resetMargin=true){int index=sidebarStack.RowCount;sidebarStack.RowCount=index+1;sidebarStack.RowStyles.Add(new RowStyle(SizeType.Absolute,height));control.Dock=DockStyle.Fill;if(resetMargin)control.Margin=Padding.Empty;sidebarStack.Controls.Add(control,0,index);}
        private static void AddDivider(Panel panel){panel.Paint+=delegate(object sender,PaintEventArgs e){using(var pen=new Pen(Theme.Border))e.Graphics.DrawLine(pen,0,0,panel.Width,0);};}
        private float UiScale{get{return IsHandleCreated?DeviceDpi/96f:1;}}
        private void SetTask(int task){if(busy||recording||choosing||stopping)return;autoPreview.Stop();captureTask=task==0?0:1;ShowCurrentPreview();UpdateState();Persist();if(captureTask==1&&previewDirty)SchedulePreview();}
        private void ShowCurrentPreview(){if(preview==null)return;if(captureTask==0){if(screenshot!=null)preview.LoadStill(screenshot);else preview.Clear();}else{if(!String.IsNullOrEmpty(currentPreview)&&File.Exists(currentPreview))preview.LoadGif(currentPreview);else preview.Clear();}}
        private void SchedulePreview(){autoPreview.Stop();if(captureTask==1&&clip!=null&&!recording&&!closing){previewDirty=true;autoPreview.Start();}}
        private void UpdateWorkspace(){
            if(workspace==null)return;float scale=UiScale;bool photo=captureTask==0;bool has=photo?screenshot!=null:clip!=null;
            taskShell.MinimumSize=taskShell.MaximumSize=new Size((int)(244*scale),(int)(44*scale));taskShell.Size=taskShell.MinimumSize;
            screenshotButton.Selected=photo;record.Selected=!photo;screenshotButton.Invalidate();record.Invalidate();
            startCapture.Text=photo?"开始截图":"开始录制";startCapture.IconKind=photo?LineIcon.Photo:LineIcon.Video;startCapture.Invalidate();
            shortcutHint.Text=photo?"Ctrl + C 复制 · Ctrl + S 保存":"Ctrl + Shift + F8 停止";
            workspaceTitle.Text=photo?"截图预览":"动图预览";
            preview.EmptyTitle=photo?"还没有截图":"还没有录制";preview.EmptyDescription=photo?"选择左侧范围，点击「开始截图」。":"选择左侧范围，点击「开始录制」。";preview.EmptyIcon=photo?LineIcon.Photo:LineIcon.Video;preview.Invalidate();
            previewInfo.Text=photo?(screenshot==null?"PNG · 原始尺寸":screenshot.Width+" × "+screenshot.Height+" px · "+(!String.IsNullOrEmpty(screenshotSavedPath)?"已保存":screenshotCopied?"已复制":"待保存")):(clip==null?"GIF · 最长 60 秒":clip.Width+" × "+clip.Height+" px · "+clip.Duration.ToString("0.00")+"s");
            trimArea.Visible=!photo&&clip!=null;workspace.RowStyles[2].Height=trimArea.Visible?108*scale:0;
            exportArea.Visible=has;workspace.RowStyles[3].Height=has?86*scale:0;
            exportOptions.Visible=!photo&&!busy;exportProgressPanel.Visible=photo||busy;exportProgressPanel.BringToFront();
            if(!busy&&photo)progressText.Text="PNG · 原始尺寸无损保存";
            exportGrid.ColumnStyles[1].Width=busy&&job!=null?74*scale:0;exportGrid.ColumnStyles[3].Width=photo?108*scale:0;
            copyScreenshot.Visible=photo;exportButton.Text=photo?"保存 PNG":"保存 GIF";
            playButton.Visible=!photo;playButton.IconKind=preview.Playing?LineIcon.Pause:LineIcon.Play;playButton.Text=preview.Playing?"暂停":"播放";
            fitPreview.Visible=preview.HasImage;fitPreview.Text=preview.ActualSize?"100%":"适应";fitPreview.Selected=preview.ActualSize;fitPreview.Invalidate();
            preview.SetProcessing(busy&&job!=null,"正在整理画面…",progress.Value);
            targetPanel.Visible=mode!=2;choose.Visible=mode==2;sidebarStack.RowStyles[4].Height=mode==2?0:36*scale;sidebarStack.RowStyles[5].Height=mode==2?36*scale:0;
        }
    }
}

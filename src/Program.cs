using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("帧刻 Framelet")]
[assembly: System.Reflection.AssemblyDescription("本地屏幕截图与动图捕获工具")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]

namespace Framelet {
    internal static class Program {
        [STAThread] private static int Main(string[] args) {
            Native.Dpi();
            if(args.Length>=2&&(args[0]=="--self-test"||args[0]=="--ui-test")) {
                string root=Path.GetFullPath(args[1]); Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("FRAMELET_DATA_DIR",Path.Combine(root,"test-data"));
                if(args[0]=="--self-test") { try { SelfTest.Run(root).GetAwaiter().GetResult();return 0; }catch(Exception e){File.WriteAllText(Path.Combine(root,"failure.txt"),e.ToString());return 1;} }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                return SelfTest.RunUi(root);
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0&&args[0]=="--fixture") { Application.Run(new SelfTest.AnimationFixture());return 0; }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender,System.Threading.ThreadExceptionEventArgs e){Paths.Log(e.Exception);MessageBox.Show("操作遇到问题，录制文件会保留。\n"+e.Exception.Message,"帧刻",MessageBoxButtons.OK,MessageBoxIcon.Warning);};
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender,UnhandledExceptionEventArgs e){var ex=e.ExceptionObject as Exception;if(ex!=null)Paths.Log(ex);};
            using(var form=new MainForm()) { form.Icon=AppIcon();Application.Run(form); } return 0;
        }
        internal static Icon AppIcon() {
            using(var b=new Bitmap(64,64)) using(var g=Graphics.FromImage(b)) {
                g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(Theme.Background);
                using(var p=new Pen(Theme.Accent,5)){g.DrawArc(p,9,9,46,46,24,276);g.DrawLine(p,47,9,55,9);g.DrawLine(p,55,9,55,17);}
                using(var brush=new SolidBrush(Theme.Accent))g.FillPolygon(brush,new Point[]{new Point(27,21),new Point(27,44),new Point(45,32)});
                IntPtr h=b.GetHicon();try{using(var icon=Icon.FromHandle(h))return (Icon)icon.Clone();}finally{Native.DestroyIcon(h);}
            }
        }
    }
}

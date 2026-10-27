using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Framelet {
    internal static class Paths {
        internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly string Data = DataRoot();
        internal static readonly string Ffmpeg = Path.Combine(Root, "bin", "ffmpeg.exe");
        private static string DataRoot() {
            string test = Environment.GetEnvironmentVariable("FRAMELET_DATA_DIR");
            if (!String.IsNullOrEmpty(test)) { Directory.CreateDirectory(test); return test; }
            string portable = Path.Combine(Root, "data");
            try { Directory.CreateDirectory(portable); string p = Path.Combine(portable, ".write-test"); File.WriteAllText(p, ""); File.Delete(p); return portable; }
            catch { string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Framelet"); Directory.CreateDirectory(p); return p; }
        }
        internal static string NewSession() { string p = Path.Combine(Data, "sessions", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(p); return p; }
        internal static string Number(double n) { return n.ToString("0.######", CultureInfo.InvariantCulture); }
        internal static string Quote(string value) {
            // Windows CommandLineToArgvW quoting, including trailing backslashes.
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') b.Append('\\', slashes * 2 + 1).Append(c);
                else b.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return b.Append('\\', slashes * 2).Append('"').ToString();
        }
        internal static void Log(Exception e) { try { File.AppendAllText(Path.Combine(Data, "error.log"), DateTime.Now.ToString("s") + " " + e + Environment.NewLine); } catch { } }
        internal static void Save<T>(string path, T value) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string tmp = path + ".tmp";
            using (var s = File.Create(tmp)) new DataContractJsonSerializer(typeof(T)).WriteObject(s, value);
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }
        internal static T Load<T>(string path) where T : class {
            try { using (var s = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(s); }
            catch { return null; }
        }
    }
    [DataContract] internal sealed class Preferences {
        [DataMember] public int Mode = 2;
        [DataMember] public int CaptureTask;
        [DataMember] public string ScreenName = "";
        [DataMember] public int X, Y, W, H;
        [DataMember] public bool Cursor = true;
        [DataMember] public bool Countdown = true;
        [DataMember] public int Fps = 15;
        [DataMember] public int Width = 960;
        [DataMember] public int Preset = 1;
        [DataMember] public string SaveDirectory = "";
        [DataMember] public string LastSession = "";
        internal Rectangle Region { get { return new Rectangle(X, Y, W, H); } set { X = value.X; Y = value.Y; W = value.Width; H = value.Height; } }
        internal static Preferences Read() { return Paths.Load<Preferences>(Path.Combine(Paths.Data, "settings.json")) ?? new Preferences(); }
        internal void Write() { Paths.Save(Path.Combine(Paths.Data, "settings.json"), this); }
    }
    [DataContract] internal sealed class Clip {
        [DataMember] public string DirectoryPath;
        [DataMember] public double Duration;
        [DataMember] public int Width, Height;
        [DataMember] public string CapturedAt;
        [DataMember] public double Start, End;
        internal string Video { get { return Path.Combine(DirectoryPath, "recording.mkv"); } }
        internal void Write() { Paths.Save(Path.Combine(DirectoryPath, "session.json"), this); }
        internal static Clip Open(string dir) {
            if (String.IsNullOrEmpty(dir)) return null;
            Clip c = Paths.Load<Clip>(Path.Combine(dir, "session.json"));
            if (c == null || !File.Exists(c.Video) || c.Duration <= 0) return null;
            c.Start = Math.Max(0, Math.Min(c.Start, c.Duration - .05));
            c.End = Math.Max(c.Start + .05, Math.Min(c.Duration, c.End)); return c;
        }
    }
    internal sealed class MediaInfo { internal int Width, Height; internal double Duration; }
    internal static class Media {
        internal static async Task<string> Run(string arguments, CancellationToken token, Action<double> progress, double duration) {
            return await Task.Run(delegate {
                token.ThrowIfCancellationRequested(); var log = new StringBuilder();
                using (var p = new Process()) {
                    p.StartInfo = new ProcessStartInfo(Paths.Ffmpeg, "-hide_banner -nostdin " + arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (log) { if (log.Length > 12000) log.Remove(0, 6000); log.AppendLine(e.Data); } };
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) {
                        if (progress == null || e.Data == null || duration <= 0) return;
                        long us; if (e.Data.StartsWith("out_time_us=") && Int64.TryParse(e.Data.Substring(12), out us)) progress(Math.Max(0, Math.Min(1, us / 1000000.0 / duration)));
                    };
                    p.Start(); p.BeginErrorReadLine(); p.BeginOutputReadLine();
                    using (token.Register(delegate { try { if (!p.HasExited) p.Kill(); } catch { } })) p.WaitForExit();
                    token.ThrowIfCancellationRequested();
                    if (p.ExitCode != 0) throw new IOException("媒体处理失败。" + Environment.NewLine + log.ToString());
                    return log.ToString();
                }
            });
        }
        internal static async Task<MediaInfo> Probe(string path) {
            return await Task.Run(delegate {
                var psi = new ProcessStartInfo(Paths.Ffmpeg, "-hide_banner -nostdin -i " + Paths.Quote(path)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                using (var p = Process.Start(psi)) {
                    string text = p.StandardError.ReadToEnd(); p.WaitForExit();
                    Match d = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                    Match size = Regex.Match(text, @"Video:.*?,\s*(\d{2,6})x(\d{2,6})(?:[\s,\[])");
                    if (!d.Success || !size.Success) throw new IOException("无法读取录制文件，请重录。\n" + text);
                    return new MediaInfo { Duration = Int32.Parse(d.Groups[1].Value) * 3600 + Int32.Parse(d.Groups[2].Value) * 60 + Double.Parse(d.Groups[3].Value, CultureInfo.InvariantCulture), Width = Int32.Parse(size.Groups[1].Value), Height = Int32.Parse(size.Groups[2].Value) };
                }
            });
        }
        internal static string Filter(int width, int fps) { return "fps=" + fps + ",scale=w='min(iw," + width + ")':h=-1:flags=lanczos"; }
        internal static async Task Gif(Clip clip, double start, double end, int width, int fps, string output, CancellationToken token, Action<int> progress) {
            if (start < 0 || end <= start || end > clip.Duration + .001) throw new ArgumentOutOfRangeException("裁剪范围无效");
            string palette = Path.Combine(clip.DirectoryPath, "palette-" + Guid.NewGuid().ToString("N") + ".png");
            double duration = end - start;
            string input = "-ss " + Paths.Number(start) + " -t " + Paths.Number(duration) + " -i " + Paths.Quote(clip.Video);
            string filter = Filter(width, fps);
            try {
                await Run("-y -loglevel error " + input + " -vf " + Paths.Quote(filter + ",palettegen=max_colors=256:reserve_transparent=1:stats_mode=diff") + " -frames:v 1 -update 1 -threads 2 -progress pipe:1 " + Paths.Quote(palette), token, delegate(double v) { if (progress != null) progress((int)(v * 42)); }, duration);
                if (progress != null) progress(44);
                await Run("-y -loglevel error " + input + " -i " + Paths.Quote(palette) + " -filter_complex " + Paths.Quote("[0:v]" + filter + "[v];[v][1:v]paletteuse=dither=sierra2_4a:diff_mode=rectangle") + " -loop 0 -threads 2 -filter_complex_threads 2 -progress pipe:1 " + Paths.Quote(output), token, delegate(double v) { if (progress != null) progress(44 + (int)(v * 55)); }, duration);
                token.ThrowIfCancellationRequested();
                if (!File.Exists(output) || new FileInfo(output).Length < 32) throw new IOException("导出未生成有效文件。");
                if (progress != null) progress(100);
            } finally { try { File.Delete(palette); } catch { } }
        }
        internal static void Commit(string source, string destination) {
            // Stage alongside the target for an atomic replace, even across volumes.
            string stage = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(source, stage, true); if (File.Exists(destination)) File.Replace(stage, destination, null); else File.Move(stage, destination); }
            finally { try { File.Delete(stage); } catch { } }
        }
    }
    internal sealed class Recorder : IDisposable {
        private Process process;
        private readonly StringBuilder errors = new StringBuilder();
        private long microseconds;
        internal bool HasExited { get { try { return process == null || process.HasExited; } catch { return true; } } }
        internal double Elapsed { get { return Interlocked.Read(ref microseconds) / 1000000.0; } }
        internal void Start(Rectangle r, bool cursor, int fps, string output, int maxSeconds) {
            if (!Native.ValidRegion(r)) throw new ArgumentException("捕获区域不在当前屏幕内，请重新选择。");
            string args = "-hide_banner -y -loglevel warning -f gdigrab -framerate " + fps + " -draw_mouse " + (cursor ? "1" : "0") + " -offset_x " + r.X + " -offset_y " + r.Y + " -video_size " + r.Width + "x" + r.Height + " -i desktop -t " + maxSeconds + " -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" -an -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -threads 2 -progress pipe:1 -stats_period 0.2 " + Paths.Quote(output);
            process = new Process { StartInfo = new ProcessStartInfo(Paths.Ffmpeg, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true } };
            process.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock(errors) { if (errors.Length > 8000) errors.Remove(0, 4000); errors.AppendLine(e.Data); } };
            process.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { long value; if (e.Data != null && e.Data.StartsWith("out_time_us=") && Int64.TryParse(e.Data.Substring(12), out value)) Interlocked.Exchange(ref microseconds, value); };
            process.Start(); process.BeginErrorReadLine(); process.BeginOutputReadLine();
        }
        internal async Task Stop() {
            if (process == null) return;
            try { if (!process.HasExited) { process.StandardInput.WriteLine("q"); process.StandardInput.Flush(); } } catch { }
            await Task.Run(delegate { if (!process.WaitForExit(12000)) { try { process.Kill(); } catch { } throw new IOException("停止录制超时，已结束进程。源文件保留，可尝试恢复。"); } process.WaitForExit(); });
            if (process.ExitCode != 0) throw new IOException("录制失败。请确认目标在屏幕内且没有锁屏。\n" + errors.ToString());
        }
        public void Dispose() { if (process != null) { try { if (!process.HasExited) process.Kill(); } catch { } process.Dispose(); process = null; } }
    }
}

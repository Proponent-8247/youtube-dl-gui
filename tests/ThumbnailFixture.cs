using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
internal static class ThumbnailFixture {
    [STAThread]
    private static int Main(string[] args) {
        if (args.Length == 2 && args[0] == "--log-probe") {
            var app = System.Reflection.Assembly.LoadFrom(args[1]);
            var infoType = app.GetType("murrty.logging.ExceptionInfo", true);
            var info = infoType.GetConstructor(new[] { typeof(Exception) }).Invoke(new object[] { new InvalidOperationException("audit-log-fixture") });
            infoType.GetProperty("ExceptionTime").SetValue(info, new DateTime(2026, 1, 2, 3, 4, 5), null);
            var log = app.GetType("murrty.logging.Log", true);
            log.GetProperty("AllowWritingToFile").SetValue(null, true, null);
            log.GetMethod("WriteToFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new[] { info });
            Console.Write("written");
            return 0;
        }
        if (args.Length == 2 && args[0] == "--url-probe") {
            var app = System.Reflection.Assembly.LoadFrom(args[1]);
            var method = app.GetType("youtube_dl_gui.DownloadHelper", true).GetMethod("SupportedDownloadLink");
            Console.Write(method.Invoke(null, new object[] { new string('a', 100000) }));
            return 0;
        }
        string pid = Environment.GetEnvironmentVariable("YTDL_AUDIT_PID_FILE");
        if (args.Length != 0 && args[0] == "--tool-hold") {
            File.WriteAllText(pid + ".child", Process.GetCurrentProcess().Id.ToString());
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        if (Array.IndexOf(args, "-version") >= 0) {
            if (string.IsNullOrEmpty(pid)) return 2;
            File.WriteAllText(pid, Process.GetCurrentProcess().Id.ToString());
            if (Environment.GetEnvironmentVariable("YTDL_AUDIT_TOOL_MODE") == "large") {
                Console.Write(new string('x', 50000));
                Console.Write("ImageMagick fixture");
                return 0;
            }
            Thread.Sleep(250);
            using (Process child = Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location, "--tool-hold") {
                UseShellExecute = false, CreateNoWindow = true
            })) {
                Thread.Sleep(250);
                Console.Write("ImageMagick fixture");
            }
            return 0;
        }
        string record = Environment.GetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT");
        int input = Array.IndexOf(args, "-i") + 1;
        if (string.IsNullOrEmpty(pid) || string.IsNullOrEmpty(record) || input == 0 || input >= args.Length) return 2;
        File.WriteAllText(record, args[input]);
        File.WriteAllText(pid, Process.GetCurrentProcess().Id.ToString());
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }
}

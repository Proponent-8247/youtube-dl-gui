using System;
using System.IO;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    static partial void RunToolProbeTests() {
        RunInputBoundaryTests();
        RunStartupProbeTests();
        foreach (string mode in new[] { "large", "orphan" }) {
            string capturedMode = mode;
            Test(mode == "large" ? "N009.ImageMagickProbeDrainsOutputBeforeWaiting" : "N009.ImageMagickProbeBoundsInheritedPipes", () => {
                string pid = Path.Combine(Environment.CurrentDirectory, "probe-" + Guid.NewGuid().ToString("N") + ".pid");
                Environment.SetEnvironmentVariable("YTDL_AUDIT_TOOL_MODE", capturedMode);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", pid);
                Task<bool> task = null;
                try {
                    string fixture = Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe");
                    task = Task.Run(() => (bool)Call(T("youtube_dl_gui.frmMiscTools"), null, "IsImageMagick", fixture));
                    PumpUntil(() => task.IsCompleted, 8000, "ImageMagick detection exceeded its deadline");
                    Equal(capturedMode == "large", task.GetAwaiter().GetResult());
                    AssertGone(pid);
                    if (capturedMode == "orphan") AssertGone(pid + ".child");
                }
                finally {
                    KillFixture(pid + ".child");
                    KillFixture(pid);
                    ObserveHttp(task);
                    Environment.SetEnvironmentVariable("YTDL_AUDIT_TOOL_MODE", null);
                    Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", null);
                }
            });
        }
    }
}

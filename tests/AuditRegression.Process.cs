using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

internal static partial class AuditRegression {
    private static ProcessStartInfo Fixture(string mode, string pidFile) {
        return new ProcessStartInfo(Self, "--fixture " + mode + " " + Escape(pidFile)) { UseShellExecute = false, CreateNoWindow = true };
    }
    private static object RunOwned(ProcessStartInfo info, CancellationToken token, int timeout, int limit) {
        return Call(T("murrty.controls.OwnedProcess"), null, "Run", info, token, timeout, limit);
    }
    private static void AssertGone(string file) {
        Require(File.Exists(file), "The fixture did not record its process identity");
        int pid = int.Parse(File.ReadAllText(file));
        Process child;
        try { child = Process.GetProcessById(pid); } catch (ArgumentException) { return; }
        using (child) {
            bool exited = child.WaitForExit(5000);
            if (!exited) { try { child.Kill(); child.WaitForExit(5000); } catch (InvalidOperationException) { } }
            Require(exited, "Owned fixture process survived cleanup: " + pid);
        }
    }
    private static void KillFixture(string file) {
        if (!File.Exists(file)) return;
        try { using (Process child = Process.GetProcessById(int.Parse(File.ReadAllText(file)))) { if (!child.HasExited) { child.Kill(); child.WaitForExit(5000); } } }
        catch (ArgumentException) { } catch (InvalidOperationException) { }
    }
    private static void AbortProbe(bool ffprobe) {
        string file = Path.Combine(Environment.CurrentDirectory, ffprobe ? "probe-local.pid" : "probe-remote.pid");
        string media = Path.Combine(Environment.CurrentDirectory, "fixture.media");
        File.WriteAllText(media, "not a real media library");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", "hang");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", file);
        Set(T("youtube_dl_gui.Verification"), null, "FFprobePath", Self);
        Exception fault = null;
        Thread worker = new Thread(() => {
            try {
                Type type = T(ffprobe ? "youtube_dl_gui.FfprobeData" : "youtube_dl_gui.YoutubeDlData");
                MethodInfo method = type.GetMethod("GenerateData", All, null, new[] { typeof(string), typeof(string).MakeByRefType() }, null);
                method.Invoke(null, new object[] { ffprobe ? media : "https://example.invalid/video", null });
            }
            catch (ThreadAbortException) { }
            catch (Exception e) { fault = e; }
        });
        worker.IsBackground = true;
        try {
            worker.Start();
            var clock = Stopwatch.StartNew();
            while (!File.Exists(file) && worker.IsAlive && clock.ElapsedMilliseconds < 10000) Thread.Sleep(20);
            Require(File.Exists(file), "Probe did not start: " + fault);
            worker.Abort();
            Require(worker.Join(12000), "Probe worker survived cancellation");
            AssertGone(file);
        }
        finally {
            if (worker.IsAlive) worker.Abort();
            KillFixture(file);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", null);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", null);
        }
    }
    static partial void RunProcessTests() {
        Test("D007.YoutubeProbeAbortStopsRoot", () => AbortProbe(false));
        Test("D007.FfprobeAbortStopsRoot", () => AbortProbe(true));
        Test("D007.OwnedRunnerClosesStdin", () => {
            object result = RunOwned(Fixture("stdin", "stdin.pid"), CancellationToken.None, 5000, 4096);
            Equal("EOF", Get(result, "StandardOutput"));
            Equal(0, Get(result, "ExitCode"));
        });
        Test("D007.OwnedRunnerPreservesExitAndError", () => {
            object result = RunOwned(Fixture("exit", "exit.pid"), CancellationToken.None, 5000, 4096);
            Equal(7, Get(result, "ExitCode")); Equal("fixture failure", Get(result, "StandardError"));
        });
        Test("D007.OwnedRunnerTimeoutStopsRoot", () => {
            string file = "timeout.pid";
            try { Throws<TimeoutException>(() => RunOwned(Fixture("hang", file), CancellationToken.None, 750, 4096)); AssertGone(file); }
            finally { KillFixture(file); }
        });
        Test("D007.OwnedRunnerCancellationStopsRoot", () => {
            string file = "cancel.pid";
            try {
                using (var cancellation = new CancellationTokenSource()) {
                    cancellation.CancelAfter(750);
                    Throws<OperationCanceledException>(() => RunOwned(Fixture("hang", file), cancellation.Token, 10000, 4096));
                }
                AssertGone(file);
            }
            finally { KillFixture(file); }
        });
        Test("D007.PreCancelledProbeNeverStarts", () => {
            string file = "pre-cancel.pid";
            using (var cancellation = new CancellationTokenSource()) {
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => RunOwned(Fixture("hang", file), cancellation.Token, 10000, 4096));
            }
            Require(!File.Exists(file), "Pre-cancelled operation started its child");
        });
        Test("D006.OwnedRunnerStopsDescendants", () => {
            string file = "tree.pid";
            try {
                Throws<TimeoutException>(() => RunOwned(Fixture("tree", file), CancellationToken.None, 2000, 4096));
                AssertGone(file); AssertGone(file + ".child");
            }
            finally { KillFixture(file); KillFixture(file + ".child"); }
        });
        Test("R006.MetadataOutputIsBounded", () => {
            string file = "large.pid";
            try { Throws<InvalidDataException>(() => RunOwned(Fixture("large", file), CancellationToken.None, 5000, 4096)); AssertGone(file); }
            finally { KillFixture(file); }
        });
        Test("D006.StartFailurePreservesOriginalError", () => {
            Throws<Win32Exception>(() => RunOwned(new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "not-installed.exe")), CancellationToken.None, 5000, 4096));
        });
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    private static void StopHarnessProcess(Process child) {
        if (child == null) return;
        try {
            if (child.HasExited) return;
            // taskkill /T owns the full fixture process tree on Windows/.NET Framework,
            // where Process.Kill() alone does not terminate descendants.
            string taskkill = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "taskkill.exe");
            if (File.Exists(taskkill)) {
                try {
                    ProcessStartInfo options = new ProcessStartInfo(taskkill,
                        "/T /F /PID " + child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    options.UseShellExecute = false;
                    options.CreateNoWindow = true;
                    using (Process killer = Process.Start(options)) {
                        if (killer != null && !killer.WaitForExit(3000)) {
                            killer.Kill();
                            killer.WaitForExit(1000);
                        }
                    }
                }
                catch (Exception) {
                    // Direct child kill below is the fallback if taskkill is unavailable.
                }
            }
            if (!child.WaitForExit(1000)) {
                child.Kill();
                Require(child.WaitForExit(5000), "Timed-out fixture survived process termination");
            }
        }
        catch (InvalidOperationException) { } // Already exited between checks.
    }

    private static bool WaitForHarnessChild(Process child, int timeoutMilliseconds) {
        if (child == null) throw new ArgumentNullException("child");
        if (child.WaitForExit(timeoutMilliseconds)) return true;
        StopHarnessProcess(child);
        Require(child.HasExited, "Fixture timeout returned while the child remained alive");
        return false;
    }

    private static void HarnessDeadlineTerminatesStalledProcess() {
        string pid = Path.Combine(Environment.CurrentDirectory,
            "harness-deadline-" + Guid.NewGuid().ToString("N") + ".pid");
        try {
            using (Process child = Process.Start(Fixture("hang", pid))) {
                Require(child != null, "Could not start deliberately stalled harness fixture");
                Require(!WaitForHarnessChild(child, 750),
                    "Hung fixture unexpectedly finished without a deadline");
                Require(child.HasExited, "Hung fixture was not reaped after deadline");
            }
            if (File.Exists(pid)) AssertGone(pid);
        }
        finally { KillFixture(pid); }
    }
}

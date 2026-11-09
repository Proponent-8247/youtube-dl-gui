using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static partial class AuditRegression {
    private static Dictionary<string, string> V011ReadResult(string path) {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try {
            if (!File.Exists(path)) return values;
            foreach (string line in File.ReadAllLines(path)) {
                int equals = line.IndexOf('=');
                if (equals > 0) values[line.Substring(0, equals)] = line.Substring(equals + 1);
            }
        }
        catch (IOException) { }
        return values;
    }

    private static string V011Describe(Dictionary<string, string> values) {
        if (values == null || values.Count == 0) return "<none>";
        return string.Join(", ", values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "=" + pair.Value));
    }

    private static Dictionary<string, string> V011WaitFor(string path, string key, int timeoutMilliseconds) {
        Dictionary<string, string> values = null;
        PumpUntil(() => {
            values = V011ReadResult(path);
            return values.ContainsKey(key);
        }, timeoutMilliseconds, "Timed out waiting for updater IPC evidence: " + key);
        return values;
    }

    private static Dictionary<string, string> V011WaitForOutcome(V011Scenario state, string successKey, int timeoutMilliseconds, params string[] failureKeys) {
        Dictionary<string, string> values = null;
        string terminalFailure = null;
        bool updaterExited = false;
        PumpUntil(() => {
            values = V011ReadResult(state.UpdaterResult);
            if (values.ContainsKey(successKey)) return true;
            foreach (string failureKey in failureKeys) {
                if (values.ContainsKey(failureKey)) {
                    terminalFailure = failureKey;
                    return true;
                }
            }
            try {
                if (state.Updater != null && state.Updater.HasExited) {
                    updaterExited = true;
                    return true;
                }
            }
            catch (InvalidOperationException) { }
            return false;
        }, timeoutMilliseconds, "Timed out waiting for updater IPC terminal evidence: " + successKey +
            "; updater=" + V011Describe(V011ReadResult(state.UpdaterResult)) +
            "; main=" + V011Describe(V011ReadResult(state.MainResult)));

        if (terminalFailure != null) {
            throw new Exception("Updater reached unexpected terminal state '" + terminalFailure + "' while waiting for '" + successKey +
                "'. Updater evidence: " + V011Describe(values) + "; main evidence: " + V011Describe(V011ReadResult(state.MainResult)));
        }
        if (updaterExited && !values.ContainsKey(successKey)) {
            throw new Exception("Updater exited before recording '" + successKey + "'. Updater evidence: " + V011Describe(values) +
                "; main evidence: " + V011Describe(V011ReadResult(state.MainResult)));
        }
        return values;
    }

    private static string V011Sha256(byte[] data) {
        using (SHA256 sha = SHA256.Create()) {
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }

    private static void V011Kill(Process process) {
        if (process == null) return;
        try {
            if (!process.HasExited) {
                process.Kill();
                process.WaitForExit(5000);
            }
        }
        catch { }
    }

    private static int V011FaultMainPid;
    private static int V011FaultUpdaterPid;
    private static string V011FaultRoot;

    private static bool V011ProcessSurvives(int pid) {
        try {
            using (Process process = Process.GetProcessById(pid)) return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
    }

    private sealed class V011Scenario : IDisposable {
        internal string Root;
        internal string AppPath;
        internal string MainResult;
        internal string UpdaterResult;
        internal string OriginalHash;
        internal string PayloadHash;
        internal byte[] Payload;
        internal Process Main;
        internal Process Updater;
        internal LoopbackResponse Server;

        public void Dispose() {
            V011Kill(Updater);
            // The updater may have been launched but the factory may have thrown
            // before attaching its Process wrapper. Verify the executable path
            // before terminating a PID found in the fixture's own result file.
            if (Updater == null && !string.IsNullOrEmpty(MainResult) &&
                !string.IsNullOrEmpty(Root)) {
                int pid;
                Dictionary<string, string> values = V011ReadResult(MainResult);
                string recorded;
                if (values.TryGetValue("updater-pid", out recorded) &&
                    int.TryParse(recorded, out pid) && pid > 0) {
                    try {
                        using (Process orphan = Process.GetProcessById(pid)) {
                            string expected = Path.GetFullPath(Path.Combine(Root, "youtube-dl-gui-updater.exe"));
                            string actual = Path.GetFullPath(orphan.MainModule.FileName);
                            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                                V011Kill(orphan);
                        }
                    }
                    catch (Exception) {
                        // A child that already exited may be absent; cleanup remains best-effort.
                    }
                }
            }
            // A fault can occur before the child's PID is published. Kill the
            // process tree, not only the parent, to avoid orphaned updater windows.
            StopHarnessProcess(Main);
            if (Updater != null) Updater.Dispose();
            if (Main != null) Main.Dispose();
            if (Server != null) Server.Dispose();
            if (!string.IsNullOrEmpty(Root)) {
                try { Directory.Delete(Root, true); } catch { }
            }
        }
    }

    private static V011Scenario V011Start(string scenario, bool servePayload,
        bool failAfterUpdaterPid = false, int identityPublicationDelayMs = 0) {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string debugApp = Path.Combine(root, "youtube-dl-gui", "bin", "Debug", "youtube-dl-gui.exe");
        string debugUpdater = Path.Combine(root, "youtube-dl-gui-updater", "bin", "Debug", "youtube-dl-gui-updater.exe");
        Require(File.Exists(debugApp), "Debug application executable is unavailable for V011");
        Require(File.Exists(debugUpdater), "Debug updater executable is unavailable for V011");

        var state = new V011Scenario();
        try {
        state.Root = Path.Combine(Environment.CurrentDirectory, "v011-" + scenario + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(state.Root);
        state.AppPath = Path.Combine(state.Root, "youtube-dl-gui-\u03bb.exe");
        string updaterPath = Path.Combine(state.Root, "youtube-dl-gui-updater.exe");
        File.Copy(debugApp, state.AppPath);
        File.Copy(debugUpdater, updaterPath);
        state.OriginalHash = FileSha256(state.AppPath);
        state.Payload = new byte[4096];
        for (int i = 0; i < state.Payload.Length; i++) state.Payload[i] = (byte)((i * 31 + 17) & 0xff);
        state.PayloadHash = V011Sha256(state.Payload);
        state.MainResult = Path.Combine(state.Root, "main-result.txt");
        state.UpdaterResult = Path.Combine(state.Root, "updater-result.txt");
        if (servePayload) state.Server = new LoopbackResponse(200, state.Payload, null, false, "/youtube-dl-gui.exe");

        var start = new ProcessStartInfo(state.AppPath, "--audit-updater-ipc");
        start.UseShellExecute = false;
        start.WorkingDirectory = state.Root;
        start.EnvironmentVariables["YTDL_AUDIT_IPC"] = "1";
        start.EnvironmentVariables["YTDL_AUDIT_IPC_SCENARIO"] = scenario;
        if (identityPublicationDelayMs > 0)
            start.EnvironmentVariables["YTDL_AUDIT_IPC_DELAY_IDENTITY_MS"] =
                identityPublicationDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.EnvironmentVariables["YTDL_AUDIT_IPC_HASH"] = state.PayloadHash;
        start.EnvironmentVariables["YTDL_AUDIT_IPC_MAIN_RESULT"] = state.MainResult;
        start.EnvironmentVariables["YTDL_AUDIT_IPC_UPDATER_RESULT"] = state.UpdaterResult;
        start.EnvironmentVariables["YTDL_AUDIT_UPDATER_PATH"] = updaterPath;
        start.EnvironmentVariables["YTDL_AUDIT_UPDATE_URL"] = servePayload ? state.Server.Uri.AbsoluteUri : "http://127.0.0.1:1/not-used.exe";
        state.Main = Process.Start(start);
        Require(state.Main != null, "Actual debug application process did not start");

        Dictionary<string, string> mainValues;
        try { mainValues = V011WaitFor(state.MainResult, "updater-pid", 10000); }
        catch (Exception ex) {
            throw new InvalidOperationException(
                "Updater launch did not publish PID; parent=" +
                V011Describe(V011ReadResult(state.MainResult)) +
                "; updater=" + V011Describe(V011ReadResult(state.UpdaterResult)) +
                "; parent-exited=" + state.Main.HasExited, ex);
        }
        int updaterPid;
        Require(int.TryParse(mainValues["updater-pid"], out updaterPid) && updaterPid > 0, "Actual updater process id was not recorded");
        if (failAfterUpdaterPid) {
            V011FaultMainPid = state.Main.Id;
            V011FaultUpdaterPid = updaterPid;
            V011FaultRoot = state.Root;
            throw new InvalidOperationException("Injected V011 startup failure after updater PID");
        }
        state.Updater = Process.GetProcessById(updaterPid);
        return state;
        }
        catch {
            // Covers failures in binary copy, listener startup, process launch,
            // PID evidence and Process attachment BEFORE caller ownership begins.
            state.Dispose();
            throw;
        }
    }

    private static void ActualUpdaterExecutablesExchangeBoundPacket() {
        using (V011Scenario state = V011Start("success", true)) {
            Dictionary<string, string> main = V011WaitFor(state.MainResult, "ack", 10000);
            PumpUntil(() => state.Server.Sent.WaitOne(0) || state.Updater.HasExited, 10000,
                "Actual updater never requested the loopback payload; updater=" + V011Describe(V011ReadResult(state.UpdaterResult)) +
                "; main=" + V011Describe(V011ReadResult(state.MainResult)));
            Require(state.Server.Error == null, "Loopback payload fixture failed: " + state.Server.Error);
            Dictionary<string, string> updater = V011WaitForOutcome(state, "replacement", 15000, "rollback", "cancelled");
            Require(state.Main.WaitForExit(10000), "Actual application did not exit after updater acknowledgement");
            Require(state.Updater.WaitForExit(10000), "Actual updater did not exit after replacement");
            Equal(0, state.Main.ExitCode);
            // The updater Process is attached with GetProcessById because production launches it.
            // Its clean exit is proven by WaitForExit plus the updater-owned replacement evidence;
            // Process.ExitCode is not portable for a Process object that this harness did not start.
            Equal("1", main["malformed-sent"]);
            Equal("1", updater["accepted"]);
            Equal("1", updater["ack-sent"]);
            Equal("1", updater["ack-transport"]);
            Equal("1", updater["ack-accepted"]);
            Require(updater["target"].EndsWith("youtube-dl-gui-\u03bb.exe", StringComparison.Ordinal), "Unicode application filename was not preserved through live IPC");
            Equal("9.8.7-6", updater["version"]);
            Equal(state.PayloadHash, updater["hash"]);
            Equal(state.Main.Id.ToString(), updater["app-pid"]);
            Equal(state.Updater.Id.ToString(), updater["updater-pid"]);
            Equal(main["app-ptr-size"], updater["ptr-size"]);
            Equal(main["app-update-size"], updater["update-size"]);
            Equal(main["app-copydata-size"], updater["copydata-size"]);
            Equal(state.PayloadHash, FileSha256(state.AppPath));
        }
    }

    private static void ActualUpdaterCancellationLeavesApplicationUntouched() {
        using (V011Scenario state = V011Start("cancel", false)) {
            Dictionary<string, string> main = V011WaitFor(state.MainResult, "ack", 10000);
            Dictionary<string, string> updater = V011WaitForOutcome(state, "cancelled", 10000, "rollback", "replacement");
            Require(state.Updater.WaitForExit(10000), "Actual updater did not exit after cancellation");
            Require(!state.Main.HasExited, "Cancellation unexpectedly terminated the application under test");
            Equal("1", main["malformed-sent"]);
            Equal("1", updater["accepted"]);
            Equal(state.OriginalHash, FileSha256(state.AppPath));
            Require(!File.Exists(state.AppPath + ".old"), "Cancellation left an application backup sidecar");
            Require(Directory.GetFiles(state.Root, "update.*.part").Length == 0, "Cancellation left an update partial file");
        }
    }

    private static void ActualUpdaterRollbackRestoresApplication() {
        using (V011Scenario state = V011Start("rollback", true)) {
            V011WaitFor(state.MainResult, "ack", 10000);
            PumpUntil(() => state.Server.Sent.WaitOne(0) || state.Updater.HasExited, 10000,
                "Rollback updater never requested the loopback payload; updater=" + V011Describe(V011ReadResult(state.UpdaterResult)) +
                "; main=" + V011Describe(V011ReadResult(state.MainResult)));
            Require(state.Server.Error == null, "Rollback loopback payload fixture failed: " + state.Server.Error);
            Dictionary<string, string> updater = V011WaitForOutcome(state, "rollback", 15000, "cancelled", "replacement");
            Require(state.Main.WaitForExit(10000), "Actual application did not exit for rollback scenario");
            Require(state.Updater.WaitForExit(10000), "Actual updater did not exit after rollback");
            Equal("1", updater["rollback-injected"]);
            Equal(state.OriginalHash, FileSha256(state.AppPath));
            Require(!File.Exists(state.AppPath + ".old"), "Rollback left the old application sidecar behind");
            Require(Directory.GetFiles(state.Root, "update.*.part").Length == 0, "Rollback left an update partial file");
        }
    }

    private static void UpdaterRestartNeverDiscardsSoleRecoveryExecutable() {
        string repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string appExe = Path.Combine(repo, "youtube-dl-gui", "bin", "Debug", "youtube-dl-gui.exe");
        string updaterExe = Path.Combine(repo, "youtube-dl-gui-updater", "bin", "Debug", "youtube-dl-gui-updater.exe");
        Require(File.Exists(appExe) && File.Exists(updaterExe), "Updater recovery fixtures were not built");
        Assembly assembly = Assembly.LoadFrom(updaterExe);
        Type form = assembly.GetType("youtube_dl_gui_updater.frmUpdater", true);
        MethodInfo recover = form.GetMethod("TryRestoreInterruptedBackup", BindingFlags.NonPublic | BindingFlags.Static);
        Require(recover != null, "Standalone updater has no crash-recovery preflight");
        string scratch = Path.Combine(Path.GetTempPath(), "updater-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try {
            string live = Path.Combine(scratch, "youtube-dl-gui.exe");
            string backup = live + ".old";
            byte[] valid = File.ReadAllBytes(appExe);
            // Crash after the old executable was moved, before replacement.
            File.WriteAllBytes(backup, valid);
            Equal(true, recover.Invoke(null, new object[] { live }));
            Require(File.Exists(live) && !File.Exists(backup) &&
                valid.SequenceEqual(File.ReadAllBytes(live)),
                "Updater failed to restore the only recoverable previous application");
            // A subsequent network failure cannot delete the recovered executable.
            Require(valid.SequenceEqual(File.ReadAllBytes(live)), "Recovered executable was modified");

            // Crash after a new file became live, or a pre-existing unrelated backup:
            // both files must survive for deliberate review rather than auto-deletion.
            byte[] unowned = Encoding.UTF8.GetBytes("UNOWNED USER BACKUP - NEVER REMOVE");
            File.WriteAllBytes(backup, unowned);
            Equal(false, recover.Invoke(null, new object[] { live }));
            Require(unowned.SequenceEqual(File.ReadAllBytes(backup)) &&
                valid.SequenceEqual(File.ReadAllBytes(live)),
                "Updater replaced or destroyed an ambiguous pre-existing backup");
            File.Delete(live);
            Equal(false, recover.Invoke(null, new object[] { live }));
            Require(!File.Exists(live) && unowned.SequenceEqual(File.ReadAllBytes(backup)),
                "Updater promoted unrelated or corrupt backup data into an executable");
            File.Delete(backup);
            Equal(true, recover.Invoke(null, new object[] { live }));
        }
        finally { try { Directory.Delete(scratch, true); } catch { } }
    }

    private static void UpdaterEarlyRequestRejectionIsRetriedUntilAuthenticated() {
        // Keep the real parent message loop pumping while PID publication is delayed.
        // The child can send a request before the parent knows its identity; it must
        // retry until authenticated update data arrives, not wait forever on delivery.
        using (V011Scenario state = V011Start("success", true, false, 4000)) {
            Dictionary<string, string> early = V011WaitFor(state.MainResult, "early-rejected", 10000);
            Equal("1", early["early-rejected"]);
            Dictionary<string, string> updater = V011WaitForOutcome(state, "replacement", 20000,
                "rollback", "cancelled");
            Dictionary<string, string> main = V011WaitFor(state.MainResult, "malformed-sent", 10000);
            Equal("1", main["malformed-sent"]);
            Equal("1", updater["accepted"]);
            Equal("1", updater["ack-sent"]);
            Equal("1", updater["ack-transport"]);
            Equal("1", updater["ack-accepted"]);
            Require(state.Main.WaitForExit(10000), "Parent did not complete after retried accepted handshake");
            Require(state.Updater.WaitForExit(10000), "Updater did not exit after retried accepted handshake");
            Equal(state.PayloadHash, FileSha256(state.AppPath));
        }
    }

    private static void UpdaterScenarioFactoryDisposesAfterPartialStartup() {
        V011FaultMainPid = 0;
        V011FaultUpdaterPid = 0;
        V011FaultRoot = null;
        try {
            V011Start("cancel", false, true);
            throw new Exception("V011 startup fault injection did not trigger");
        }
        catch (InvalidOperationException ex) {
            Require(ex.Message.Contains("Injected V011 startup failure"),
                "Unexpected startup fault, not the controlled injection: " + ex);
        }
        Require(V011FaultMainPid > 0 && V011FaultUpdaterPid > 0 &&
            !string.IsNullOrEmpty(V011FaultRoot), "Fault injection did not start both live processes");
        Require(!V011ProcessSurvives(V011FaultMainPid),
            "Partially constructed V011 left its main application process running");
        Require(!V011ProcessSurvives(V011FaultUpdaterPid),
            "Partially constructed V011 left its updater child process running");
        Require(!Directory.Exists(V011FaultRoot),
            "Partially constructed V011 leaked its scenario files after cleanup");
    }

    private static void RunV011Tests() {
        Test("V011.ActualUpdaterExecutablesExchangeBoundPacket", ActualUpdaterExecutablesExchangeBoundPacket);
        Test("V011.ActualUpdaterCancellationLeavesApplicationUntouched", ActualUpdaterCancellationLeavesApplicationUntouched);
        Test("V011.ActualUpdaterRollbackRestoresApplication", ActualUpdaterRollbackRestoresApplication);
        Test("V011.UpdaterRestartNeverDiscardsSoleRecoveryExecutable", UpdaterRestartNeverDiscardsSoleRecoveryExecutable);
        Test("V011.UpdaterScenarioFactoryDisposesAfterPartialStartup", UpdaterScenarioFactoryDisposesAfterPartialStartup);
        Test("V011.UpdaterEarlyRequestRejectionIsRetriedUntilAuthenticated", UpdaterEarlyRequestRejectionIsRetriedUntilAuthenticated);
    }
}

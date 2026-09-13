using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

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

    private static Dictionary<string, string> V011WaitFor(string path, string key, int timeoutMilliseconds) {
        Dictionary<string, string> values = null;
        PumpUntil(() => {
            values = V011ReadResult(path);
            return values.ContainsKey(key);
        }, timeoutMilliseconds, "Timed out waiting for updater IPC evidence: " + key);
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
            V011Kill(Main);
            if (Updater != null) Updater.Dispose();
            if (Main != null) Main.Dispose();
            if (Server != null) Server.Dispose();
            try { Directory.Delete(Root, true); } catch { }
        }
    }

    private static V011Scenario V011Start(string scenario, bool servePayload) {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string debugApp = Path.Combine(root, "youtube-dl-gui", "bin", "Debug", "youtube-dl-gui.exe");
        string debugUpdater = Path.Combine(root, "youtube-dl-gui-updater", "bin", "Debug", "youtube-dl-gui-updater.exe");
        Require(File.Exists(debugApp), "Debug application executable is unavailable for V011");
        Require(File.Exists(debugUpdater), "Debug updater executable is unavailable for V011");

        var state = new V011Scenario();
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
        start.EnvironmentVariables["YTDL_AUDIT_IPC_HASH"] = state.PayloadHash;
        start.EnvironmentVariables["YTDL_AUDIT_IPC_MAIN_RESULT"] = state.MainResult;
        start.EnvironmentVariables["YTDL_AUDIT_IPC_UPDATER_RESULT"] = state.UpdaterResult;
        start.EnvironmentVariables["YTDL_AUDIT_UPDATER_PATH"] = updaterPath;
        start.EnvironmentVariables["YTDL_AUDIT_UPDATE_URL"] = servePayload ? state.Server.Uri.AbsoluteUri : "http://127.0.0.1:1/not-used.exe";
        state.Main = Process.Start(start);
        Require(state.Main != null, "Actual debug application process did not start");

        Dictionary<string, string> mainValues = V011WaitFor(state.MainResult, "updater-pid", 10000);
        int updaterPid;
        Require(int.TryParse(mainValues["updater-pid"], out updaterPid) && updaterPid > 0, "Actual updater process id was not recorded");
        state.Updater = Process.GetProcessById(updaterPid);
        return state;
    }

    private static void ActualUpdaterExecutablesExchangeBoundPacket() {
        using (V011Scenario state = V011Start("success", true)) {
            Dictionary<string, string> main = V011WaitFor(state.MainResult, "ack", 10000);
            Dictionary<string, string> updater = V011WaitFor(state.UpdaterResult, "replacement", 15000);
            Require(state.Main.WaitForExit(10000), "Actual application did not exit after updater acknowledgement");
            Require(state.Updater.WaitForExit(10000), "Actual updater did not exit after replacement");
            Equal(0, state.Main.ExitCode);
            Equal(0, state.Updater.ExitCode);
            Equal("1", main["malformed-sent"]);
            Equal("1", updater["accepted"]);
            Equal("1", updater["ack-sent"]);
            Require(updater["target"].EndsWith("youtube-dl-gui-\u03bb.exe", StringComparison.Ordinal), "Unicode application filename was not preserved through live IPC");
            Equal("9.8.7-6", updater["version"]);
            Equal(state.PayloadHash, updater["hash"]);
            Equal(state.Main.Id.ToString(), updater["app-pid"]);
            Equal(state.Updater.Id.ToString(), updater["updater-pid"]);
            Equal(IntPtr.Size.ToString(), updater["ptr-size"]);
            Equal(Marshal.SizeOf(T("youtube_dl_gui_shared.UpdateData")).ToString(), updater["update-size"]);
            Equal(Marshal.SizeOf(T("youtube_dl_gui_shared.CopyDataStruct")).ToString(), updater["copydata-size"]);
            Equal(state.PayloadHash, FileSha256(state.AppPath));
        }
    }

    private static void ActualUpdaterCancellationLeavesApplicationUntouched() {
        using (V011Scenario state = V011Start("cancel", false)) {
            Dictionary<string, string> main = V011WaitFor(state.MainResult, "ack", 10000);
            Dictionary<string, string> updater = V011WaitFor(state.UpdaterResult, "cancelled", 10000);
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
            Dictionary<string, string> updater = V011WaitFor(state.UpdaterResult, "rollback", 15000);
            Require(state.Main.WaitForExit(10000), "Actual application did not exit for rollback scenario");
            Require(state.Updater.WaitForExit(10000), "Actual updater did not exit after rollback");
            Equal("1", updater["rollback-injected"]);
            Equal(state.OriginalHash, FileSha256(state.AppPath));
            Require(!File.Exists(state.AppPath + ".old"), "Rollback left the old application sidecar behind");
            Require(Directory.GetFiles(state.Root, "update.*.part").Length == 0, "Rollback left an update partial file");
        }
    }

    private static void RunV011Tests() {
        Test("V011.ActualUpdaterExecutablesExchangeBoundPacket", ActualUpdaterExecutablesExchangeBoundPacket);
        Test("V011.ActualUpdaterCancellationLeavesApplicationUntouched", ActualUpdaterCancellationLeavesApplicationUntouched);
        Test("V011.ActualUpdaterRollbackRestoresApplication", ActualUpdaterRollbackRestoresApplication);
    }
}

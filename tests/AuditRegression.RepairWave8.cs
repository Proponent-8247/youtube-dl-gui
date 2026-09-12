using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    private static void FailedConfigWriteDoesNotPoisonCache() {
        Type general = T("youtube_dl_gui.General");
        PropertyInfo property = general.GetProperty("UseStaticYtdl", All);
        Type ini = T("youtube_dl_gui.IniProvider");
        string iniPath = (string)ini.GetField("IniPath", All).GetValue(null);
        bool original = (bool)property.GetValue(null, null);
        bool requested = !original;
        try {
            bool failed = false;
            using (FileStream locked = new FileStream(iniPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                try { property.SetValue(null, requested, null); }
                catch (TargetInvocationException ex) {
                    if (ex.InnerException is IOException) failed = true;
                    else throw;
                }
            }
            Require(failed, "The locked INI fixture did not make the configuration write fail");
            Equal(original, property.GetValue(null, null));

            property.SetValue(null, requested, null);
            Equal(requested, property.GetValue(null, null));
            string saved = File.ReadAllText(iniPath);
            Require(saved.IndexOf("UseStaticYtdl=" + (requested ? "True" : "False"), StringComparison.OrdinalIgnoreCase) >= 0,
                "Retrying the same setting value did not persist it after the write failure was removed");
        }
        finally {
            try { property.SetValue(null, original, null); } catch { }
        }
    }

    private static object ParsedArgumentType(string[] args, out string data) {
        IList parsed = (IList)Call(T("youtube_dl_gui.Arguments"), null, "RetrieveArguments", (object)args);
        Require(parsed.Count == 1, "Expected exactly one parsed argument; got " + parsed.Count);
        object item = parsed[0];
        data = (string)item.GetType().GetField("Item2").GetValue(item);
        return item.GetType().GetField("Item1").GetValue(item);
    }

    private static void DocumentedArchiveAliasesAreParsed() {
        Type argumentType = T("youtube_dl_gui.ArgumentType");
        object video = Enum.Parse(argumentType, "DownloadArchived");
        object audio = Enum.Parse(argumentType, "DownloadArchivedNoSound");
        string operand = "archive-id";
        string[] videoAliases = { "var", "varchive", "videoarchive" };
        string[] audioAliases = { "aar", "aarchive", "audioarchive" };

        foreach (string alias in videoAliases) {
            foreach (string command in new[] { alias, "-" + alias }) {
                string data;
                Equal(video, ParsedArgumentType(new[] { command, operand }, out data));
                Equal(operand, data);
            }
            string protocolData;
            Equal(video, ParsedArgumentType(new[] { "ytdlgui:" + alias + "%20%22" + operand + "%22" }, out protocolData));
            Equal(operand, protocolData);
        }
        foreach (string alias in audioAliases) {
            foreach (string command in new[] { alias, "-" + alias }) {
                string data;
                Equal(audio, ParsedArgumentType(new[] { command, operand }, out data));
                Equal(operand, data);
            }
            string protocolData;
            Equal(audio, ParsedArgumentType(new[] { "ytdlgui:" + alias + "%20%22" + operand + "%22" }, out protocolData));
            Equal(operand, protocolData);
        }

        string compatibilityData;
        Equal(video, ParsedArgumentType(new[] { "ar", operand }, out compatibilityData));
        Equal(operand, compatibilityData);
    }

    private static void UpdaterPartialPathsAreUnique() {
        Assembly updater = LoadUpdaterAssembly();
        Type formType = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        string first = (string)Call(formType, null, "CreateUpdateDestinationPath");
        string second = (string)Call(formType, null, "CreateUpdateDestinationPath");
        Require(!string.Equals(first, second, StringComparison.OrdinalIgnoreCase), "Updater reused the same partial path for independent operations");
        Equal(Path.GetFullPath(Environment.CurrentDirectory), Path.GetDirectoryName(Path.GetFullPath(first)));
        Equal(Path.GetFullPath(Environment.CurrentDirectory), Path.GetDirectoryName(Path.GetFullPath(second)));
        Require(!string.Equals(Path.GetFileName(first), "update.part", StringComparison.OrdinalIgnoreCase), "Updater still uses the shared deterministic update.part name");
        Require(first.EndsWith(".part", StringComparison.OrdinalIgnoreCase), "Updater partial path lost the .part suffix");
        Require(second.EndsWith(".part", StringComparison.OrdinalIgnoreCase), "Updater partial path lost the .part suffix");
    }

    private static int WaitForOwnedInstaller(Process process, int timeoutMilliseconds, CancellationToken cancellation) {
        Task<int> wait = (Task<int>)Call(T("youtube_dl_gui.frmSettings"), null, "WaitForOwnedInstallerAsync", process, timeoutMilliseconds, cancellation);
        return wait.GetAwaiter().GetResult();
    }

    private static void ProtocolInstallerWaitIsBounded() {
        string exitPid = Path.Combine(Environment.CurrentDirectory, "protocol-exit-" + Guid.NewGuid().ToString("N") + ".pid");
        using (Process child = Process.Start(Fixture("exit", exitPid))) {
            Require(child != null, "Could not start protocol exit fixture");
            Equal(7, WaitForOwnedInstaller(child, 5000, CancellationToken.None));
        }

        string hangPid = Path.Combine(Environment.CurrentDirectory, "protocol-hang-" + Guid.NewGuid().ToString("N") + ".pid");
        Process hanging = null;
        try {
            hanging = Process.Start(Fixture("hang", hangPid));
            Require(hanging != null, "Could not start protocol hang fixture");
            PumpUntil(() => File.Exists(hangPid), 5000, "Protocol hang fixture did not start");
            Throws<TimeoutException>(() => WaitForOwnedInstaller(hanging, 250, CancellationToken.None));
            Require(hanging.WaitForExit(5000), "Timed-out protocol helper survived owned-process cleanup");
        }
        finally {
            KillFixture(hangPid);
            if (hanging != null) hanging.Dispose();
        }

        string cancelPid = Path.Combine(Environment.CurrentDirectory, "protocol-cancel-" + Guid.NewGuid().ToString("N") + ".pid");
        Process cancelling = null;
        using (CancellationTokenSource cancellation = new CancellationTokenSource()) {
            try {
                cancelling = Process.Start(Fixture("hang", cancelPid));
                Require(cancelling != null, "Could not start protocol cancellation fixture");
                PumpUntil(() => File.Exists(cancelPid), 5000, "Protocol cancellation fixture did not start");
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => WaitForOwnedInstaller(cancelling, 5000, cancellation.Token));
                Require(cancelling.WaitForExit(5000), "Cancelled protocol helper survived owned-process cleanup");
            }
            finally {
                KillFixture(cancelPid);
                if (cancelling != null) cancelling.Dispose();
            }
        }
    }

    private static void RunRepairWave8Tests() {
        Test("CURRENT_O016.ConfigWriteFailureCanRetry", FailedConfigWriteDoesNotPoisonCache);
        Test("F_O023.DocumentedArchiveAliasesAreParsed", DocumentedArchiveAliasesAreParsed);
        Test("CURRENT_O008.UpdaterPartialPathsAreUnique", UpdaterPartialPathsAreUnique);
        Test("F_O017.ProtocolInstallerWaitIsBounded", ProtocolInstallerWaitIsBounded);
    }
}

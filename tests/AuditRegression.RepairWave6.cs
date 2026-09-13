using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void RunCancelledGenericAttempt(string output) {
        using (Form form = (Form)New("youtube_dl_gui.frmGenericDownloadProgress", "http://127.0.0.1:1/offline", output)) {
            IntPtr handle = form.Handle;
            CancellationTokenSource cancellation = (CancellationTokenSource)Field(form, "CancelToken");
            cancellation.Cancel();
            Task task = (Task)Call(form.GetType(), form, "RunDownload");
            PumpUntil(() => task.IsCompleted, 5000, "Cancelled generic recovery attempt did not complete");
            task.GetAwaiter().GetResult();
        }
    }

    private static void AssertGenericRecoveryState(int state) {
        string output = Path.Combine(Environment.CurrentDirectory, "generic-recovery-matrix-" + state + "-" + Guid.NewGuid().ToString("N") + ".exe");
        string backup = output + ".bck";
        string temp = output + ".tmp";
        string live = "live-output-" + state;
        string prior = "backup-output-" + state;
        bool hadOutput = (state & 1) != 0;
        bool hadBackup = (state & 2) != 0;
        bool hadTemp = (state & 4) != 0;
        try {
            if (hadOutput) File.WriteAllText(output, live);
            if (hadBackup) File.WriteAllText(backup, prior);
            if (hadTemp) File.WriteAllText(temp, "partial-output-" + state);

            RunCancelledGenericAttempt(output);

            if (hadOutput) {
                Require(File.Exists(output), "State " + state + " lost the existing live output");
                Equal(live, File.ReadAllText(output));
            }
            else if (hadBackup) {
                Require(File.Exists(output), "State " + state + " did not recover the backup to the live output");
                Equal(prior, File.ReadAllText(output));
                Require(!File.Exists(backup), "State " + state + " left the recovered backup stranded");
            }
            else {
                Require(!File.Exists(output), "State " + state + " invented a live output without a recoverable prior file");
            }

            Require(!File.Exists(temp), "State " + state + " left stale temporary data after the cancelled attempt");
            if (hadOutput && hadBackup) {
                Require(File.Exists(backup), "State " + state + " unexpectedly discarded the pre-existing backup while the live output was authoritative");
                Equal(prior, File.ReadAllText(backup));
            }
        }
        finally {
            foreach (string path in new[] { output, backup, temp }) {
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void GenericDownloadRecoveryStateMatrix() {
        for (int state = 0; state < 8; state++) AssertGenericRecoveryState(state);
    }

    private static void RunRepairWave6Tests() {
        Test("F_O026.GenericDownloadRecoveryStateMatrix", GenericDownloadRecoveryStateMatrix);
    }
}

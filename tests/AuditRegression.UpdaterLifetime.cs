using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void UpdaterAsyncCloseWaitsForCancellationAndCatchesOuterErrors() {
        Assembly updater = LoadUpdaterAssembly();
        Type program = updater.GetType("youtube_dl_gui_updater.Program", true);
        Type updaterForm = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        object previousToken = program.GetProperty("CancelToken", All).GetValue(null, null);
        int previousExit = (int)program.GetProperty("ExitCode", All).GetValue(null, null);
        using (CancellationTokenSource token = new CancellationTokenSource()) {
            Form form = null;
            try {
                Set(program, null, "CancelToken", token);
                Set(program, null, "ExitCode", 0);
                form = (Form)Activator.CreateInstance(updaterForm, true);
                var blocked = new TaskCompletionSource<bool>();
                Field(form, "UpdateWorkflow", blocked.Task);

                var request = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Call(updaterForm, form, "OnFormClosing", request);
                Require(request.Cancel && token.IsCancellationRequested,
                    "User-initiated close disposed updater while its task was still active");
                Require((bool)Field(form, "CloseWhenFinished"),
                    "Pending updater close did not arm a close-after-cancellation gate");

                blocked.SetResult(true);
                var completedClose = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Call(updaterForm, form, "OnFormClosing", completedClose);
                Require(!completedClose.Cancel,
                    "Completed updater workflow still prevented normal form closure");
            }
            finally {
                if (form != null) form.Dispose();
                Set(program, null, "CancelToken", previousToken);
                Set(program, null, "ExitCode", previousExit);
            }
        }

        using (CancellationTokenSource cancelled = new CancellationTokenSource()) {
            cancelled.Cancel();
            Form form = null;
            try {
                Set(program, null, "CancelToken", cancelled);
                Set(program, null, "ExitCode", 0);
                form = (Form)Activator.CreateInstance(updaterForm, true);
                Task safeRun = (Task)Call(updaterForm, form, "RunUpdateSafelyAsync");
                Require(safeRun.Wait(2000),
                    "Pre-cancelled updater task escaped its protected outer lifetime");
                Equal(1, program.GetProperty("ExitCode", All).GetValue(null, null));
            }
            finally {
                if (form != null) form.Dispose();
                Set(program, null, "CancelToken", previousToken);
                Set(program, null, "ExitCode", previousExit);
            }
        }
    }
}

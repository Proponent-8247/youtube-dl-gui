using System;
using System.Reflection;
using System.IO;
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
    private static void UpdaterDirectFetchCloseCancelsWithoutUiException() {
        Assembly updater = LoadUpdaterAssembly();
        Type program = updater.GetType("youtube_dl_gui_updater.Program", true);
        Type updaterForm = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Type updateDataType = updater.GetType("youtube_dl_gui_shared.UpdateData", true);
        Type nullableData = typeof(Nullable<>).MakeGenericType(updateDataType);
        ConstructorInfo constructor = updaterForm.GetConstructor(new Type[] { nullableData });
        Require(constructor != null, "Could not start direct-mode updater with an empty update-data packet");

        string oldMode = Environment.GetEnvironmentVariable("YTDL_AUDIT_IPC");
        string oldScenario = Environment.GetEnvironmentVariable("YTDL_AUDIT_IPC_SCENARIO");
        string oldResult = Environment.GetEnvironmentVariable("YTDL_AUDIT_IPC_UPDATER_RESULT");
        string evidence = Path.Combine(Environment.CurrentDirectory,
            "direct-updater-cancel-" + Guid.NewGuid().ToString("N") + ".txt");
        object oldToken = program.GetProperty("CancelToken", All).GetValue(null, null);
        object oldExit = program.GetProperty("ExitCode", All).GetValue(null, null);
        Exception threadException = null;
        ThreadExceptionEventHandler threadHandler = (sender, args) => { threadException = args.Exception; };
        Application.ThreadException += threadHandler;
        Form form = null;
        using (CancellationTokenSource token = new CancellationTokenSource()) {
            try {
                Environment.SetEnvironmentVariable("YTDL_AUDIT_IPC", "1");
                Environment.SetEnvironmentVariable("YTDL_AUDIT_IPC_SCENARIO", "direct-fetch-cancel");
                Environment.SetEnvironmentVariable("YTDL_AUDIT_IPC_UPDATER_RESULT", evidence);
                Set(program, null, "CancelToken", token);
                Set(program, null, "ExitCode", 0);

                form = (Form)constructor.Invoke(new object[] { null });
                form.Show();
                PumpUntil(() => File.Exists(evidence) &&
                    File.ReadAllText(evidence).Contains("direct-fetch-pending=1"),
                    3000, "Direct-mode updater did not reach a genuinely pending GitHub release await");
                Task workflow = (Task)Field(form, "UpdateWorkflow");
                Require(workflow != null && !workflow.IsCompleted,
                    "Direct-mode test did not hold the real updater async workflow");

                form.Close();
                Require(token.IsCancellationRequested,
                    "Closing the real updater form did not cancel its active release lookup");
                PumpUntil(() => workflow.IsCompleted && form.IsDisposed, 5000,
                    "Live direct-mode form failed to finish cancellation before disposal");
                Equal(1, program.GetProperty("ExitCode", All).GetValue(null, null));
                Require(!form.IsHandleCreated,
                    "Canceled updater retained or recreated its disposed window handle");
                Require(threadException == null,
                    "A canceled release lookup escaped as an unhandled WinForms exception: " + threadException);
            }
            finally {
                if (form != null && !form.IsDisposed) form.Dispose();
                Application.ThreadException -= threadHandler;
                Set(program, null, "CancelToken", oldToken);
                Set(program, null, "ExitCode", oldExit);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_IPC", oldMode);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_IPC_SCENARIO", oldScenario);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_IPC_UPDATER_RESULT", oldResult);
                try { File.Delete(evidence); } catch { }
            }
        }
    }

}

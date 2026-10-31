using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static Task RunMiscProcess(Type formType, ProcessStartInfo info, CancellationToken cancellation) {
        object task = Call(formType, null, "RunOwnedProcessAsync", info, cancellation);
        Require(task is Task, "Misc process helper did not return a Task");
        return (Task)task;
    }

    private static object MiscProcessResult(Task task) {
        task.GetAwaiter().GetResult();
        PropertyInfo result = task.GetType().GetProperty("Result", All);
        Require(result != null, "Misc process helper task has no result");
        return result.GetValue(task, null);
    }

    private static void MiscProcessCloseCancelsOwnedTree() {
        Type formType = T("youtube_dl_gui.frmMiscTools");
        object form = Activator.CreateInstance(formType);
        FieldInfo cancellationField = formType.GetField("MiscOperationCancellation", All);
        Require(cancellationField != null, "Misc Tools has no owner-lifetime cancellation source");
        CancellationTokenSource cancellation = cancellationField.GetValue(form) as CancellationTokenSource;
        Require(cancellation != null, "Misc Tools cancellation source was not initialized");

        string pid = Path.Combine(Environment.CurrentDirectory, "misc-tree-" + Guid.NewGuid().ToString("N") + ".pid");
        Task task = null;
        try {
            task = RunMiscProcess(formType, Fixture("tree", pid), cancellation.Token);
            PumpUntil(() => File.Exists(pid), 5000, "Misc Tools process fixture did not start");
            PumpUntil(() => File.Exists(pid + ".child"), 5000, "Misc Tools process fixture did not create its descendant");

            Call(formType, form, "frmTools_FormClosing", form,
                new FormClosingEventArgs(CloseReason.UserClosing, false));
            Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            AssertGone(pid);
            AssertGone(pid + ".child");
        }
        finally {
            KillFixture(pid + ".child");
            KillFixture(pid);
            IDisposable disposable = form as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }

    private static void MiscProcessStartFailureIsContained() {
        Type formType = T("youtube_dl_gui.frmMiscTools");
        ProcessStartInfo missing = new ProcessStartInfo(
            Path.Combine(Environment.CurrentDirectory, "definitely-not-installed-" + Guid.NewGuid().ToString("N") + ".exe")) {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Task task = RunMiscProcess(formType, missing, CancellationToken.None);
        Equal(null, MiscProcessResult(task));
    }

    private static void RunRepairWave10Tests() {
        Test("CURRENT_O006.MiscProcessCloseCancelsOwnedTree", MiscProcessCloseCancelsOwnedTree);
        Test("CURRENT_O036.MiscProcessStartFailureIsContained", MiscProcessStartFailureIsContained);
        RunRepairWave11Tests();
    }
}

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void SettingsToolOperationOutlivesSettings() {
        Type settings = T("youtube_dl_gui.frmSettings");
        Type program = T("youtube_dl_gui.Program");
        using (ManualResetEventSlim entered = new ManualResetEventSlim(false))
        using (ManualResetEventSlim release = new ManualResetEventSlim(false)) {
            Func<Task> work = () => Task.Run(() => {
                entered.Set();
                release.Wait(5000);
            });

            Task operation;
            using (Form form = (Form)Activator.CreateInstance(settings)) {
                operation = (Task)Call(settings, null, "RunApplicationOwnedToolOperationAsync", work);
                PumpUntil(() => entered.IsSet, 5000, "Application-owned tool operation did not start");
                Equal(true, ProgramHasRunningActions(program));
                form.Dispose();
                Equal(true, ProgramHasRunningActions(program));
            }

            release.Set();
            operation.GetAwaiter().GetResult();
            Equal(false, ProgramHasRunningActions(program));
        }
    }

    private static void RunRepairWave13Tests() {
        Test("POLICY_O022.SettingsToolOperationOutlivesSettings", SettingsToolOperationOutlivesSettings);
        RunRepairWave14Tests();
    }
}

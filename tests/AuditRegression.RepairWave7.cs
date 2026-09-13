using System;
using System.Threading;

internal static partial class AuditRegression {
    private static Mutex CreateAbandonedMutex(string name) {
        Mutex abandoned = null;
        using (ManualResetEvent ready = new ManualResetEvent(false)) {
            Thread owner = new Thread(delegate() {
                abandoned = new Mutex(false, name);
                abandoned.WaitOne();
                ready.Set();
            });
            owner.IsBackground = true;
            owner.Start();
            Require(ready.WaitOne(3000), "Abandoned-mutex fixture did not acquire the named mutex");
            Require(owner.Join(3000), "Abandoned-mutex owner thread did not terminate");
        }
        return abandoned;
    }

    private static void LastHandleClosureRemovesAbandonedState() {
        string programName = (string)T("youtube_dl_gui.Program").GetProperty("ProgramGUID", All).GetValue(null, null);
        string name = programName + "-audit-" + Guid.NewGuid().ToString("N");
        Mutex abandoned = CreateAbandonedMutex(name);

        // The abandoned state is attached to this kernel object. When the last
        // handle closes, the named object is destroyed; a later opener creates
        // a fresh mutex rather than inheriting the abandoned state.
        abandoned.Dispose();

        using (Mutex next = new Mutex(false, name)) {
            bool acquired;
            try {
                acquired = next.WaitOne(0, true);
            }
            catch (AbandonedMutexException) {
                throw new Exception("A fresh named mutex inherited abandoned state after its last prior handle was closed");
            }
            Require(acquired, "A fresh named mutex could not be acquired after the prior object's last handle was closed");
            next.ReleaseMutex();
        }
    }

    private static void RunRepairWave7Tests() {
        Test("W_O021.LastHandleClosureRemovesAbandonedState", LastHandleClosureRemovesAbandonedState);
        RunRepairWave8Tests();
    }
}

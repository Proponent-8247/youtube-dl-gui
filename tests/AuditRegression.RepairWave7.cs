using System;
using System.Reflection;
using System.Threading;

internal static partial class AuditRegression {
    private static bool AcquireApplicationMutex(Type program) {
        MethodInfo acquire = program.GetMethod("TryAcquireSingleInstanceMutex", All);
        Require(acquire != null, "Program does not expose a testable single-instance acquisition boundary");
        try { return (bool)acquire.Invoke(null, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    private static Mutex ApplicationMutex(Type program) {
        PropertyInfo property = program.GetProperty("Instance", All);
        Require(property != null, "Program.Instance mutex property is missing");
        return (Mutex)property.GetValue(null, null);
    }

    private static void ClearApplicationMutex(Type program, bool release) {
        PropertyInfo property = program.GetProperty("Instance", All);
        Mutex instance = property == null ? null : (Mutex)property.GetValue(null, null);
        if (instance != null) {
            if (release) instance.ReleaseMutex();
            instance.Dispose();
            property.SetValue(null, null, null);
        }
    }

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

    private static bool OtherThreadCanAcquire(string name) {
        bool acquired = false;
        Exception failure = null;
        Thread probe = new Thread(delegate() {
            try {
                using (Mutex mutex = new Mutex(false, name)) {
                    try { acquired = mutex.WaitOne(0, true); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (acquired) mutex.ReleaseMutex();
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        probe.IsBackground = true;
        probe.Start();
        Require(probe.Join(3000), "Single-instance mutex probe did not terminate");
        if (failure != null) throw failure;
        return acquired;
    }

    private static void SingleInstanceMutexRecoversAfterAbandonment() {
        Type program = T("youtube_dl_gui.Program");
        MethodInfo acquire = program.GetMethod("TryAcquireSingleInstanceMutex", All);
        Require(acquire != null, "Program does not expose a testable single-instance acquisition boundary");
        string name = (string)program.GetProperty("ProgramGUID", All).GetValue(null, null);

        ClearApplicationMutex(program, false);
        for (int iteration = 0; iteration < 2; iteration++) {
            Mutex abandoned = CreateAbandonedMutex(name);
            bool ownsApplicationMutex = false;
            try {
                Require(AcquireApplicationMutex(program), "Application rejected an abandoned single-instance mutex");
                ownsApplicationMutex = true;
                Require(ApplicationMutex(program) != null, "Application did not retain the recovered mutex handle");
                ClearApplicationMutex(program, true);
                ownsApplicationMutex = false;
                Require(OtherThreadCanAcquire(name), "One ReleaseMutex did not fully release recovered ownership");
            }
            finally {
                if (ownsApplicationMutex) {
                    try { ClearApplicationMutex(program, true); } catch { }
                }
                abandoned.Dispose();
            }
        }

        bool ownsFreshMutex = false;
        try {
            Require(AcquireApplicationMutex(program), "Normal first-instance acquisition failed");
            ownsFreshMutex = true;
            Require(!OtherThreadCanAcquire(name), "A normal second instance could acquire the live application's mutex");
            ClearApplicationMutex(program, true);
            ownsFreshMutex = false;

            Require(AcquireApplicationMutex(program), "Orderly release did not allow a subsequent launch to acquire the mutex");
            ownsFreshMutex = true;
            ClearApplicationMutex(program, true);
            ownsFreshMutex = false;
        }
        finally {
            if (ownsFreshMutex) {
                try { ClearApplicationMutex(program, true); } catch { }
            }
        }
    }

    private static void RunRepairWave7Tests() {
        Test("CURRENT_O021.SingleInstanceMutexRecoversAfterAbandonment", SingleInstanceMutexRecoversAfterAbandonment);
    }
}

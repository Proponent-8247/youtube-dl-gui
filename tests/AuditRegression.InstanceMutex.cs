using System;
using System.IO;
using System.Threading;

internal static partial class AuditRegression {
    private static void AbandonedInstanceMutexIsRecoveredWithoutRecursiveAcquisition() {
        Type program = T("youtube_dl_gui.Program");
        string name = "Local\\ytdl-audit-mutex-" + Guid.NewGuid().ToString("N");
        using (Mutex abandoned = new Mutex(false, name))
        using (ManualResetEvent acquired = new ManualResetEvent(false)) {
            Thread owner = new Thread(() => {
                abandoned.WaitOne();
                acquired.Set();
                // Exit without releasing to intentionally abandon the OS mutex.
            });
            owner.IsBackground = true;
            owner.Start();
            Require(acquired.WaitOne(5000) && owner.Join(5000),
                "Mutex ownership fixture never finished");

            using (Mutex replacement = new Mutex(false, name)) {
                Equal(true, Call(program, null, "TryAcquireInstanceMutex", replacement));
                // The helper acquired once despite the AbandonedMutexException.
                bool contenderEntered = false;
                Thread competitor = new Thread(() => {
                    using (Mutex contender = new Mutex(false, name)) {
                        try {
                            contenderEntered = contender.WaitOne(0);
                            if (contenderEntered) contender.ReleaseMutex();
                        }
                        catch (AbandonedMutexException) {
                            contenderEntered = true;
                            contender.ReleaseMutex();
                        }
                    }
                });
                competitor.IsBackground = true;
                competitor.Start();
                Require(competitor.Join(5000), "Contender did not finish its mutex probe");
                Require(!contenderEntered,
                    "Abandoned mutex recovery did not preserve exclusive instance ownership");
                replacement.ReleaseMutex();
                Require(replacement.WaitOne(1000),
                    "Single mutex release failed to restore availability for the next instance");
                replacement.ReleaseMutex();
            }
        }

        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string source = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Program.cs"));
        Require(source.Contains("Instance = new(false, ProgramGUID)") &&
            source.Contains("TryAcquireInstanceMutex(Instance)"),
            "Release startup still double-acquires its named instance mutex");
    }
}

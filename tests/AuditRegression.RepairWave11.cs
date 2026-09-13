using System;
using System.IO;
using System.Reflection;
using System.Threading;

internal static partial class AuditRegression {
    private static bool ProgramHasRunningActions(Type program) {
        PropertyInfo property = program.GetProperty("HasRunningActions", All);
        Require(property != null, "Program has no combined running-action state");
        return (bool)property.GetValue(null, null);
    }

    private static void BatchWorkerIsTrackedAndSta() {
        Type main = T("youtube_dl_gui.frmMain");
        Type program = T("youtube_dl_gui.Program");
        using (ManualResetEventSlim entered = new ManualResetEventSlim(false))
        using (ManualResetEventSlim release = new ManualResetEventSlim(false)) {
            bool sawSta = false;
            bool sawTracked = false;

            Action work = () => {
                sawSta = Thread.CurrentThread.GetApartmentState() == ApartmentState.STA;
                sawTracked = ProgramHasRunningActions(program);
                entered.Set();
                release.Wait(5000);
            };

            Thread worker = null;
            try {
                worker = (Thread)Call(main, null, "StartBatchWorker", work);
                Require(worker != null, "Batch worker helper did not return its thread");
                PumpUntil(() => entered.IsSet, 5000, "Batch worker did not start");
                Equal(true, ProgramHasRunningActions(program));
                Equal(true, sawSta);
                Equal(true, sawTracked);
            }
            finally {
                release.Set();
                if (worker != null) {
                    Require(worker.Join(5000), "Batch worker did not finish after release");
                }
            }

            Equal(false, ProgramHasRunningActions(program));
        }
    }

    private static void FfmpegArchiveCleanupIsBestEffort() {
        Type updater = T("youtube_dl_gui.Updater");
        string path = Path.Combine(Environment.CurrentDirectory, "ffmpeg-cleanup-" + Guid.NewGuid().ToString("N") + ".zip");
        File.WriteAllText(path, "audit");
        try {
            using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
                Equal(false, Call(updater, null, "TryDeleteFfmpegArchive", path));
                Equal(true, File.Exists(path));
            }
            Equal(true, Call(updater, null, "TryDeleteFfmpegArchive", path));
            Equal(false, File.Exists(path));
        }
        finally {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void RunRepairWave11Tests() {
        Test("CURRENT_O024.BatchWorkerIsTrackedAndSta", BatchWorkerIsTrackedAndSta);
        Test("CURRENT_O023.FfmpegArchiveCleanupIsBestEffort", FfmpegArchiveCleanupIsBestEffort);
        RunRepairWave12Tests();
    }
}

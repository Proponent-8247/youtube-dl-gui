#nullable disable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace youtube_dl_gui.History {
    // A monitor independent of the form's worker owns the lease. Aborting a UI worker
    // must not release the archive lock while yt-dlp or its postprocessors still run.
    internal sealed class HistoryProcessGuard {
        private readonly TaskCompletionSource<HistoryReport> completion = new TaskCompletionSource<HistoryReport>();
        private static int activeCount;
        public static int ActiveCount { get { return Volatile.Read(ref activeCount); } }
        public Task<HistoryReport> Completion { get { return completion.Task; } }

        public static HistoryProcessGuard Start(Process process, HistoryOptions options, Func<bool> cancelled, Action<string> notify) {
            if (process == null || options == null) throw new ArgumentNullException();
            options.Cancelled = cancelled;
            var guard = new HistoryProcessGuard();
            HistoryStore store = null;
            HistoryJob job = null;
            Process observer = null;
            bool prepared = false;
            bool started = false;
            bool handedOff = false;
            bool notified = false;
            Interlocked.Increment(ref activeCount);
            try {
                while (store == null) {
                    if (cancelled()) throw new OperationCanceledException("Protected download cancelled while waiting for the library.");
                    try { store = HistoryStore.Open(options); }
                    catch (HistoryBusyException) {
                        if (!notified) { notify("Waiting for the other protected job to release this library/archive."); notified = true; }
                        Thread.Sleep(200);
                    }
                }
                notify("Validating Download History before launching the downloader.");
                store.BeginRun();
                prepared = true;
                if (cancelled()) throw new OperationCanceledException("Protected download cancelled before launch.");
                job = new HistoryJob();
                // Thread.Abort is deferred in finally: no abort can strand a started child
                // between Process.Start and handing its job/lease to the monitor.
                try { }
                finally {
                    if (!process.Start()) throw new HistoryException("The downloader process did not start.");
                    started = true;
                    job.Assign(process);
                    store.TrackProcess(process);
                    try {
                        observer = Process.GetProcessById(process.Id);
                        // Acquire a separate kernel handle before the UI can dispose its Process.
                        _ = observer.Handle;
                        if (observer.StartTime.ToUniversalTime() != process.StartTime.ToUniversalTime())
                            throw new HistoryException("Downloader process identity changed during launch.");
                    }
                    catch (Exception ex) when ((ex is ArgumentException || ex is InvalidOperationException || ex is Win32Exception) && process.HasExited) {
                        if (observer != null) observer.Dispose();
                        observer = null;
                    }
                    var ownedStore = store;
                    var ownedJob = job;
                    var ownedObserver = observer;
                    var monitor = new Thread(() => guard.Monitor(ownedStore, ownedJob, ownedObserver)) {
                        IsBackground = true,
                        Name = "Download History process monitor"
                    };
                    monitor.Start();
                    handedOff = true;
                }
                return guard;
            }
            finally {
                if (!handedOff) {
                    try {
                        if (started) {
                            try { if (!process.HasExited) process.Kill(); process.WaitForExit(10000); }
                            catch (InvalidOperationException) { }
                        }
                        if (job != null) job.StopRemaining();
                        if (prepared) store.FinishRun();
                    }
                    catch {
                        if (prepared) store.RequireManualRecovery();
                        throw;
                    }
                    finally {
                        if (observer != null) observer.Dispose();
                        if (job != null) job.Dispose();
                        if (store != null) store.Dispose();
                        Interlocked.Decrement(ref activeCount);
                    }
                }
            }
        }
        private void Monitor(HistoryStore store, HistoryJob job, Process observer) {
            HistoryReport report = null;
            Exception failure = null;
            try {
                if (observer != null) observer.WaitForExit();
                // A crashed downloader can leave an ffmpeg child. Reap the whole job before
                // scanning final files or allowing the next protected worker to acquire it.
                job.StopRemaining();
                report = store.FinishRun();
            }
            catch (Exception ex) {
                failure = ex;
                try { store.RequireManualRecovery(); }
                catch (Exception markerError) { failure = new AggregateException(ex, markerError); }
            }
            finally {
                if (observer != null) observer.Dispose();
                job.Dispose();
                store.Dispose();
                Interlocked.Decrement(ref activeCount);
                if (failure == null) completion.TrySetResult(report);
                else completion.TrySetException(failure);
            }
        }
    }

    internal sealed class HistoryJob : IDisposable {
        private readonly JobHandle handle;
        public HistoryJob() {
            handle = CreateJobObject(IntPtr.Zero, null);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create a protected downloader job.");
            var limits = new ExtendedLimits();
            limits.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimits)))) {
                int error = Marshal.GetLastWin32Error(); handle.Dispose();
                throw new Win32Exception(error, "Cannot enforce protected downloader process cleanup.");
            }
        }
        public void Assign(Process process) {
            if (!AssignProcessToJobObject(handle, process.Handle)) {
                int error = Marshal.GetLastWin32Error();
                if (!process.HasExited) throw new Win32Exception(error,
                    "Cannot isolate the protected downloader. On Windows 7, an existing host job may prevent this. Download History cannot safely continue in this host.");
            }
        }
        public void StopRemaining() {
            if (!TerminateJobObject(handle, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var timeout = Stopwatch.StartNew();
            while (true) {
                Accounting accounting;
                uint returned;
                if (!QueryInformationJobObject(handle, 1, out accounting, (uint)Marshal.SizeOf(typeof(Accounting)), out returned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (accounting.ActiveProcesses == 0) return;
                if (timeout.ElapsedMilliseconds > 10000) throw new HistoryException("A protected postprocessor could not be stopped. Manual recovery is required.");
                Thread.Sleep(20);
            }
        }
        public void Dispose() { handle.Dispose(); }

        private sealed class JobHandle : SafeHandleZeroOrMinusOneIsInvalid {
            public JobHandle() : base(true) { }
            protected override bool ReleaseHandle() { return CloseHandle(handle); }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits {
            public long ProcessTime, JobTime;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters {
            public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits {
            public BasicLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Accounting {
            public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
            public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern JobHandle CreateJobObject(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(JobHandle job, int infoClass, ref ExtendedLimits limits, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(JobHandle job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(JobHandle job, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(JobHandle job, int infoClass, out Accounting accounting, uint size, out uint returned);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr value);
    }
}

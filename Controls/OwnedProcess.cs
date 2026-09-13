#nullable enable
namespace murrty.controls;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

/// <summary>Owns finite helper/probe executions, including their redirected pipes.</summary>
internal static class OwnedProcess {
    internal sealed class Result {
        public string StandardOutput { get; }
        public string StandardError { get; }
        public int ExitCode { get; }
        internal Result(string Output, string Error, int ExitCode) {
            StandardOutput = Output;
            StandardError = Error;
            this.ExitCode = ExitCode;
        }
    }

    private sealed class OutputBudget(int Limit) {
        private int Remaining = Limit;
        internal void Consume(int Count) {
            lock (this) {
                if (Count > Remaining) {
                    throw new InvalidDataException("The helper exceeded the permitted metadata/output size.");
                }
                Remaining -= Count;
            }
        }
    }

    internal static Result Run(ProcessStartInfo StartInfo, CancellationToken Cancellation = default,
        int TimeoutMilliseconds = 300_000, int MaximumOutputCharacters = 32 * 1024 * 1024) {
        if (StartInfo is null) throw new ArgumentNullException(nameof(StartInfo));
        if (TimeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(TimeoutMilliseconds));
        if (MaximumOutputCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumOutputCharacters));
        Cancellation.ThrowIfCancellationRequested();

        StartInfo.UseShellExecute = false;
        StartInfo.RedirectStandardInput = true;
        StartInfo.RedirectStandardOutput = true;
        StartInfo.RedirectStandardError = true;
        StartInfo.CreateNoWindow = true;
        StartInfo.StandardOutputEncoding = Encoding.UTF8;
        StartInfo.StandardErrorEncoding = Encoding.UTF8;

        using Process Child = new() { StartInfo = StartInfo };
        // Ownership exists before Start: failures in Start/pipe setup must also attempt cleanup.
        using ProcessOwnership Ownership = new(Child);
        Task<string>? Output = null;
        Task<string>? Error = null;
        Stopwatch Clock = Stopwatch.StartNew();
        try {
            Cancellation.ThrowIfCancellationRequested();
            Child.Start();
            Ownership.Attach();
            Child.StandardInput.Close();
            OutputBudget Budget = new(MaximumOutputCharacters);
            Output = ReadBounded(Child.StandardOutput, Budget);
            Error = ReadBounded(Child.StandardError, Budget);

            // The deadline covers both process execution and pipe draining. A descendant
            // inheriting a pipe must not turn a timed wait into an unbounded WaitForExit().
            while (true) {
                Cancellation.ThrowIfCancellationRequested();
                if (Output.IsFaulted) Output.GetAwaiter().GetResult();
                if (Error.IsFaulted) Error.GetAwaiter().GetResult();
                if (Child.HasExited && Output.IsCompleted && Error.IsCompleted) {
                    Cancellation.ThrowIfCancellationRequested();
                    return new(Output.GetAwaiter().GetResult(), Error.GetAwaiter().GetResult(), Child.ExitCode);
                }
                if (Clock.ElapsedMilliseconds >= TimeoutMilliseconds) {
                    throw new TimeoutException("The helper did not complete within the permitted time.");
                }
                Thread.Sleep(25);
            }
        }
        finally {
            // Root termination does not depend on cancelling reads or enumerating children.
            Ownership.Dispose();
            try { Child.StandardOutput.Dispose(); } catch (InvalidOperationException) { }
            try { Child.StandardError.Dispose(); } catch (InvalidOperationException) { }
            Observe(Output);
            Observe(Error);
        }
    }

    private static async Task<string> ReadBounded(StreamReader Reader, OutputBudget Budget) {
        char[] Buffer = new char[4096];
        StringBuilder Text = new();
        int Count;
        while ((Count = await Reader.ReadAsync(Buffer, 0, Buffer.Length).ConfigureAwait(false)) != 0) {
            Budget.Consume(Count);
            Text.Append(Buffer, 0, Count);
        }
        return Text.ToString();
    }

    private static void Observe(Task? Pending) {
        if (Pending is not null) {
            _ = Pending.ContinueWith(Failed => { _ = Failed.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}

/// <summary>
/// Best-effort lifetime containment for a process we created, not a sandbox for hostile code.
/// Windows 7 can refuse nested jobs; the fallback still kills the root independently.
/// </summary>
internal sealed class ProcessOwnership : IDisposable {
    private readonly Process Child;
    private SafeKernelHandle? Job;
    private int Disposed;

    internal ProcessOwnership(Process Child) {
        this.Child = Child ?? throw new ArgumentNullException(nameof(Child));
        SafeKernelHandle Candidate = CreateJobObject(IntPtr.Zero, null);
        if (Candidate.IsInvalid) {
            Candidate.Dispose();
            return;
        }
        ExtendedLimitInformation Limits = new();
        Limits.Basic.LimitFlags = 0x00002000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if (SetInformationJobObject(Candidate, 9, ref Limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>())) {
            Job = Candidate;
        }
        else {
            Candidate.Dispose();
        }
    }

    internal void Attach() {
        if (Job is not null && !AssignProcessToJobObject(Job, Child.Handle)) {
            // Existing job restrictions, particularly on Windows 7, must not break a download.
            Job.Dispose();
            Job = null;
            Trace.WriteLine("Process job assignment was unavailable; using best-effort process-tree cleanup.");
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref Disposed, 1) != 0) return;
        List<Process> Descendants = [];
        try {
            // Also capture any child created in the short Start-to-AssignJob interval.
            Descendants = SnapshotDescendants(Child);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) {
            Trace.WriteLine("Child-process enumeration was unavailable during cleanup.");
        }
        finally {
            try { Job?.Dispose(); }
            finally {
                for (int i = Descendants.Count - 1; i >= 0; i--) {
                    TryKill(Descendants[i]);
                    Descendants[i].Dispose();
                }
                TryKill(Child);
                try { Child.WaitForExit(3_000); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            }
        }
    }

    private static void TryKill(Process Target) {
        try { if (!Target.HasExited) Target.Kill(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) {
            Trace.WriteLine("An owned process exited or could not be terminated during cleanup.");
        }
    }

    private static List<Process> SnapshotDescendants(Process Root) {
        List<Process> Result = [];
        if (Root.HasExited) return Result;
        DateTime RootStarted = Root.StartTime.ToUniversalTime();
        DateTime SnapshotAt = DateTime.UtcNow;
        using SafeKernelHandle Snapshot = CreateToolhelp32Snapshot(2, 0); // TH32CS_SNAPPROCESS
        if (Snapshot.IsInvalid) return Result;
        ProcessEntry Entry = new() { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        Dictionary<uint, List<uint>> Children = [];
        if (Process32First(Snapshot, ref Entry)) {
            int Count = 0;
            do {
                if (++Count > 65_536) break;
                if (!Children.TryGetValue(Entry.ParentProcessId, out List<uint>? Ids)) {
                    Children[Entry.ParentProcessId] = Ids = [];
                }
                Ids.Add(Entry.ProcessId);
            } while (Process32Next(Snapshot, ref Entry));
        }
        HashSet<uint> Seen = [(uint)Root.Id];
        Queue<uint> Pending = new();
        Pending.Enqueue((uint)Root.Id);
        while (Pending.Count > 0 && Seen.Count < 4096) {
            if (!Children.TryGetValue(Pending.Dequeue(), out List<uint>? Ids)) continue;
            foreach (uint Id in Ids) {
                if (Id > int.MaxValue || !Seen.Add(Id)) continue;
                Process? Candidate = null;
                try {
                    Candidate = Process.GetProcessById((int)Id);
                    DateTime Started = Candidate.StartTime.ToUniversalTime();
                    if (Started < RootStarted || Started > SnapshotAt) continue;
                    Result.Add(Candidate);
                    Candidate = null; // Transfer handle ownership to the result.
                    Pending.Enqueue(Id);
                }
                catch (Exception ex) when (ex is ArgumentException or Win32Exception or InvalidOperationException) { }
                finally { Candidate?.Dispose(); }
            }
        }
        return Result;
    }

    private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid {
        private SafeKernelHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation {
        internal long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters {
        internal ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        internal ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation {
        internal BasicLimitInformation Basic;
        internal IoCounters Io;
        internal UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry {
        internal uint Size, Usage, ProcessId;
        internal UIntPtr DefaultHeapId;
        internal uint ModuleId, Threads, ParentProcessId;
        internal int Priority;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Executable;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateJobObjectW", SetLastError = true)]
    private static extern SafeKernelHandle CreateJobObject(IntPtr Attributes, string? Name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeKernelHandle Job, int InformationClass, ref ExtendedLimitInformation Information, uint Length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeKernelHandle Job, IntPtr Process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeKernelHandle CreateToolhelp32Snapshot(uint Flags, uint ProcessId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(SafeKernelHandle Snapshot, ref ProcessEntry Entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(SafeKernelHandle Snapshot, ref ProcessEntry Entry);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr Handle);
}

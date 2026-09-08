#nullable enable
namespace murrty.controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal sealed class ProcessOutputEventArgs : EventArgs {
    public string Data { get; }
    internal ProcessOutputEventArgs(string data) { Data = data; }
}

// Unlike Process.BeginOutputReadLine, a missing newline cannot grow storage without limit.
// Callbacks run on the reader tasks so their exceptions are observed by the owning worker.
internal sealed class BoundedProcessOutput : IDisposable {
    private readonly Process process;
    private readonly int maximumLineCharacters;
    private StreamReader? outputReader;
    private StreamReader? errorReader;
    private Task outputTask = Task.CompletedTask;
    private Task errorTask = Task.CompletedTask;
    private int started;
    private int disposed;
    public event EventHandler<ProcessOutputEventArgs>? OutputDataReceived;
    public event EventHandler<ProcessOutputEventArgs>? ErrorDataReceived;

    internal BoundedProcessOutput(Process process, int maximumLineCharacters = 65536) {
        this.process = process ?? throw new ArgumentNullException(nameof(process));
        if (maximumLineCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumLineCharacters));
        this.maximumLineCharacters = maximumLineCharacters;
    }

    internal void Start() {
        if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(BoundedProcessOutput));
        if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("Output reading has already started.");
        outputReader = process.StandardOutput;
        errorReader = process.StandardError;
        outputTask = Task.Run(() => ReadLines(outputReader, true));
        errorTask = Task.Run(() => ReadLines(errorReader, false));
    }

    private async Task ReadLines(StreamReader reader, bool output) {
        char[] buffer = new char[4096];
        StringBuilder line = new(Math.Min(maximumLineCharacters, buffer.Length));
        bool truncated = false;
        bool afterCarriageReturn = false;
        void Publish() {
            if (truncated) {
                const string marker = "[line truncated]";
                string notice = marker.Substring(0, Math.Min(marker.Length, maximumLineCharacters));
                line.Length = Math.Min(line.Length, maximumLineCharacters - notice.Length);
                line.Append(notice);
            }
            if (Volatile.Read(ref disposed) == 0) {
                (output ? OutputDataReceived : ErrorDataReceived)?.Invoke(this, new ProcessOutputEventArgs(line.ToString()));
            }
            line.Clear();
            truncated = false;
        }
        while (Volatile.Read(ref disposed) == 0) {
            int count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (count == 0) break;
            for (int i = 0; i < count; i++) {
                char c = buffer[i];
                if (afterCarriageReturn && c == '\n') { afterCarriageReturn = false; continue; }
                afterCarriageReturn = c == '\r';
                if (c == '\r' || c == '\n') Publish();
                else if (line.Length < maximumLineCharacters) line.Append(c);
                else truncated = true;
            }
        }
        if (line.Length > 0 || truncated) Publish();
    }

    internal void ThrowIfFaulted() {
        if (outputTask.IsFaulted || outputTask.IsCanceled) outputTask.GetAwaiter().GetResult();
        if (errorTask.IsFaulted || errorTask.IsCanceled) errorTask.GetAwaiter().GetResult();
    }

    // The process may exit while descendants still hold its pipe handles open.
    internal void Drain(int timeoutMilliseconds) {
        if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        if (Volatile.Read(ref started) == 0) throw new InvalidOperationException("Output reading has not started.");
        Stopwatch watch = Stopwatch.StartNew();
        while (true) {
            ThrowIfFaulted();
            if (outputTask.IsCompleted && errorTask.IsCompleted) return;
            if (watch.ElapsedMilliseconds >= timeoutMilliseconds) throw new TimeoutException("Process output did not finish within the drain deadline.");
            Thread.Sleep(10);
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        // The caller terminates owned children before disposing their pipe readers.
        foreach (StreamReader? reader in new[] { outputReader, errorReader }) {
            try { reader?.Dispose(); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
        foreach (Task task in new[] { outputTask, errorTask }) {
            _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}

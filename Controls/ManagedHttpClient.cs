#nullable enable
namespace murrty.controls;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
internal sealed class ManagedHttpClient : IDisposable {
    internal const long DefaultBuffer = 2048L; //81920L;
    internal const int ProgressReportTime = 25;
    internal const int EstimateReportTime = 1000 / ProgressReportTime;
    private static readonly TimeSpan ProgressThrottle = TimeSpan.FromMilliseconds(ProgressReportTime);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private delegate void ProgressFinishedCallback();

    private readonly Timer ProgressReportTimer;
    private readonly ProgressFinishedCallback FinishedCallback;
    private readonly object ProgressSync = new();
    private static SynchronizationContext SyncThread;

    public event EventHandler<DownloadProgressChangedEventArgs>? ProgressChanged;
    public event EventHandler<DownloadFinishedEventArgs>? DownloadComplete;

    private long CurrentProgress;
    private long CurrentTotalSize;
    private long ByteEstimate;
    private long ByteEstimateBuffer;
    private int EstimateTime;

    public bool Disposed { get; private set; }
    public static HttpClient DownloadClientStatic { get; private set; }
    public static bool UseProxy { get; private set; }
    public HttpClient DownloadClient { get; private set; }

    static ManagedHttpClient() {
        SyncThread = SynchronizationContext.Current;
    }
    public ManagedHttpClient() {
        this.ProgressReportTimer = new(
            SyncThread is null ? OnProgressThrottleTicked_NoSyncContext : OnProgressThrottleTicked,
            null, Timeout.Infinite, Timeout.Infinite);

        FinishedCallback = SyncThread is null ? OnProgressFinished_NoSyncContext : OnProgressFinished;
        DownloadClient = DownloadClientStatic;
    }

    public void UpdateInstancedDownloadClient() {
        DownloadClient = DownloadClientStatic;
    }

    public static bool UpdateDownloadClient(string UserAgent) {
        HttpClientHandler handler = new() {
            UseCookies = true,
        };
        DownloadClientStatic = new(handler) {
            Timeout = DefaultTimeout,
        };

        DownloadClientStatic.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        DownloadClientStatic.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        //DownloadClientStatic.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        DownloadClientStatic.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("*"));
        DownloadClientStatic.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        DownloadClientStatic.DefaultRequestHeaders.ConnectionClose = false;
        DownloadClientStatic.DefaultRequestHeaders.Add("Keep-Alive", "600");

        DownloadClientStatic.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return true;
    }
    public static bool UpdateSyncContext(SynchronizationContext Context) {
        if (SyncThread is null || SyncThread != Context) {
            SyncThread = Context;
            return true;
        }
        return false;
    }
    private static async Task<HttpException> GetException(HttpResponseMessage Response, Uri uri, CancellationToken Token) {
        byte[] Content = await ReadResponseBytes(Response, Token, 64 * 1024, Truncate: true).ConfigureAwait(false);
        return new HttpException(Response.StatusCode, Content, uri);
    }

    // Metadata and diagnostics are bounded after decompression. Disposing the response
    // on cancellation also interrupts Framework streams that ignore ReadAsync's token.
    private static async Task<byte[]> ReadResponseBytes(HttpResponseMessage Response, CancellationToken Token,
        int MaximumBytes, bool Truncate = false, Action<int>? ReportProgress = null) {
        if (MaximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumBytes));
        Token.ThrowIfCancellationRequested();
        using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        Deadline.CancelAfter(DefaultTimeout);
        using CancellationTokenRegistration Registration = Deadline.Token.Register(() => Response.Dispose());
        try {
            using Stream Content = await Response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using Stream Decoded = Response.Content.Headers.ContentEncoding.FirstOrDefault()?.ToLowerInvariant() switch {
                "gzip" => new GZipStream(Content, CompressionMode.Decompress),
                "deflate" => new DeflateStream(Content, CompressionMode.Decompress),
                _ => Content,
            };
            using MemoryStream Bytes = new();
            byte[] Buffer = new byte[8192];
            while (true) {
                Deadline.Token.ThrowIfCancellationRequested();
                int Count = await Decoded.ReadAsync(Buffer, 0, Buffer.Length, Deadline.Token).ConfigureAwait(false);
                if (Count == 0) break;
                if (Count > MaximumBytes - Bytes.Length) {
                    if (!Truncate) throw new InvalidDataException("The HTTP response exceeded the permitted decompressed size.");
                    byte[] Notice = Encoding.UTF8.GetBytes("\n[response truncated]");
                    int Limit = Math.Max(0, MaximumBytes - Notice.Length);
                    if (Bytes.Length > Limit) Bytes.SetLength(Limit);
                    else Bytes.Write(Buffer, 0, Math.Min(Count, Limit - (int)Bytes.Length));
                    Bytes.Position = Bytes.Length;
                    Bytes.Write(Notice, 0, Math.Min(Notice.Length, MaximumBytes - (int)Bytes.Length));
                    return Bytes.ToArray();
                }
                Bytes.Write(Buffer, 0, Count);
                ReportProgress?.Invoke(Count);
            }
            Deadline.Token.ThrowIfCancellationRequested();
            return Bytes.ToArray();
        }
        catch (Exception ex) when (Deadline.IsCancellationRequested) {
            Token.ThrowIfCancellationRequested();
            throw new TimeoutException("The HTTP response body did not complete within the permitted time.", ex);
        }
    }

    internal async Task<byte[]> DownloadBytesTaskAsync(Uri uri, CancellationToken Token, int MaximumBytes) {
        using HttpResponseMessage Response = await DownloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, Token).ConfigureAwait(false);
        if (!Response.IsSuccessStatusCode) throw await GetException(Response, uri, Token).ConfigureAwait(false);
        return await ReadResponseBytes(Response, Token, MaximumBytes).ConfigureAwait(false);
    }

    private DownloadProgressChangedEventArgs GetProgressEventArgs() {
        lock (ProgressSync) {
            if (EstimateTime == EstimateReportTime) {
                EstimateTime = 0;
                ByteEstimate = ByteEstimateBuffer;
                ByteEstimateBuffer = 0;
            }
            else {
                EstimateTime++;
            }

            return new(CurrentProgress, CurrentTotalSize, ByteEstimate);
        }
    }
    private DownloadFinishedEventArgs GetFinishedEventArgs() {
        lock (ProgressSync) {
            return new(CurrentProgress);
        }
    }
    private void ResetProgress() {
        lock (ProgressSync) {
            CurrentProgress = 0L;
            CurrentTotalSize = 0L;
            EstimateTime = 0;
            ByteEstimate = 0;
            ByteEstimateBuffer = 0;
        }
    }
    private void OnProgressThrottleTicked(object state) {
        EventHandler<DownloadProgressChangedEventArgs>? Handler = ProgressChanged;
        if (Handler is null) {
            return;
        }

        DownloadProgressChangedEventArgs EventArgs = GetProgressEventArgs();
        SyncThread.Post(_ => Handler.Invoke(this, EventArgs), null);
    }
    private void OnProgressThrottleTicked_NoSyncContext(object state) {
        EventHandler<DownloadProgressChangedEventArgs>? Handler = ProgressChanged;
        if (Handler is null) {
            return;
        }

        DownloadProgressChangedEventArgs EventArgs = GetProgressEventArgs();
        Handler.Invoke(this, EventArgs);
    }

    private void OnProgressFinished() {
        EventHandler<DownloadFinishedEventArgs>? Handler = DownloadComplete;
        if (Handler is null) {
            return;
        }

        DownloadFinishedEventArgs EventArgs = GetFinishedEventArgs();
        SyncThread.Post(_ => Handler.Invoke(this, EventArgs), null);
    }
    private void OnProgressFinished_NoSyncContext() {
        EventHandler<DownloadFinishedEventArgs>? Handler = DownloadComplete;
        if (Handler is null) {
            return;
        }

        DownloadFinishedEventArgs EventArgs = GetFinishedEventArgs();
        Handler.Invoke(this, EventArgs);
    }

    public async Task DownloadFileTaskAsync(Uri uri, string destination, CancellationToken Token) {
        try {
            using HttpResponseMessage Response = await DownloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, Token);

            if (!Response.IsSuccessStatusCode)
                throw await GetException(Response, uri, Token);

            string? ContentEncoding = Response.Content.Headers.ContentEncoding.FirstOrDefault()?.ToLowerInvariant();
            lock (ProgressSync) {
                CurrentTotalSize = ContentEncoding is "gzip" or "deflate"
                    ? 0
                    : Response.Content.Headers.ContentLength ?? 0;
            }

            using FileStream Destination = new(
                path: destination,
                mode: FileMode.Create,
                access: FileAccess.ReadWrite,
                share: FileShare.Read);
            using Stream ContentStream = await Response.Content.ReadAsStreamAsync();

            switch (ContentEncoding) {
                case "gzip": {
                    using GZipStream DecompressedStream = new(ContentStream, CompressionMode.Decompress);
                    await WriteStream(DecompressedStream, Destination, Token);
                } break;
                case "deflate": {
                    using DeflateStream DecompressedStream = new(ContentStream, CompressionMode.Decompress);
                    await WriteStream(DecompressedStream, Destination, Token);
                } break;
                default:
                    await WriteStream(ContentStream, Destination, Token);
                    break;
            }
            await Destination.FlushAsync();
            Destination.Close();
            FinishedCallback();
        }
        finally {
            ProgressReportTimer.Change(Timeout.Infinite, Timeout.Infinite);
            ResetProgress();
        }
    }
    public async Task<string> DownloadStringTaskAsync(Uri uri, CancellationToken Token) {
        try {
            using HttpResponseMessage Response = await DownloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, Token);

            if (!Response.IsSuccessStatusCode)
                throw await GetException(Response, uri, Token);

            lock (ProgressSync) {
                CurrentTotalSize = Response.Content.Headers.ContentEncoding.Any() ? 0 : Response.Content.Headers.ContentLength ?? 0;
            }

            ProgressReportTimer.Change(ProgressThrottle, ProgressThrottle);
            byte[] Bytes = await ReadResponseBytes(Response, Token, 32 * 1024 * 1024, ReportProgress: Count => {
                lock (ProgressSync) {
                    CurrentProgress += Count;
                    ByteEstimateBuffer += Count;
                }
            });
            FinishedCallback();

            return (Response.Content.Headers.ContentType?.CharSet ?? "utf-8").ToLowerInvariant() switch {
                "ascii" => Encoding.ASCII.GetString(Bytes),
                "utf-7" => Encoding.UTF7.GetString(Bytes),
                "utf-32" => Encoding.UTF32.GetString(Bytes),
                "utf-16" or "unicode" => Encoding.Unicode.GetString(Bytes),
                "utf-16-be" or "utf-16be" or "unicode-be" or "unicodebe" => Encoding.BigEndianUnicode.GetString(Bytes),
                _ => Encoding.UTF8.GetString(Bytes),
            };
        }
        finally {
            ProgressReportTimer.Change(Timeout.Infinite, Timeout.Infinite);
            ResetProgress();
        }
    }

    private async Task WriteStream(Stream Source, Stream Writer, CancellationToken Token) {
        byte[] buffer = new byte[DefaultBuffer];
        int bytesRead;
        lock (ProgressSync) {
            EstimateTime = 35;
        }

        using CancellationTokenSource ReadTimeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using CancellationTokenRegistration ReadCancellation = ReadTimeout.Token.Register(() => Source.Dispose());
        ProgressReportTimer.Change(ProgressThrottle, ProgressThrottle);
        while (true) {
            ReadTimeout.CancelAfter(DefaultTimeout);
            try {
                bytesRead = await Source.ReadAsync(buffer, 0, buffer.Length, ReadTimeout.Token).ConfigureAwait(false);
                ReadTimeout.Token.ThrowIfCancellationRequested();
            }
            catch (Exception ex) when (ReadTimeout.IsCancellationRequested) {
                Token.ThrowIfCancellationRequested();
                throw new TimeoutException("The download response stopped providing data.", ex);
            }
            ReadTimeout.CancelAfter(Timeout.Infinite);
            if (bytesRead <= 0) {
                break;
            }

            await Writer.WriteAsync(buffer, 0, bytesRead, Token).ConfigureAwait(false);
            lock (ProgressSync) {
                CurrentProgress += bytesRead;
                ByteEstimateBuffer += bytesRead;
            }
        }
        ProgressReportTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose() {
        Dispose(true);
    }
    private void Dispose(bool disposing) {
        if (disposing && !Disposed) {
            ProgressReportTimer.Change(Timeout.Infinite, Timeout.Infinite);
            ProgressReportTimer.Dispose();
            Disposed = true;
        }
    }
}
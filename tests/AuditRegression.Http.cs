using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    // Only loopback traffic is permitted. Every fixture owns its socket and thread.
    private sealed class LoopbackResponse : IDisposable {
        private readonly TcpListener listener;
        private readonly Thread worker;
        private readonly ManualResetEvent release = new ManualResetEvent(false);
        internal readonly ManualResetEvent Sent = new ManualResetEvent(false);
        private TcpClient client;
        internal Uri Uri;
        internal Exception Error;
        internal LoopbackResponse(int status, byte[] body, string encoding, bool stall, string path) {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Uri = new Uri("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + path);
            worker = new Thread(() => {
                try {
                    client = listener.AcceptTcpClient();
                    using (NetworkStream stream = client.GetStream()) {
                        stream.ReadTimeout = 5000;
                        string headers = "";
                        while (!headers.EndsWith("\r\n\r\n", StringComparison.Ordinal) && headers.Length < 16384) {
                            int b = stream.ReadByte();
                            if (b < 0) return;
                            headers += (char)b;
                        }
                        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Fixture\r\nContent-Length: " + (body.Length + (stall ? 4096 : 0)) +
                            "\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n" +
                            (encoding == null ? "" : "Content-Encoding: " + encoding + "\r\n") + "\r\n");
                        stream.Write(response, 0, response.Length);
                        if (body.Length != 0) stream.Write(body, 0, body.Length);
                        stream.Flush();
                        Sent.Set();
                        if (stall) release.WaitOne(15000);
                    }
                }
                catch (Exception e) { Error = e; Sent.Set(); }
            });
            worker.IsBackground = true;
            worker.Start();
        }
        public void Dispose() {
            release.Set();
            if (client != null) client.Close();
            listener.Stop();
            Require(worker.Join(5000), "Loopback fixture did not stop");
            release.Dispose();
            Sent.Dispose();
        }
    }
    private static object HttpClient() {
        Call(T("murrty.controls.ManagedHttpClient"), null, "UpdateDownloadClient", "AuditRegression/1.0");
        return T("murrty.controls.ManagedHttpClient").GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
    }
    private static void ObserveHttp(Task task) {
        if (task == null) return;
        PumpUntil(() => task.IsCompleted, 5000, "HTTP fixture left a pending task");
        if (task.IsFaulted) { Exception ignored = task.Exception; }
    }
    static partial void RunThumbnailTests();
    static partial void RunHttpTests() {
        Test("N003.HttpErrorBodyHonorsCancellation", () => {
            object client = HttpClient();
            Task task = null;
            using (CancellationTokenSource cancel = new CancellationTokenSource()) {
                LoopbackResponse server = new LoopbackResponse(500, new byte[] { 65 }, null, true, "/error");
                try {
                    task = (Task)Call(client.GetType(), client, "DownloadStringTaskAsync", server.Uri, cancel.Token);
                    PumpUntil(() => server.Sent.WaitOne(0), 5000, "No error headers arrived");
                    Thread.Sleep(100);
                    cancel.Cancel();
                    PumpUntil(() => task.IsCompleted, 2500, "Cancellation did not interrupt the HTTP error body");
                    Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                }
                finally { server.Dispose(); ObserveHttp(task); ((IDisposable)client).Dispose(); }
            }
        });
        Test("N003.HttpErrorDiagnosticsAreBounded", () => {
            object client = HttpClient();
            Task task = null;
            using (LoopbackResponse server = new LoopbackResponse(500, new byte[100000], null, false, "/large-error")) {
                try {
                    task = (Task)Call(client.GetType(), client, "DownloadStringTaskAsync", server.Uri, CancellationToken.None);
                    PumpUntil(() => task.IsCompleted, 5000, "Error response did not finish");
                    Exception error = null;
                    try { task.GetAwaiter().GetResult(); } catch (Exception e) { error = e; }
                    Require(error != null && error.GetType().FullName == "murrty.controls.HttpException", "HTTP status was lost");
                    byte[] bytes = (byte[])Get(error, "ResponseContent");
                    Require(bytes.Length <= 65536, "Error diagnostics exceeded 64 KiB");
                }
                finally { ((IDisposable)client).Dispose(); }
            }
        });
        Test("N003.HttpGzipStringLimitAppliesAfterDecompression", () => {
            byte[] compressed;
            using (MemoryStream output = new MemoryStream()) {
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, true)) {
                    byte[] chunk = new byte[8192];
                    for (int i = 0; i < 33 * 128; i++) gzip.Write(chunk, 0, chunk.Length);
                }
                compressed = output.ToArray();
            }
            object client = HttpClient();
            using (LoopbackResponse server = new LoopbackResponse(200, compressed, "gzip", false, "/large-json")) {
                try {
                    Task task = (Task)Call(client.GetType(), client, "DownloadStringTaskAsync", server.Uri, CancellationToken.None);
                    PumpUntil(() => task.IsCompleted, 10000, "Compressed response did not finish");
                    Throws<InvalidDataException>(() => task.GetAwaiter().GetResult());
                }
                finally { ((IDisposable)client).Dispose(); }
            }
        });
        Test("N003.HttpSmallGzipContentPreserved", () => {
            const string text = "{\"message\":\"two  spaces and \u03bb\"}";
            byte[] compressed;
            using (MemoryStream output = new MemoryStream()) {
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, true)) {
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    gzip.Write(bytes, 0, bytes.Length);
                }
                compressed = output.ToArray();
            }
            object client = HttpClient();
            using (LoopbackResponse server = new LoopbackResponse(200, compressed, "gzip", false, "/small-json")) {
                try {
                    Task<string> task = (Task<string>)Call(client.GetType(), client, "DownloadStringTaskAsync", server.Uri, CancellationToken.None);
                    PumpUntil(() => task.IsCompleted, 5000, "Small response did not finish");
                    Equal(text, task.GetAwaiter().GetResult());
                }
                finally { ((IDisposable)client).Dispose(); }
            }
        });
        RunThumbnailTests();
    }
}

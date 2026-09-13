using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object ThumbnailData(Uri uri) {
        object data = New("youtube_dl_gui.YoutubeDlData");
        Set(data.GetType(), data, "ThumbnailLink", uri.AbsoluteUri);
        return data;
    }
    private static MethodInfo CancelableThumbnail() {
        MethodInfo method = T("youtube_dl_gui.YoutubeDlData").GetMethod("GetThumbnail", All, null, new[] { typeof(CancellationToken) }, null);
        Require(method != null, "Thumbnail retrieval has no cancellation boundary");
        return method;
    }
    private static object InvokeThumbnail(MethodInfo method, object data, CancellationToken token) {
        try { return method.Invoke(data, new object[] { token }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    private sealed class ThumbnailNormalizerScope : IDisposable {
        private readonly object PreviousFfmpeg;
        private readonly string PreviousMode;
        private readonly string PreviousPid;
        private readonly string PreviousInput;
        private readonly string Pid;
        private readonly string InputRecord;

        internal ThumbnailNormalizerScope() {
            PreviousFfmpeg = T("youtube_dl_gui.Verification").GetProperty("FFmpegPath", All).GetValue(null, null);
            PreviousMode = Environment.GetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE");
            PreviousPid = Environment.GetEnvironmentVariable("YTDL_AUDIT_PID_FILE");
            PreviousInput = Environment.GetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT");
            Pid = Path.Combine(Environment.CurrentDirectory, "thumbnail-normalize-" + Guid.NewGuid().ToString("N") + ".pid");
            InputRecord = Pid + ".input";

            Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", "thumbnail-normalize");
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", Pid);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT", InputRecord);
            Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe"));
        }

        public void Dispose() {
            Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", PreviousFfmpeg);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", PreviousMode);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", PreviousPid);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT", PreviousInput);
            try { File.Delete(Pid); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { File.Delete(InputRecord); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static IDisposable UseThumbnailNormalizer() { return new ThumbnailNormalizerScope(); }

    static partial void RunThumbnailTests() {
        Test("N004.ThumbnailDownloadHonorsCancellation", () => {
            MethodInfo method = CancelableThumbnail();
            object http = HttpClient();
            ((IDisposable)http).Dispose();
            using (CancellationTokenSource cancel = new CancellationTokenSource()) {
                LoopbackResponse server = new LoopbackResponse(200, new byte[] { 1 }, null, true, "/preview.jpg");
                Task task = null;
                try {
                    object data = ThumbnailData(server.Uri);
                    task = Task.Run(() => InvokeThumbnail(method, data, cancel.Token));
                    PumpUntil(() => server.Sent.WaitOne(0), 5000, "Thumbnail response did not start");
                    cancel.Cancel();
                    PumpUntil(() => task.IsCompleted, 3000, "Thumbnail cancellation did not finish");
                    Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                }
                finally { server.Dispose(); ObserveHttp(task); }
            }
        });
        Test("N004.ThumbnailCloneSurvivesResponseDisposal", () => {
            byte[] png;
            using (Bitmap bitmap = new Bitmap(2, 3))
            using (MemoryStream memory = new MemoryStream()) {
                bitmap.Save(memory, ImageFormat.Png);
                png = memory.ToArray();
            }
            object http = HttpClient();
            ((IDisposable)http).Dispose();
            using (IDisposable decoder = UseThumbnailNormalizer())
            using (LoopbackResponse server = new LoopbackResponse(200, png, null, false, "/preview.png")) {
                using (Image image = (Image)Call(T("youtube_dl_gui.YoutubeDlData"), ThumbnailData(server.Uri), "GetThumbnail")) {
                    Equal(2, image.Width);
                    Equal(3, image.Height);
                    using (MemoryStream output = new MemoryStream()) image.Save(output, ImageFormat.Png);
                }
            }
        });
        Test("N004.ThumbnailConversionCancellationCleansProcessAndFiles", () => {
            MethodInfo method = CancelableThumbnail();
            object http = HttpClient();
            ((IDisposable)http).Dispose();
            string pid = Path.Combine(Environment.CurrentDirectory, "thumbnail-" + Guid.NewGuid().ToString("N") + ".pid");
            string sourceRecord = pid + ".input";
            object previousFfmpeg = T("youtube_dl_gui.Verification").GetProperty("FFmpegPath", All).GetValue(null, null);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", "thumbnail-hang");
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", pid);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT", sourceRecord);
            Task task = null;
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            using (LoopbackResponse server = new LoopbackResponse(200, new byte[] { 1, 2, 3 }, null, false, "/preview.WEBP?size=small")) {
                try {
                    Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe"));
                    task = Task.Run(() => InvokeThumbnail(method, ThumbnailData(server.Uri), cancel.Token));
                    PumpUntil(() => File.Exists(pid), 8000, "Thumbnail converter fixture did not start");
                    cancel.Cancel();
                    PumpUntil(() => task.IsCompleted, 8000, "Thumbnail converter did not cancel");
                    Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                    AssertGone(pid);
                    string input = File.ReadAllText(sourceRecord);
                    Require(!File.Exists(input), "Thumbnail input was not removed");
                    Require(!Directory.Exists(Path.GetDirectoryName(input)), "Thumbnail temporary directory was not removed");
                }
                finally {
                    cancel.Cancel();
                    KillFixture(pid);
                    ObserveHttp(task);
                    Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", null);
                    Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", null);
                    Environment.SetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT", null);
                    Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", previousFfmpeg);
                }
            }
        });
    }
}
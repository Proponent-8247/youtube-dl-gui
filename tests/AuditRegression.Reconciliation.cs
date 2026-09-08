using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private sealed class DeferredRelease : HttpMessageHandler {
        internal readonly TaskCompletionSource<HttpResponseMessage> Response = new TaskCompletionSource<HttpResponseMessage>();
        internal Uri Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            Requested = request.RequestUri;
            token.Register(() => Response.TrySetCanceled());
            return Response.Task;
        }
    }
    private static void UpdateChannelSnapshot(bool beta) {
        Type updater = T("youtube_dl_gui.Updater"), general = T("youtube_dl_gui.General"), program = T("youtube_dl_gui.Program"), http = T("murrty.controls.ManagedHttpClient");
        object oldBeta = general.GetProperty("DownloadBetaVersions", All).GetValue(null, null);
        object oldHttp = program.GetProperty("HttpClient", All).GetValue(null, null);
        object oldStatic = http.GetProperty("DownloadClientStatic", All).GetValue(null, null);
        string[] caches = { "LastChecked", "LastCheckedLatestRelease", "LastCheckedAllRelease" };
        object[] saved = caches.Select(name => updater.GetProperty(name, All).GetValue(null, null)).ToArray();
        object client = null;
        using (DeferredRelease deferred = new DeferredRelease())
        using (HttpClient transport = new HttpClient(deferred)) {
            try {
                Set(http, null, "DownloadClientStatic", transport);
                client = New("murrty.controls.ManagedHttpClient");
                Set(program, null, "HttpClient", client);
                Set(general, null, "DownloadBetaVersions", beta);
                foreach (string name in caches) Set(updater, null, name, null);
                Task task = (Task)Call(updater, null, "CheckForUpdate", true);
                Require(deferred.Requested != null, "The release request did not start");
                Equal(beta, !deferred.Requested.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal));
                Set(general, null, "DownloadBetaVersions", !beta);
                string release = "{\"name\":\"audit\",\"tag_name\":\"99.0.0\",\"body\":\"exe sha256: " + new string('a', 64) + "\",\"prerelease\":false,\"assets\":[]}";
                deferred.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(beta ? "[" + release + "]" : release) });
                PumpUntil(() => task.IsCompleted, 5000, "Release check did not complete after channel change");
                task.GetAwaiter().GetResult();
                object selected = updater.GetProperty("LastChecked", All).GetValue(null, null);
                object cached = updater.GetProperty(beta ? "LastCheckedAllRelease" : "LastCheckedLatestRelease", All).GetValue(null, null);
                Require(selected != null && ReferenceEquals(selected, cached), "The release response was assigned to the wrong channel");
                Equal("99.0.0", Get(selected, "VersionTag"));
                Equal(null, updater.GetProperty(beta ? "LastCheckedLatestRelease" : "LastCheckedAllRelease", All).GetValue(null, null));
            }
            finally {
                Set(general, null, "DownloadBetaVersions", oldBeta);
                Set(program, null, "HttpClient", oldHttp);
                Set(http, null, "DownloadClientStatic", oldStatic);
                for (int i = 0; i < caches.Length; i++) Set(updater, null, caches[i], saved[i]);
                if (client != null) ((IDisposable)client).Dispose();
            }
        }
    }
    private static void StaleProviderMetadataIsRejected() {
        Type updater = T("youtube_dl_gui.Updater"), downloads = T("youtube_dl_gui.Downloads"), http = T("murrty.controls.ManagedHttpClient");
        object oldType = downloads.GetProperty("YtdlType", All).GetValue(null, null);
        object oldRelease = updater.GetProperty("LatestYoutubeDl", All).GetValue(null, null);
        object oldCacheType = updater.GetField("LatestYoutubeDlType", All).GetValue(null);
        object oldHttp = http.GetProperty("DownloadClientStatic", All).GetValue(null, null);
        using (DeferredRelease deferred = new DeferredRelease())
        using (HttpClient transport = new HttpClient(deferred))
        using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 50 }) {
            int dialogs = 0;
            timer.Tick += (sender, args) => {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) {
                    if (form.GetType().FullName != "youtube_dl_gui.frmGenericDownloadProgress") continue;
                    dialogs++;
                    form.Close();
                }
            };
            try {
                object release = New("murrty.updater.GithubData");
                Set(release.GetType(), release, "VersionTag", "99.0.0");
                Set(release.GetType(), release, "IsNewerVersion", true);
                Set(updater, null, "LatestYoutubeDl", release);
                updater.GetField("LatestYoutubeDlType", All).SetValue(null, 0);
                Set(downloads, null, "YtdlType", 1);
                Set(http, null, "DownloadClientStatic", transport);
                timer.Start();
                Equal(false, Call(updater, null, "UpdateYoutubeDl", false, null));
                Require(dialogs == 0 && deferred.Requested == null, "Metadata for another provider started an update download");
            }
            finally {
                timer.Stop();
                Set(downloads, null, "YtdlType", oldType);
                Set(updater, null, "LatestYoutubeDl", oldRelease);
                updater.GetField("LatestYoutubeDlType", All).SetValue(null, oldCacheType);
                Set(http, null, "DownloadClientStatic", oldHttp);
            }
        }
    }
    private static void ProcessingFormDisposal() {
        Type program = T("youtube_dl_gui.Program");
        object oldHandler = program.GetProperty("QueueHandler", All).GetValue(null, null);
        IList running = (IList)program.GetProperty("RunningActions", All).GetValue(null, null);
        using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
        using (Form form = (Form)New("youtube_dl_gui.LocalizedProcessingForm")) {
            Set(program, null, "QueueHandler", handler);
            try {
                form.Show();
                Require(running.Contains(form), "Shown processing form was not registered");
                form.Dispose();
                Require(!running.Contains(form), "Disposed processing form is still registered as active work");
            }
            finally {
                // Clean the pre-fix failure without concealing it from the assertion.
                Call(program, null, "RemoveProcessingForm", form);
                Set(program, null, "QueueHandler", oldHandler);
            }
        }
    }
    private static void InitialNoAudioBinding(bool authenticate) {
        object auth = authenticate ? New("youtube_dl_gui.AuthenticationDetails") : null;
        object kind = Enum.Parse(T("youtube_dl_gui.ArgumentType"), authenticate ? "DownloadAuthenticateVideoNoSound" : "DownloadVideoNoSound");
        using (Form form = (Form)New("youtube_dl_gui.frmExtendedDownloader", "https://example.invalid/fixture", "", false, auth, kind)) {
            object media = Get(form, "MediaDetails");
            Set(media.GetType(), media, "InfoRetrieved", true);
            Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(media.GetType(), media, "VideoDownloadAudio", false);
            Require(((CheckBox)Field(form, "chkVideoDownloadAudio")).Checked, "Fixture must start with the conflicting designer default");
            Call(form.GetType(), form, "SelectedMediaChanged", media);
            Equal(false, Get(media, "VideoDownloadAudio"));
            Equal(false, ((CheckBox)Field(form, "chkVideoDownloadAudio")).Checked);
            Call(form.GetType(), form, "SaveMediaOptions");
            Equal(false, Get(media, "VideoDownloadAudio"));
            Require(ReferenceEquals(auth, Get(media, "Authentication")), "Binding replaced authentication");
        }
    }
    private static void QueueResolverPreservesDownload() {
        string pid = Path.Combine(Environment.CurrentDirectory, "resolver-" + Guid.NewGuid().ToString("N") + ".pid");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", "complete");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", pid);
        try {
            LiveExtendedWorker(form => {
                IntPtr handle = form.Handle;
                object worker = Get(form, "ProcessingThread");
                Set(form.GetType(), form, "Status", DownloadState("MergingFiles"));
                Call(form.GetType(), form, "QueueNewItem", "https://example.invalid/queued", false, false, false, null);
                PumpUntil(() => !(bool)Field(form, "QueueResolverRunning"), 10000, "Metadata resolver did not finish");
                Thread resolver = (Thread)Get(form, "QueueResolverThread");
                PumpUntil(() => resolver == null || !resolver.IsAlive, 5000, "Resolver thread did not exit");
                Require(ReferenceEquals(worker, Get(form, "ProcessingThread")), "Queue resolution replaced active transfer ownership");
                Equal(DownloadState("MergingFiles"), Get(form, "Status"));
                Require(((Thread)worker).IsAlive, "Fixture download worker was terminated");
            });
        }
        finally {
            KillFixture(pid);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", null);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", null);
        }
    }
    private delegate bool EnumAuditWindow(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumAuditWindow callback, IntPtr data);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int capacity);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendAuditButton(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);
    private static void ChecksumRetryReleasesFile() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        Assembly assembly = Assembly.LoadFrom(Path.Combine(root, "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe"));
        Type program = assembly.GetType("youtube_dl_gui_updater.Program", true), http = assembly.GetType("murrty.controls.ManagedHttpClient", true);
        Call(assembly.GetType("youtube_dl_gui_updater.Language", true), null, "LoadInternalEnglish");
        Call(http, null, "UpdateDownloadClient", "audit-regression/1.0");
        object client = Activator.CreateInstance(http, true);
        Set(program, null, "DownloadClient", client);
        byte[] replacement = Encoding.UTF8.GetBytes(new string('r', 2048));
        string file = Path.Combine(Environment.CurrentDirectory, "update-retry.part");
        File.WriteAllText(file, "bad checksum");
        string hash;
        using (SHA256 sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(replacement)).Replace("-", "").ToLowerInvariant();
        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        using (LoopbackResponse server = new LoopbackResponse(200, replacement, null, false, "/retry.exe"))
        using (Form form = (Form)Activator.CreateInstance(assembly.GetType("youtube_dl_gui_updater.frmUpdater", true), true))
        using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 50 }) {
            Set(program, null, "CancelToken", cancellation);
            IntPtr handle = form.Handle;
            object data = Activator.CreateInstance(assembly.GetType("youtube_dl_gui_updater.UpdateData", false) ?? assembly.GetTypes().Single(t => t.Name == "UpdateData"));
            Set(data.GetType(), data, "UpdateHash", hash);
            Field(form, "UpdateData", data);
            int retries = 0;
            Exception inspection = null;
            uint thread = GetCurrentThreadId();
            timer.Tick += (s, e) => EnumThreadWindows(thread, (window, state) => {
                StringBuilder name = new StringBuilder(256);
                GetClassName(window, name, name.Capacity);
                if (name.ToString() != "#32770") return true;
                IntPtr button = GetDlgItem(window, retries == 0 ? (int)DialogResult.Retry : (int)DialogResult.Abort);
                if (button == IntPtr.Zero) return true;
                try { using (FileStream probe = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
                catch (Exception ex) { inspection = ex; }
                retries++;
                SendAuditButton(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                return false;
            }, IntPtr.Zero);
            try {
                timer.Start();
                Task task = (Task)Call(form.GetType(), form, "VerifyHash", server.Uri.ToString(), file);
                PumpUntil(() => task.IsCompleted, 15000, "Checksum retry did not complete");
                task.GetAwaiter().GetResult();
                if (inspection != null) throw inspection;
                Equal(1, retries);
                Require(File.ReadAllBytes(file).SequenceEqual(replacement), "Replacement content was not verified and retained");
            }
            finally { timer.Stop(); cancellation.Cancel(); ((IDisposable)client).Dispose(); }
        }
    }
    static partial void RunReconciliationTests() {
        Test("N010.DisposedProcessingFormIsUnregistered", ProcessingFormDisposal);
        Test("N012.StaleProviderMetadataCannotStartDownload", StaleProviderMetadataIsRejected);
        Test("N011.StableReleaseRequestKeepsItsChannel", () => UpdateChannelSnapshot(false));
        Test("N011.BetaReleaseRequestKeepsItsChannel", () => UpdateChannelSnapshot(true));
        Test("C1.ResolverCompletionPreservesActiveDownload", QueueResolverPreservesDownload);
        Test("C5.FirstBindingPreservesNoAudio", () => InitialNoAudioBinding(false));
        Test("C5.FirstAuthenticatedBindingPreservesNoAudio", () => InitialNoAudioBinding(true));
        Test("C4.ChecksumRetryReleasesAndReverifiesFile", ChecksumRetryReleasesFile);
        Test("Authentication.PasswordsAndCloneAreIndependent", () => {
            object auth = New("youtube_dl_gui.AuthenticationDetails");
            string password = "audit two  spaces \u03bb";
            Call(auth.GetType(), auth, "SetPassword", password);
            Call(auth.GetType(), auth, "SetMediaPassword", "media-" + password);
            object clone = Call(auth.GetType(), auth, "Clone");
            Equal(password, Call(clone.GetType(), clone, "GetPassword"));
            Equal("media-" + password, Call(clone.GetType(), clone, "GetMediaPassword"));
            Require(!ReferenceEquals(Get(auth, "Password"), Get(clone, "Password")), "Password buffers are shared");
            Require(!ReferenceEquals(Get(auth, "MediaPassword"), Get(clone, "MediaPassword")), "Media password buffers are shared");
            Call(clone.GetType(), clone, "SetPassword", "changed");
            Equal(password, Call(auth.GetType(), auth, "GetPassword"));
        });
        Test("C2.ExtendedCustomSourceIsOperand", () => {
            object media = New("youtube_dl_gui.ExtendedMediaDetails", "--config-locations=untrusted");
            Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Require((bool)Call(media.GetType(), media, "GenerateArguments"), "Extended generation failed");
            Require(((string)Get(media, "Arguments")).EndsWith("-- --config-locations=untrusted", StringComparison.Ordinal), "Extended URL became an option");
        });
        Test("C2.SearchAndCollectionOperandsRemainSupported", () => {
            foreach (string source in new[] { "ytsearch2:two  words", "https://www.youtube.com/example/videos", "https://example.invalid/playlist?list=a" }) {
                Require(Generate(Download(source)).EndsWith("-- " + Escape(source), StringComparison.Ordinal), "Supported source was lost or rewritten");
            }
        });
    }
}

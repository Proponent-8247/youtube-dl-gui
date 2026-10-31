using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private sealed class SetupTransport : HttpMessageHandler {
        internal int Requests;
        internal volatile bool Waiting;
        private readonly byte[] executable;
        private readonly byte[] archive;
        private readonly string archiveHash;
        internal SetupTransport() {
            executable = File.ReadAllBytes(Self);
            using (MemoryStream memory = new MemoryStream()) {
                using (ZipArchive zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
                    foreach (string file in new[] { "bin/ffmpeg.exe", "bin/ffprobe.exe" }) {
                        using (Stream entry = zip.CreateEntry(file).Open()) entry.Write(executable, 0, executable.Length);
                    }
                }
                archive = memory.ToArray();
            }
            using (SHA256 sha = SHA256.Create()) {
                archiveHash = BitConverter.ToString(sha.ComputeHash(archive)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            Interlocked.Increment(ref Requests);
            Waiting = true;
            try { await Task.Delay(400, token).ConfigureAwait(false); }
            finally { Waiting = false; }
            if (request.RequestUri.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal)) {
                string release = "{\"tag_name\":\"2099.01.01\",\"assets\":[{\"name\":\"yt-dlp.exe\",\"browser_download_url\":\"https://audit.invalid/yt-dlp.exe\",\"digest\":\"sha256:" + FileSha256(Self) + "\",\"size\":" + executable.Length + "}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(release) };
            }
            if (request.RequestUri.AbsolutePath.EndsWith(".zip.sha256", StringComparison.OrdinalIgnoreCase)) {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(archiveHash) };
            }
            if (request.RequestUri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) };
            }
            if (request.RequestUri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(executable) };
            }
            throw new InvalidOperationException("Unexpected startup fixture request: " + request.RequestUri);
        }
    }
    private static void RunStartupProbeTests() {
        Test("C3.FirstRunPumpsUiDuringDelayedToolInstallation", () => {
            Type program = T("youtube_dl_gui.Program"), general = T("youtube_dl_gui.General"), downloads = T("youtube_dl_gui.Downloads");
            Type init = T("youtube_dl_gui.Initialization"), verification = T("youtube_dl_gui.Verification"), updater = T("youtube_dl_gui.Updater"), http = T("murrty.controls.ManagedHttpClient");
            List<Action> restore = new List<Action>();
            Action<Type, string, object> change = (type, name, value) => {
                object old = type.GetProperty(name, All).GetValue(null, null);
                restore.Add(() => Set(type, null, name, old));
                Set(type, null, name, value);
            };
            string oldDirectory = Environment.CurrentDirectory;
            string scratch = Path.Combine(oldDirectory, "first-run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            int oldProviderType = (int)updater.GetField("LatestYoutubeDlType", All).GetValue(null);
            object client = null;
            using (SetupTransport transport = new SetupTransport())
            using (HttpClient network = new HttpClient(transport))
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 40 }) {
                int prompts = 0, delayedTicks = 0;
                uint thread = GetCurrentThreadId();
                timer.Tick += (sender, args) => {
                    if (transport.Waiting) delayedTicks++;
                    foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) {
                        if (form.GetType().FullName == "youtube_dl_gui.frmLanguage") {
                            ((ComboBox)Field(form, "cbLanguages")).SelectedIndex = 0;
                            ((Button)Field(form, "btnLanguageSave")).PerformClick();
                            return;
                        }
                    }
                    EnumThreadWindows(thread, (window, state) => {
                        System.Text.StringBuilder name = new System.Text.StringBuilder(256);
                        GetClassName(window, name, name.Capacity);
                        if (name.ToString() != "#32770") return true;
                        // Welcome: yes; custom download directory: no; install both missing tools: yes.
                        int answer = prompts == 1 ? (int)DialogResult.No : (int)DialogResult.Yes;
                        IntPtr button = GetDlgItem(window, answer);
                        if (button == IntPtr.Zero) return true;
                        prompts++;
                        SendAuditButton(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                        return false;
                    }, IntPtr.Zero);
                };
                try {
                    Environment.CurrentDirectory = scratch;
                    change(general, "UseStaticYtdl", true);
                    change(general, "ytdlPath", Path.Combine(scratch, "yt-dlp.exe"));
                    change(general, "UseStaticFFmpeg", true);
                    change(general, "ffmpegPath", Path.Combine(scratch, "ffmpeg.exe"));
                    change(downloads, "YtdlType", 0);
                    change(downloads, "downloadPath", scratch);
                    change(init, "firstTime", true);
                    change(init, "LanguageFile", null);
                    change(verification, "YoutubeDlPath", null);
                    change(verification, "YoutubeDlVersion", null);
                    change(verification, "FFmpegPath", null);
                    change(verification, "FFprobePath", null);
                    change(updater, "LatestYoutubeDl", null);
                    updater.GetField("LatestYoutubeDlType", All).SetValue(null, -1);
                    change(http, "DownloadClientStatic", network);
                    client = Activator.CreateInstance(http, true);
                    change(program, "HttpClient", client);
                    timer.Start();
                    Equal(true, Call(program, null, "RunFirstTimeSetup"));
                    Equal(4, prompts);
                    Equal(4, transport.Requests);
                    Require(delayedTicks >= 6, "UI timers did not run during delayed dependency requests");
                    foreach (string file in new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe" }) {
                        Require(File.Exists(Path.Combine(scratch, file)), "First-run installation did not produce " + file);
                        Equal(FileSha256(Self), FileSha256(Path.Combine(scratch, file)));
                    }
                    Require(!File.Exists(Path.Combine(scratch, "ffmpeg.zip")), "FFmpeg archive was not cleaned up");
                    Require(Directory.GetFiles(scratch, "*.update").Length == 0, "Partial extraction files remained");
                }
                finally {
                    timer.Stop();
                    for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
                    updater.GetField("LatestYoutubeDlType", All).SetValue(null, oldProviderType);
                    if (client != null) ((IDisposable)client).Dispose();
                    Environment.CurrentDirectory = oldDirectory;
                }
            }
        });
    }
}
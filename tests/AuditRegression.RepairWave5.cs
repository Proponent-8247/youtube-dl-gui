using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object TimedExtendedVideo(string source, int startSeconds, int endSeconds) {
        object media = New("youtube_dl_gui.ExtendedMediaDetails", source);
        ListViewItem video = new ListViewItem("video") { Tag = FormatWithId("v-audit") };
        Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
        Set(media.GetType(), media, "SelectedVideoItem", video);
        Set(media.GetType(), media, "VideoDownloadAudio", false);
        if (startSeconds >= 0) Set(media.GetType(), media, "StartTime", Time(startSeconds, 0));
        if (endSeconds >= 0) Set(media.GetType(), media, "EndTime", Time(endSeconds, 0));
        return media;
    }

    private static byte[] TinyPng(int width, int height) {
        using (Bitmap bitmap = new Bitmap(width, height))
        using (MemoryStream output = new MemoryStream()) {
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
    }

    private static bool ImageIsDisposed(Image image) {
        try {
            using (MemoryStream output = new MemoryStream()) image.Save(output, ImageFormat.Png);
            return false;
        }
        catch (ArgumentException) { return true; }
        catch (ExternalException) { return true; }
    }

    private static void ForcedThumbnailReplacementDisposesOldImage() {
        object client = HttpClient();
        ((IDisposable)client).Dispose();
        using (LoopbackResponse firstServer = new LoopbackResponse(200, TinyPng(2, 3), null, false, "/first.png"))
        using (LoopbackResponse secondServer = new LoopbackResponse(200, TinyPng(4, 5), null, false, "/second.png")) {
            object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/media");
            Set(media.GetType(), media, "MediaData", ThumbnailData(firstServer.Uri));
            Image first = (Image)Call(media.GetType(), media, "DownloadThumbnail", false);
            try {
                Equal(2, first.Width);
                Equal(3, first.Height);
                Set(media.GetType(), media, "MediaData", ThumbnailData(secondServer.Uri));
                Image second = (Image)Call(media.GetType(), media, "DownloadThumbnail", true);
                try {
                    Equal(4, second.Width);
                    Equal(5, second.Height);
                    Require(!ReferenceEquals(first, second), "Forced refresh reused the cached image");
                    Require(ImageIsDisposed(first), "Forced refresh dropped the previous cached image without disposing it");
                }
                finally { if (!ReferenceEquals(first, second)) second.Dispose(); }
            }
            finally { if (!ImageIsDisposed(first)) first.Dispose(); }
        }
    }

    private static int StaticInt(Type type, string name) {
        PropertyInfo property = type.GetProperty(name, All);
        Require(property != null, "Missing static property " + type.FullName + "." + name);
        return (int)property.GetValue(null, null);
    }

    private static void DownloadSectionsRequireYtDlp() {
        Type downloads = T("youtube_dl_gui.Downloads");
        int original = StaticInt(downloads, "YtdlType");
        try {
            Set(downloads, null, "YtdlType", 2); // YoutubeDl
            object youtubeDl = TimedExtendedVideo("https://example.invalid/youtube-dl", 5, 10);
            Require(!(bool)Call(youtubeDl.GetType(), youtubeDl, "GenerateArguments"),
                "youtube-dl accepted a time range that requires the yt-dlp-only --download-sections option");

            Set(downloads, null, "YtdlType", 0); // YtDlp
            object ytDlp = TimedExtendedVideo("https://example.invalid/yt-dlp", 5, 10);
            Require((bool)Call(ytDlp.GetType(), ytDlp, "GenerateArguments"), "yt-dlp argument generation failed");
            Require(((string)Get(ytDlp, "Arguments")).Contains("--download-sections"), "yt-dlp time range was not emitted");
        }
        finally { Set(downloads, null, "YtdlType", original); }
    }

    private static void ReversedDownloadSectionsAreRejected() {
        Type downloads = T("youtube_dl_gui.Downloads");
        int original = StaticInt(downloads, "YtdlType");
        try {
            Set(downloads, null, "YtdlType", 0); // YtDlp
            object media = TimedExtendedVideo("https://example.invalid/reversed", 20, 10);
            Equal(false, Call(media.GetType(), media, "GenerateArguments"));
        }
        finally { Set(downloads, null, "YtdlType", original); }
    }

    private static void WmvProfileCheckIsCaseInsensitive() {
        object info = New("youtube_dl_gui.ConvertInfo", "input.mkv", "OUTPUT.WMV");
        Set(info.GetType(), info, "Type", Enum.Parse(T("youtube_dl_gui.ConversionType"), "Video"));
        Set(info.GetType(), info, "VideoUseProfile", true);
        Set(info.GetType(), info, "VideoProfile", 1);
        using (Form form = (Form)New("youtube_dl_gui.frmConverter", info)) {
            Call(form.GetType(), form, "BeginConversion");
            string arguments = ((TextBox)Field(form, "txtArgumentsGenerated")).Text;
            Thread worker = (Thread)Field(form, "ConverterThread");
            if (worker != null) PumpUntil(() => !worker.IsAlive, 5000, "Converter fixture did not exit");
            Require(arguments.IndexOf("-profile:v", StringComparison.Ordinal) < 0,
                "Uppercase .WMV output incorrectly received an incompatible video profile: " + arguments);
        }
    }

    private static void ExtendedTextBoxAlignmentMatchesEnum() {
        Type alignment = T("murrty.controls.ButtonAlignment");
        using (Control text = (Control)New("murrty.controls.ExtendedTextBox")) {
            text.Width = 220;
            Set(text.GetType(), text, "ButtonSize", new Size(24, 20));
            Set(text.GetType(), text, "ShowButton", true);
            IntPtr handle = text.Handle;
            Button button = (Button)Field(text, "InsetButton");
            Equal(Enum.Parse(alignment, "Right"), Get(text, "ButtonAlignment"));
            Require(button.Left > text.ClientSize.Width / 2, "Default Right alignment does not place the inset button on the right");

            Set(text.GetType(), text, "ButtonAlignment", Enum.Parse(alignment, "Left"));
            Require(button.Left <= 1, "Left alignment does not place the inset button on the left");

            Set(text.GetType(), text, "ButtonAlignment", Enum.Parse(alignment, "Right"));
            Require(button.Left > text.ClientSize.Width / 2, "Right alignment does not place the inset button on the right");
        }
    }

    private sealed class UpdaterRequestSink : NativeWindow, IDisposable {
        internal bool Received { get; private set; }

        internal UpdaterRequestSink() {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x1001) {
                Received = true;
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }

        public void Dispose() {
            DestroyHandle();
        }
    }

    private static void UpdaterParentExitWaitHonorsCloseCancellation() {
        string pidFile = Path.Combine(Environment.CurrentDirectory, "updater-parent-wait-" + Guid.NewGuid().ToString("N") + ".pid");
        Assembly updater = LoadUpdaterAssembly();
        Type program = updater.GetType("youtube_dl_gui_updater.Program", true);
        Type formType = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Type handlesType = updater.GetType("youtube_dl_gui_shared.ApplicationHandles", true);
        object previousToken = program.GetProperty("CancelToken", All).GetValue(null, null);
        int previousExitCode = (int)program.GetProperty("ExitCode", All).GetValue(null, null);
        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        using (UpdaterRequestSink sink = new UpdaterRequestSink()) {
            Process child = null;
            Form form = null;
            Task wait = null;
            try {
                Set(program, null, "CancelToken", cancellation);
                Set(program, null, "ExitCode", 1);
                child = Process.Start(Fixture("hang", pidFile));
                Require(child != null, "Could not start the updater parent fixture");
                PumpUntil(() => File.Exists(pidFile), 5000, "Updater parent fixture did not start");

                form = (Form)Activator.CreateInstance(formType, true);
                object handles = Activator.CreateInstance(
                    handlesType, All, null, new object[] { sink.Handle, child.Id },
                    System.Globalization.CultureInfo.InvariantCulture);
                Field(form, "ApplicationData", handles);
                Field(form, "ProgramProcess", child);

                wait = (Task)Call(formType, form, "WaitForApplication");
                Require(sink.Received, "Updater did not request update data before waiting for its parent");
                Call(formType, form, "OnFormClosing", new FormClosingEventArgs(CloseReason.UserClosing, false));
                Require(cancellation.IsCancellationRequested, "Closing the updater did not cancel its parent-exit wait");
                PumpUntil(() => wait.IsCompleted, 2000, "Closing the updater did not release the parent-exit wait");

                bool cancelled = false;
                try { wait.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled, "Parent-exit wait completed without reporting cancellation");
                Require(!child.HasExited, "Cancelling the updater terminated the parent application");
            }
            finally {
                if (form != null) form.Dispose();
                KillFixture(pidFile);
                if (wait != null && !wait.IsCompleted) {
                    try { wait.Wait(5000); } catch (AggregateException) { }
                }
                if (child != null) child.Dispose();
                try { File.Delete(pidFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                Set(program, null, "CancelToken", previousToken);
                Set(program, null, "ExitCode", previousExitCode);
            }
        }
    }

    private static void UpdaterStaleRequestTargetDoesNotWaitForParent() {
        string pidFile = Path.Combine(Environment.CurrentDirectory, "updater-stale-target-" + Guid.NewGuid().ToString("N") + ".pid");
        Assembly updater = LoadUpdaterAssembly();
        Type program = updater.GetType("youtube_dl_gui_updater.Program", true);
        Type formType = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Type handlesType = updater.GetType("youtube_dl_gui_shared.ApplicationHandles", true);
        object previousToken = program.GetProperty("CancelToken", All).GetValue(null, null);
        using (CancellationTokenSource cancellation = new CancellationTokenSource()) {
            Process child = null;
            Form form = null;
            Task wait = null;
            try {
                Set(program, null, "CancelToken", cancellation);
                child = Process.Start(Fixture("hang", pidFile));
                Require(child != null, "Could not start the stale-target parent fixture");
                PumpUntil(() => File.Exists(pidFile), 5000, "Stale-target parent fixture did not start");

                form = (Form)Activator.CreateInstance(formType, true);
                object handles = Activator.CreateInstance(
                    handlesType, All, null, new object[] { IntPtr.Zero, child.Id },
                    System.Globalization.CultureInfo.InvariantCulture);
                Field(form, "ApplicationData", handles);
                Field(form, "ProgramProcess", child);

                wait = (Task)Call(formType, form, "WaitForApplication");
                PumpUntil(() => wait.IsCompleted, 2500, "A stale updater message target left the updater waiting for the parent process");
                bool rejected = false;
                try { wait.GetAwaiter().GetResult(); }
                catch (TimeoutException) { rejected = true; }
                Require(rejected, "A stale updater message target was not rejected as a bounded IPC failure");
                Require(!child.HasExited, "Rejecting a stale updater message target terminated the parent application");
            }
            finally {
                if (form != null) form.Dispose();
                KillFixture(pidFile);
                if (wait != null && !wait.IsCompleted) {
                    try { wait.Wait(5000); } catch (AggregateException) { }
                }
                if (child != null) child.Dispose();
                try { File.Delete(pidFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                Set(program, null, "CancelToken", previousToken);
            }
        }
    }

    private static void UpdaterParentExitRaceCompletes() {
        string pidFile = Path.Combine(Environment.CurrentDirectory, "updater-exit-race-" + Guid.NewGuid().ToString("N") + ".pid");
        Assembly updater = LoadUpdaterAssembly();
        Type program = updater.GetType("youtube_dl_gui_updater.Program", true);
        Type formType = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Type handlesType = updater.GetType("youtube_dl_gui_shared.ApplicationHandles", true);
        object previousToken = program.GetProperty("CancelToken", All).GetValue(null, null);
        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        using (UpdaterRequestSink sink = new UpdaterRequestSink()) {
            Process child = null;
            Form form = null;
            try {
                Set(program, null, "CancelToken", cancellation);
                child = Process.Start(Fixture("exit", pidFile));
                Require(child != null, "Could not start the exiting parent fixture");
                Require(child.WaitForExit(5000), "Exiting parent fixture did not exit");

                form = (Form)Activator.CreateInstance(formType, true);
                object handles = Activator.CreateInstance(
                    handlesType, All, null, new object[] { sink.Handle, child.Id },
                    System.Globalization.CultureInfo.InvariantCulture);
                Field(form, "ApplicationData", handles);
                Field(form, "ProgramProcess", child);

                Task wait = (Task)Call(formType, form, "WaitForApplication");
                PumpUntil(() => wait.IsCompleted, 2000, "Updater did not complete when the parent had already exited");
                wait.GetAwaiter().GetResult();
                Require(sink.Received, "Updater skipped the update-data request during the parent exit race");
            }
            finally {
                if (form != null) form.Dispose();
                if (child != null) child.Dispose();
                try { File.Delete(pidFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                Set(program, null, "CancelToken", previousToken);
            }
        }
    }

    private static void GenericDownloadRecoversBackupBeforeCancelledAttempt() {
        string output = Path.Combine(Environment.CurrentDirectory, "generic-recovery-" + Guid.NewGuid().ToString("N") + ".exe");
        string backup = output + ".bck";
        string temp = output + ".tmp";
        const string knownGood = "known-good-prior-output";
        try {
            File.WriteAllText(backup, knownGood);
            File.WriteAllText(temp, "orphaned-partial-download");
            using (Form form = (Form)New("youtube_dl_gui.frmGenericDownloadProgress", "http://127.0.0.1:1/offline", output)) {
                IntPtr handle = form.Handle;
                CancellationTokenSource cancellation = (CancellationTokenSource)Field(form, "CancelToken");
                cancellation.Cancel();
                Task task = (Task)Call(form.GetType(), form, "RunDownload");
                PumpUntil(() => task.IsCompleted, 5000, "Cancelled generic recovery attempt did not complete");
                task.GetAwaiter().GetResult();
            }

            Require(File.Exists(output), "A known-good orphaned backup was not restored before the cancelled network attempt");
            Equal(knownGood, File.ReadAllText(output));
            Require(!File.Exists(backup), "Recovered backup remained stranded beside the restored live output");
        }
        finally {
            foreach (string path in new[] { output, backup, temp }) {
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    static partial void RunRepairWave5Tests() {
        Test("F_O026.GenericDownloadRestoresBackupBeforeCancelledAttempt", GenericDownloadRecoversBackupBeforeCancelledAttempt);
        Test("CURRENT_O005.ParentExitWaitHonorsCloseCancellation", UpdaterParentExitWaitHonorsCloseCancellation);
        Test("CURRENT_O005.StaleRequestTargetIsBounded", UpdaterStaleRequestTargetDoesNotWaitForParent);
        Test("CURRENT_O005.ParentExitRaceCompletes", UpdaterParentExitRaceCompletes);
        Test("CURRENT_O004.ForcedThumbnailReplacementDisposesOldImage", ForcedThumbnailReplacementDisposesOldImage);
        Test("CURRENT_O014.DownloadSectionsRequireYtDlp", DownloadSectionsRequireYtDlp);
        Test("CURRENT_O036.ReversedDownloadSectionsAreRejected", ReversedDownloadSectionsAreRejected);
        Test("F_O029.WmvProfileCheckIsCaseInsensitive", WmvProfileCheckIsCaseInsensitive);
        Test("F_O034.ExtendedTextBoxAlignmentMatchesEnum", ExtendedTextBoxAlignmentMatchesEnum);
    }
}

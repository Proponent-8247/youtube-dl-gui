using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void PumpUntil(Func<bool> condition, int milliseconds, string message) {
        Stopwatch clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < milliseconds) {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Require(condition(), message);
    }
    private static void CloseLiveTransfer(string kind, bool flood = false) {
        string pidFile = Path.Combine(Environment.CurrentDirectory, "ui-" + kind + "-" + Guid.NewGuid().ToString("N") + ".pid");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", flood ? "flood" : "hang");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", pidFile);
        Type verification = T("youtube_dl_gui.Verification");
        object oldDownloader = verification.GetProperty("YoutubeDlPath", All).GetValue(null, null);
        object oldConverter = verification.GetProperty("FFmpegPath", All).GetValue(null, null);
        if (flood) {
            string fixture = Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe");
            Set(verification, null, "YoutubeDlPath", fixture);
            Set(verification, null, "FFmpegPath", fixture);
        }
        Form form = null;
        Thread worker = null;
        // Program.Main normally owns this handler. Closing a real processing form
        // must exercise the same lifecycle rather than dereference an absent fixture.
        Form handler = (Form)New("youtube_dl_gui.MessageHandler");
        Set(T("youtube_dl_gui.Program"), null, "QueueHandler", handler);
        try {
            if (kind == "quick") {
                object info = Download("https://example.invalid/fixture");
                Set(info.GetType(), info, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
                Set(info.GetType(), info, "MostlyCustomArguments", true);
                Set(info.GetType(), info, "CustomArguments", "--simulate");
                form = (Form)New("youtube_dl_gui.frmDownloader", info);
            }
            else if (kind == "converter") {
                object info = New("youtube_dl_gui.ConvertInfo", "--simulate");
                form = (Form)New("youtube_dl_gui.frmConverter", info);
            }
            else {
                form = (Form)New("youtube_dl_gui.frmExtendedDownloader");
                ListView queue = (ListView)Field(form, "lvQueuedMedia");
                object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/fixture");
                Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
                Set(media.GetType(), media, "CustomArguments", "--simulate");
                Set(media.GetType(), media, "InfoRetrieved", true);
                Set(media.GetType(), media, "BatchDownloadItem", true);
                ListViewItem item = new ListViewItem("fixture");
                item.Tag = media;
                Set(media.GetType(), media, "QueueItem", item);
                queue.Items.Add(item);
            }
            form.Show();
            if (kind == "extended") Call(form.GetType(), form, "BeginDownload", false);
            PumpUntil(() => File.Exists(pidFile), 10000, "The " + kind + " transfer fixture did not start");
            worker = (Thread)(kind == "extended" ? Get(form, "ProcessingThread") : Field(form, kind == "quick" ? "DownloadThread" : "ConverterThread"));
            Require(worker != null && worker.IsAlive, "Transfer worker is not running");
            if (flood) {
                TextBoxBase console = (TextBoxBase)Field(form, kind == "converter" ? "rtbConsoleOutput" : "rtbVerbose");
                PumpUntil(() => console.Text.Contains("audit stress"), 5000, "No live output reached the transfer form");
            }
            form.Close();
            PumpUntil(() => !worker.IsAlive && form.IsDisposed, 12000, "Close did not complete after cancelling the " + kind + " transfer");
            AssertGone(pidFile);
        }
        finally {
            KillFixture(pidFile);
            if (form != null && !form.IsDisposed) form.Dispose();
            if (worker != null) PumpUntil(() => !worker.IsAlive, 5000, "Transfer test left a worker running");
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", null);
            Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", null);
            Set(verification, null, "YoutubeDlPath", oldDownloader);
            Set(verification, null, "FFmpegPath", oldConverter);
            handler.Dispose();
            Set(T("youtube_dl_gui.Program"), null, "QueueHandler", null);
        }
    }
    static partial void RunConversionOptionTests();
    static partial void RunConverterTests() {
        Test("O043.LinkLabelPreservesConfiguredColorsAcrossHover", () => {
            Control control = (Control)New("murrty.controls.ExtendedLinkLabel");
            try {
                Type type = control.GetType();
                System.Reflection.PropertyInfo linkColor = type.GetProperty("LinkColor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                System.Reflection.PropertyInfo visitedLinkColor = type.GetProperty("VisitedLinkColor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                Color normal = Color.FromArgb(12, 34, 56);
                Color visited = Color.FromArgb(65, 43, 21);
                Color changedWhileHovered = Color.FromArgb(90, 80, 70);
                linkColor.SetValue(control, normal, null);
                visitedLinkColor.SetValue(control, visited, null);

                Call(type, control, "OnMouseEnter", EventArgs.Empty);
                Equal(normal, linkColor.GetValue(control, null));
                Equal(visited, visitedLinkColor.GetValue(control, null));
                Equal(Color.FromArgb(0x33, 0x99, 0xFF), typeof(LinkLabel).GetProperty("LinkColor").GetValue(control, null));
                Equal(Color.FromArgb(0xA4, 0x00, 0xA4), typeof(LinkLabel).GetProperty("VisitedLinkColor").GetValue(control, null));

                linkColor.SetValue(control, changedWhileHovered, null);
                Call(type, control, "OnMouseLeave", EventArgs.Empty);
                Equal(changedWhileHovered, typeof(LinkLabel).GetProperty("LinkColor").GetValue(control, null));
                Equal(visited, typeof(LinkLabel).GetProperty("VisitedLinkColor").GetValue(control, null));

                Call(type, control, "OnMouseEnter", EventArgs.Empty);
                Call(type, control, "OnMouseLeave", EventArgs.Empty);
                Equal(changedWhileHovered, linkColor.GetValue(control, null));
                Equal(visited, visitedLinkColor.GetValue(control, null));
            }
            finally { control.Dispose(); }
        });
        Test("D006.QuickCloseCompletesAfterCleanup", () => CloseLiveTransfer("quick"));
        Test("D006.ConverterCloseCompletesAfterCleanup", () => CloseLiveTransfer("converter"));
        Test("D006.ExtendedCloseCompletesAfterCleanup", () => CloseLiveTransfer("extended"));
        foreach (string kind in new[] { "quick", "converter", "extended" }) {
            string captured = kind;
            Test("Stress." + captured + "CloseDuringConcurrentOutput", () => {
                for (int repeat = 0; repeat < 3; repeat++) CloseLiveTransfer(captured, true);
            });
        }
        RunConversionOptionTests();
    }
}

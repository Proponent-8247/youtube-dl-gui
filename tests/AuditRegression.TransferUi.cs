using System;
using System.Diagnostics;
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
    private static void CloseLiveTransfer(string kind) {
        string pidFile = Path.Combine(Environment.CurrentDirectory, "ui-" + kind + "-" + Guid.NewGuid().ToString("N") + ".pid");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", "hang");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", pidFile);
        Form form = null;
        Thread worker = null;
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
        }
    }
    static partial void RunConversionOptionTests();
    static partial void RunConverterTests() {
        Test("D006.QuickCloseCompletesAfterCleanup", () => CloseLiveTransfer("quick"));
        Test("D006.ConverterCloseCompletesAfterCleanup", () => CloseLiveTransfer("converter"));
        Test("D006.ExtendedCloseCompletesAfterCleanup", () => CloseLiveTransfer("extended"));
        RunConversionOptionTests();
    }
}

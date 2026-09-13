using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void VerifyProgressUiMarshal(Form form, string label) {
        Type type = form.GetType();
        FieldInfo progressField = type.GetField("pbStatus", All);
        Require(progressField != null, label + " has no progress control");
        Control progress = (Control)progressField.GetValue(form);
        IntPtr formHandle = form.Handle;
        IntPtr progressHandle = progress.Handle;
        int uiThread = Thread.CurrentThread.ManagedThreadId;
        int callbackThread = -1;
        bool invoked = false;
        Exception workerFailure = null;

        using (ManualResetEventSlim finished = new ManualResetEventSlim(false)) {
            Thread worker = new Thread(() => {
                try {
                    invoked = (bool)Call(type, form, "TryInvokeProgress", (Action)(() => {
                        callbackThread = Thread.CurrentThread.ManagedThreadId;
                        progress.Text = "audit-ui-thread";
                    }));
                }
                catch (Exception ex) {
                    workerFailure = ex;
                }
                finally {
                    finished.Set();
                }
            });
            worker.IsBackground = true;
            worker.Start();
            PumpUntil(() => finished.IsSet, 5000, label + " progress marshal did not complete");
            Require(worker.Join(5000), label + " progress marshal worker did not stop");
        }

        if (workerFailure != null) throw new Exception(label + " progress marshal failed", workerFailure);
        Equal(true, invoked);
        Equal(uiThread, callbackThread);
        Equal("audit-ui-thread", progress.Text);
    }

    private static void DownloaderLiveOutputUsesUiMarshal() {
        bool previous = Control.CheckForIllegalCrossThreadCalls;
        Control.CheckForIllegalCrossThreadCalls = true;
        try {
            object info = Download("https://example.invalid/video");
            using (Form form = (Form)New("youtube_dl_gui.frmDownloader", info)) {
                VerifyProgressUiMarshal(form, "Quick downloader");
            }
        }
        finally {
            Control.CheckForIllegalCrossThreadCalls = previous;
        }
    }

    private static void ExtendedDownloaderLiveOutputUsesUiMarshal() {
        bool previous = Control.CheckForIllegalCrossThreadCalls;
        Control.CheckForIllegalCrossThreadCalls = true;
        try {
            using (Form form = (Form)New("youtube_dl_gui.frmExtendedDownloader")) {
                VerifyProgressUiMarshal(form, "Extended downloader");
            }
        }
        finally {
            Control.CheckForIllegalCrossThreadCalls = previous;
        }
    }

    private static void RunRepairWave12Tests() {
        Test("CURRENT_O034.DownloaderLiveOutputUsesUiMarshal", DownloaderLiveOutputUsesUiMarshal);
        Test("CURRENT_O034.ExtendedDownloaderLiveOutputUsesUiMarshal", ExtendedDownloaderLiveOutputUsesUiMarshal);
    }
}

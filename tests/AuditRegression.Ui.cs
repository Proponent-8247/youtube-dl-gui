using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void Field(object target, string name, object value) {
        target.GetType().GetField(name, All).SetValue(target, value);
    }
    private static object Field(object target, string name) {
        return target.GetType().GetField(name, All).GetValue(target);
    }
    private static object DownloadState(string name) {
        return Enum.Parse(T("youtube_dl_gui.DownloadStatus"), name);
    }
    private static void LiveExtendedWorker(Action<Form> action) {
        using (Form form = (Form)New("youtube_dl_gui.frmExtendedDownloader"))
        using (ManualResetEvent release = new ManualResetEvent(false)) {
            Thread worker = new Thread(() => release.WaitOne());
            worker.IsBackground = true;
            worker.Start();
            Set(form.GetType(), form, "ProcessingThread", worker);
            try { action(form); }
            finally { release.Set(); Require(worker.Join(5000), "Test worker did not stop"); }
        }
    }
    static partial void RunUiTests() {
        Test("D005.ProgressCannotEraseCancellation", () => {
            using (Form form = (Form)New("youtube_dl_gui.frmExtendedDownloader")) {
                Field(form, "CancellationRequested", true);
                foreach (string state in new[] { "Downloading", "MergingFiles", "Converting", "EmbeddingMetadata", "Finished", "ProgramError" }) {
                    Set(form.GetType(), form, "Status", DownloadState(state));
                    Equal(DownloadState("Aborted"), Get(form, "Status"));
                }
            }
        });
        Test("D005.ProgressCannotEraseCloseCancellation", () => {
            using (Form form = (Form)New("youtube_dl_gui.frmExtendedDownloader")) {
                Field(form, "CancellationRequested", true);
                Set(form.GetType(), form, "Status", DownloadState("AbortForClose"));
                Set(form.GetType(), form, "Status", DownloadState("Downloading"));
                Equal(DownloadState("AbortForClose"), Get(form, "Status"));
            }
        });
        Test("D001.RetryCannotResetLiveWorker", () => LiveExtendedWorker(form => {
            Field(form, "CancellationRequested", true);
            object originalWorker = Get(form, "ProcessingThread");
            Call(form.GetType(), form, "BeginDownload", false);
            Equal(true, Field(form, "CancellationRequested"));
            Require(ReferenceEquals(originalWorker, Get(form, "ProcessingThread")), "A live worker was replaced");
        }));
        Test("D001.QueueRemovalBlockedDuringPostProcessing", () => LiveExtendedWorker(form => {
            ListView queue = (ListView)Field(form, "lvQueuedMedia");
            IntPtr handle = queue.Handle;
            queue.Items.Add(new ListViewItem("https://example.invalid/one"));
            queue.Items.Add(new ListViewItem("https://example.invalid/two"));
            queue.Items[0].Selected = true;
            Equal(1, queue.SelectedItems.Count);
            Set(form.GetType(), form, "Status", DownloadState("MergingFiles"));
            Call(form.GetType(), form, "mQueueRemoveSelected_Click", form, EventArgs.Empty);
            Equal(2, queue.Items.Count);
        }));
    }
}

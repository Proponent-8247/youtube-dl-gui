#nullable disable
namespace youtube_dl_gui;
using System;

internal partial class frmDownloader {
    private void HistoryMessage(string message) {
        if (IsDisposed || !IsHandleCreated) return;
        try { Invoke(new Action(() => { if (!rtbVerbose.IsDisposed) rtbVerbose.AppendLine(message); })); }
        catch (InvalidOperationException) { }
    }
    private void StartProtectedDownload() {
        DownloadHistoryRuntime.Start(DownloadProcess,
            () => CurrentDownload.Status == DownloadStatus.Aborted || CurrentDownload.Status == DownloadStatus.AbortForClose,
            HistoryMessage);
    }
    private bool CompleteProtectedDownload() { return DownloadHistoryRuntime.Complete(DownloadProcess, HistoryMessage); }
}

public partial class frmExtendedDownloader {
    private void HistoryMessage(string message) {
        if (IsDisposed || !IsHandleCreated) return;
        try { Invoke(new Action(() => { if (!rtbVerbose.IsDisposed) rtbVerbose.AppendLine(message); })); }
        catch (InvalidOperationException) { }
    }
    private void StartProtectedDownload() {
        DownloadHistoryRuntime.Start(DownloadProcess,
            () => Status == DownloadStatus.Aborted || Status == DownloadStatus.AbortForClose,
            HistoryMessage);
    }
    private bool CompleteProtectedDownload() { return DownloadHistoryRuntime.Complete(DownloadProcess, HistoryMessage); }
}

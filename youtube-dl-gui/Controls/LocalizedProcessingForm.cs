#nullable enable
namespace youtube_dl_gui;
using System;
public class LocalizedProcessingForm : LocalizedForm {
    private System.Windows.Forms.Timer? workerCloseTimer;
    protected bool WorkerClosePending { get; private set; }

    // Keep pumping UI callbacks while workers release processes and output pipes.
    // The timer closes only after the actual threads exit, not on a progress status.
    protected bool DeferCloseForWorkers(params System.Threading.Thread?[] workers) {
        if (!System.Array.Exists(workers, worker => worker?.IsAlive == true)) return false;
        WorkerClosePending = true;
        if (workerCloseTimer is null) {
            workerCloseTimer = new System.Windows.Forms.Timer { Interval = 50 };
            workerCloseTimer.Tick += (sender, args) => {
                if (System.Array.Exists(workers, worker => worker?.IsAlive == true)) return;
                workerCloseTimer?.Stop();
                workerCloseTimer?.Dispose();
                workerCloseTimer = null;
                if (!IsDisposed) Close();
            };
            Disposed += (sender, args) => {
                workerCloseTimer?.Stop();
                workerCloseTimer?.Dispose();
                workerCloseTimer = null;
            };
            workerCloseTimer.Start();
        }
        return true;
    }

    protected override void OnShown(EventArgs e) {
        Program.AddProcessingForm(this);
        try {
            base.OnShown(e);
        }
        catch {
            Program.RemoveProcessingForm(this);
            throw;
        }
    }
    protected override void Dispose(bool disposing) {
        try {
            base.Dispose(disposing);
        }
        finally {
            if (disposing) Program.RemoveProcessingForm(this);
        }
    }
    protected override void OnClosed(EventArgs e) {
        try {
            base.OnClosed(e);
        }
        finally {
            Program.RemoveProcessingForm(this);
        }
    }
}
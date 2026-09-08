#nullable enable
namespace youtube_dl_gui;
using System;
public class LocalizedProcessingForm : LocalizedForm {
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
    protected override void OnClosed(EventArgs e) {
        try {
            base.OnClosed(e);
        }
        finally {
            Program.RemoveProcessingForm(this);
        }
    }
}
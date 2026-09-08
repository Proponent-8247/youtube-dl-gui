#nullable enable
namespace youtube_dl_gui;
using System;
using System.Windows.Forms;
public class LocalizedForm : Form, ILocalizedForm {
    private bool LocalizationRegistered;

    // This is supposed to be overridden.
    public virtual void LoadLanguage() { }
    public string GetFormName() => this.Name;
    /// <inheritdoc/>
    protected override void OnLoad(EventArgs e) {
        Language.RegisterForm(this);
        LocalizationRegistered = true;
        try {
            base.OnLoad(e);
        }
        catch {
            UnregisterLocalization();
            throw;
        }
    }
    /// <inheritdoc/>
    protected override void OnFormClosing(FormClosingEventArgs e) {
        base.OnFormClosing(e);
        if (!e.Cancel) {
            UnregisterLocalization();
        }
    }
    protected override void Dispose(bool disposing) {
        if (disposing) {
            UnregisterLocalization();
        }
        base.Dispose(disposing);
    }
    private void UnregisterLocalization() {
        if (!LocalizationRegistered) {
            return;
        }
        Language.UnregisterForm(this);
        LocalizationRegistered = false;
    }
}

/// <summary>
/// Represents a localized form that has text that is dependant on the loaded language.
/// </summary>
internal interface ILocalizedForm {
    /// <summary>
    /// Applies the localized text onto controls.
    /// </summary>
    public void LoadLanguage();
    public string GetFormName();
}
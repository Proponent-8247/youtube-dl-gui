using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static Timer DialogMonitor;
    private static readonly List<string> UnexpectedDialogs = new List<string>();
    private static readonly HashSet<Form> PendingDialogs = new HashSet<Form>();
    static partial void RunBoundaryTests() {
        // Language/log initialization has already installed the application's handler.
        // Capture its real UI error instead of allowing an unattended modal dialog to hang CI.
        DialogMonitor = new Timer { Interval = 100 };
        DialogMonitor.Tick += (sender, args) => {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) {
                if (form.GetType().FullName != "murrty.logging.frmException" || !PendingDialogs.Add(form)) continue;
                try {
                    form.BeginInvoke((Action)(() => {
                        if (form.IsDisposed) return;
                        string detail = string.Join("\n", DiagnosticText(form));
                        lock (UnexpectedDialogs) UnexpectedDialogs.Add(detail);
                        Console.WriteLine("UNEXPECTED APPLICATION ERROR: " + detail);
                        form.DialogResult = DialogResult.Abort;
                        form.Close();
                    }));
                }
                catch (InvalidOperationException) { }
            }
        };
        DialogMonitor.Start();
    }
    private static IEnumerable<string> DiagnosticText(Control control) {
        if (control is TextBoxBase && !string.IsNullOrEmpty(control.Text)) yield return control.Text;
        foreach (Control child in control.Controls)
            foreach (string text in DiagnosticText(child)) yield return text;
    }
    static partial void RunUpdaterTests() {
        Test("Harness.NoUnexpectedApplicationDialogs", () => {
            lock (UnexpectedDialogs) Require(UnexpectedDialogs.Count == 0, string.Join("\n", UnexpectedDialogs));
        });
        DialogMonitor.Stop();
        DialogMonitor.Dispose();
    }
}

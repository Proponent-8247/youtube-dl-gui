using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void DisposedMainFormCannotRecreateClipboardListenerDuringExit() {
        Type mainType = T("youtube_dl_gui.frmMain");
        Form main = (Form)Activator.CreateInstance(mainType, true);
        try {
            FieldInfo active = mainType.GetField("ClipboardScannerActive", All);
            Require(active != null, "Clipboard listener active field missing");
            active.SetValue(main, true);
            main.Dispose();
            Require(main.IsDisposed && !main.IsHandleCreated,
                "Test did not dispose the main form and its window handle");
            // Simulates an already queued shutdown event after updater-driven
            // MainForm.Dispose(); touching Handle here would recreate a dead HWND.
            Call(mainType, main, "ApplicationExit", null, EventArgs.Empty);
            Equal(false, active.GetValue(main));
            Require(!main.IsHandleCreated,
                "ApplicationExit created a new handle after the form was disposed");

            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmMain.cs"));
            Require(source.Contains("this.Disposed +=") &&
                source.Contains("Application.ApplicationExit -= ApplicationExit"),
                "Main form does not detach its process-wide exit handler when disposed");
        }
        finally { if (!main.IsDisposed) main.Dispose(); }
    }
}

using System;
using System.Reflection;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void HiddenLogAppliesCurrentLanguageBeforeFirstDisplay() {
        Type language = T("youtube_dl_gui.Language");
        string[] keys = { "frmLog", "frmLogExceptions", "lbLogPastExceptions",
            "GenericRemove", "frmLogClear", "GenericClose" };
        string[] originals = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++) {
            PropertyInfo property = language.GetProperty(keys[i], All);
            Require(property != null, "Missing language contract property " + keys[i]);
            originals[i] = (string)property.GetValue(null, null);
        }

        Form form = null;
        try {
            form = (Form)New("murrty.logging.frmLog");
            Require(!form.IsHandleCreated,
                "Hidden log localization fixture unexpectedly created a form handle");
            for (int i = 0; i < keys.Length; i++)
                Set(language, null, keys[i], "translated-" + keys[i]);

            // The original Language.LocalizationChanged skipped a form that had
            // no handle. ShowLog must always replay the complete live contract.
            Call(form.GetType(), form, "ShowLog");
            Require(form.IsHandleCreated && form.Visible, "Log did not show");
            Equal("translated-frmLog", form.Text);
            string[,] checks = {
                { "tpMainLog", "frmLog" },
                { "tpExceptions", "frmLogExceptions" },
                { "lbExceptionDetails", "lbLogPastExceptions" },
                { "btnRemoveException", "GenericRemove" },
                { "btnClear", "frmLogClear" },
                { "btnClose", "GenericClose" }
            };
            for (int i = 0; i < checks.GetLength(0); i++) {
                Control control = (Control)Field(form, checks[i, 0]);
                Require(control != null, "Log control missing " + checks[i, 0]);
                Equal("translated-" + checks[i, 1], control.Text);
            }
        }
        finally {
            if (form != null) form.Dispose();
            for (int i = 0; i < keys.Length; i++)
                Set(language, null, keys[i], originals[i]);
        }
    }
}

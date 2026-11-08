using System;
using System.IO;
using System.Reflection;

internal static partial class AuditRegression {
    private static void ExceptionDialogRollDisposesCryptographicGenerator() {
        Type dialog = T("murrty.logging.frmException");
        MethodInfo generate = dialog.GetMethod("GenerateUwURoll", All);
        Require(generate != null && generate.IsStatic && generate.ReturnType == typeof(int),
            "Exception dialog has no isolated, synchronous cryptographic-roll helper");

        // Exercise real compiled helper repeatedly, without opening modal error dialogs.
        for (int i = 0; i < 256; i++) {
            object result = generate.Invoke(null, null);
            Require(result is int && (int)result >= 0 && (int)result < 5000,
                "Cryptographic exception-dialog roll left its previous range");
        }

        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string source = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Logging", "Forms", "frmException.cs"));
        int begin = source.IndexOf("private static int GenerateUwURoll()", StringComparison.Ordinal);
        int end = source.IndexOf("public frmException(", begin < 0 ? 0 : begin, StringComparison.Ordinal);
        Require(begin >= 0 && end > begin, "Scoped RNG helper is not separate from dialog constructor");
        string helper = source.Substring(begin, end - begin);
        Require(helper.Contains("using RandomNumberGenerator") &&
            helper.Contains("RandomNumberGenerator.Create()") &&
            !helper.Contains("new RNGCryptoServiceProvider()"),
            "Per-dialog RNG is not owned by a deterministic using/Dispose scope");
    }
}

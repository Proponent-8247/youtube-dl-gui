using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static partial class AuditRegression {
    private static void NativeArchiveRejectsOversizedLineBeforeAllocatingWholeEntry() {
        Type history = T("youtube_dl_gui.DownloadHistory");
        string path = Path.Combine(Environment.CurrentDirectory,
            "oversized-native-archive-" + Guid.NewGuid().ToString("N") + ".txt");
        try {
            string valid = "youtube abc123\n";
            File.WriteAllText(path, valid, new UTF8Encoding(false));
            HashSet<string> entries = new HashSet<string>(StringComparer.Ordinal);
            object[] accepted = { path, entries, null };
            Equal(true, Call(history, null, "TryReadArchive", accepted));
            Require(entries.Contains("youtube abc123"), "Small valid archive line was rejected");

            string huge = "youtube " + new string('x', 2 * 1024 * 1024);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) {
                writer.Write(valid);
                writer.Write(huge); // Intentionally no terminating newline.
            }
            HashSet<string> untouched = new HashSet<string>(StringComparer.Ordinal) { "retain-me" };
            object[] rejected = { path, untouched, null };
            Equal(false, Call(history, null, "TryReadArchive", rejected));
            string error = (string)rejected[2];
            Require(error != null && error.IndexOf("exceeds", StringComparison.OrdinalIgnoreCase) >= 0 &&
                error.Length < 240,
                "Overlong archive line did not fail closed with a bounded non-echoing message");
            Require(!error.Contains(new string('x', 64)),
                "Oversized archive input was echoed into a diagnostic");
            Equal(1, untouched.Count);
            Require(untouched.Contains("retain-me"),
                "Invalid input partially published its parsed identities");
        }
        finally { try { File.Delete(path); } catch { } }
    }
}

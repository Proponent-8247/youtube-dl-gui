using System;
using System.IO;
using System.Text;

internal static partial class AuditRegression {
    private static void RawProviderSessionHistoryHasBoundedRecentWindow() {
        Type log = T("murrty.logging.Log");
        const int quota = 32768, retain = 16384;
        string path = Path.Combine(Environment.CurrentDirectory,
            "session-quota-" + Guid.NewGuid().ToString("N") + ".log");
        try {
            for (int i = 0; i < 140; i++) {
                string channel = (i % 2 == 0) ? "RAW-PROVIDER-STDOUT" : "RAW-PROVIDER-STDERR";
                string input = "[" + channel + "] record-" + i.ToString("D3") +
                    " " + new string((char)('A' + i % 20), 700) + "\n";
                Call(log, null, "AppendBoundedSessionHistory", path, input, quota, retain);
                Require(new FileInfo(path).Length <= quota,
                    "Session-history quota was exceeded after provider output chunk " + i);
            }
            string output = File.ReadAllText(path, Encoding.UTF8);
            Require(output.Contains("older session history discarded"),
                "Rotated raw-provider history omitted its truncation notice");
            Require(output.Contains("record-139"),
                "Bounded session history did not preserve the newest provider diagnostics");
            Require(!output.Contains("record-000"),
                "Bounded session history retained stale diagnostics despite quota trimming");
            Require(output.Contains("RAW-PROVIDER-STDOUT") &&
                output.Contains("RAW-PROVIDER-STDERR"),
                "Quota rotation lost the stdout/stderr channel distinctions");
            Require(new FileInfo(path).Length > retain,
                "Synthetic quota fixture did not retain its recent diagnostic window");

            // A path with extremely large individual input must still be bounded.
            Call(log, null, "AppendBoundedSessionHistory", path, new string('z', quota * 3),
                quota, retain);
            Require(new FileInfo(path).Length <= quota,
                "One oversized diagnostic chunk bypassed the session-history quota");
        }
        finally { try { File.Delete(path); } catch { } }
    }
}

using System;
using System.IO;

internal static partial class AuditRegression {
    private static void ExternalCustomProtocolNeverSilentlyReplaysAmbientHeaders() {
        Type program = T("youtube_dl_gui.Program");
        string target = "https://receiver.invalid/video";
        string stored = "--add-header \"Authorization: Bearer AUDIT-NOT-A-REAL-SECRET\"";
        int confirmations = 0;
        Func<string, string, bool> deny = (url, args) => {
            confirmations++;
            Equal(target, url);
            Equal(stored, args);
            return false;
        };
        object[] refused = { target, stored, deny, null };
        Equal(false, Call(program, null, "TryApproveExternalCustomArguments", refused));
        Equal(string.Empty, (string)refused[3]);
        Equal(1, confirmations);

        object[] noSensitive = { target, string.Empty, deny, null };
        Equal(true, Call(program, null, "TryApproveExternalCustomArguments", noSensitive));
        Equal(string.Empty, (string)noSensitive[3]);
        Equal(1, confirmations);

        Func<string, string, bool> approve = (url, args) => {
            confirmations++;
            Equal(target, url);
            Equal(stored, args);
            return true;
        };
        object[] accepted = { target, stored, approve, null };
        Equal(true, Call(program, null, "TryApproveExternalCustomArguments", accepted));
        Equal(stored, (string)accepted[3]);
        Equal(2, confirmations);

        object[] oversized = { target, new string('a', 9000), approve, null };
        Equal(false, Call(program, null, "TryApproveExternalCustomArguments", oversized));
        Equal(string.Empty, (string)oversized[3]);
        Equal(2, confirmations);

        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string src = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Program.cs"));
        int normal = src.IndexOf("CheckArgs(", StringComparison.Ordinal);
        int copy = src.IndexOf("internal static void ProcessCopyData(", StringComparison.Ordinal);
        Require(normal >= 0 && copy > normal,
            "Application command entrypoints were not located");
        string first = src.Substring(normal, copy - normal);
        string second = src.Substring(copy);
        Require(first.Contains("TryApproveExternalCustomArguments") &&
            second.Contains("TryApproveExternalCustomArguments"),
            "CLI or second-instance protocol bypasses inherited custom argument authorization");
        Require(!src.Contains("CustomArguments = youtube_dl_gui.CustomArguments.LastUsedYtdlArgument"),
            "Application still forwards persisted custom options without explicit consent");
    }
}

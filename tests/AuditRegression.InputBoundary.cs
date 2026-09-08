using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

internal static partial class AuditRegression {
    private static void VerifySingleLogWrite(bool updater) {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string assembly = updater ? Path.Combine(root, "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe") : App.Location;
        string scratch = Path.Combine(Environment.CurrentDirectory, "log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string fixture = Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe");
        object result = RunOwned(new ProcessStartInfo(fixture, "--log-probe " + Escape(assembly)) { WorkingDirectory = scratch }, CancellationToken.None, 10000, 65536);
        Equal(0, Get(result, "ExitCode"));
        Require(((string)Get(result, "StandardOutput")).EndsWith("written", StringComparison.Ordinal), "Log writer did not return after writing");
        string[] logs = Directory.GetFiles(scratch, "ex_*.log");
        Equal(1, logs.Length);
        Require(File.ReadAllText(logs[0]).Contains("audit-log-fixture"), "Exception log content was not preserved");
    }
    private static void RunInputBoundaryTests() {
        Test("N014.ApplicationLogWriteReturnsAfterSuccess", () => VerifySingleLogWrite(false));
        Test("N014.UpdaterLogWriteReturnsAfterSuccess", () => VerifySingleLogWrite(true));
        Test("N013.LongClipboardTextCompletesWithinDeadline", () => {
            string fixture = Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe");
            object result = RunOwned(new ProcessStartInfo(fixture, "--url-probe " + Escape(App.Location)), CancellationToken.None, 5000, 65536);
            Equal(0, Get(result, "ExitCode"));
            Equal("False", Get(result, "StandardOutput"));
        });
        Test("N013.ClipboardHeuristicPreservesAcceptedInputs", () => {
            foreach (string text in new[] { "a.b", "https://example.invalid/playlist?a=1", "before\na.b\nafter", "a b.c d", "..." }) {
                Equal(true, Call(T("youtube_dl_gui.DownloadHelper"), null, "SupportedDownloadLink", text));
            }
            foreach (string text in new[] { "", "a", ".", "a.", ".b", "a\n.b", "a.\nb", "no dot here" }) {
                Equal(false, Call(T("youtube_dl_gui.DownloadHelper"), null, "SupportedDownloadLink", text));
            }
        });
        Test("Progress.MalformedRowsCannotThrow", () => {
            foreach (string percent in new[] { null, "", "%", "NaN%", "Infinity%", "1.0%", "12345678900000%", "0,5%" }) {
                for (int length = 0; length <= 12; length++) {
                    string[] parts = new string[length];
                    for (int i = 0; i < length; i++) parts[i] = "x";
                    if (length > 0) parts[0] = "[download]";
                    if (length > 1) parts[1] = percent;
                    if (length > 3) parts[3] = "~";
                    object[] args = { parts, 0f, "" };
                    Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", args);
                }
            }
            object[] empty = { null, 0f, "" };
            Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", empty);
        });
        Test("Progress.PercentageUsesInvariantCulture", () => {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                object[] args = { new[] { "[download]", "12.5%", "of", "10MiB", "at", "1MiB/s", "ETA", "00:10" }, 0f, "" };
                Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", args);
                Equal(12.5f, args[1]);
                Equal("00:10", args[2]);
            }
            finally { Thread.CurrentThread.CurrentCulture = saved; }
        });
    }
}

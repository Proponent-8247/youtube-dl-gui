#nullable disable
namespace youtube_dl_gui;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using youtube_dl_gui.History;

internal static class DownloadHistoryRuntime {
    private static readonly ConditionalWeakTable<Process, HistoryProcessGuard> Guards = new ConditionalWeakTable<Process, HistoryProcessGuard>();
    private static readonly object ProviderGate = new object();
    private static string verifiedProvider;

    public static string ProbePrefix { get { return DownloadHistorySettings.Enabled ? "--ignore-config " : ""; } }
    public static string PreviewSuffix() {
        if (!DownloadHistorySettings.Enabled) return "";
        try {
            var options = DownloadHistorySettings.Options(DownloadHistorySettings.Current, Downloads.fileNameSchema);
            return HistoryCommandPolicy.Suffix(options.ArchivePath);
        }
        catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is NotSupportedException) { return ""; }
    }
    public static void CheckProvider(string path) {
        if (Downloads.YtdlType != (int)GitID.YtDlp && Downloads.YtdlType != (int)GitID.YtDlpNightly)
            throw new HistoryException("Native Download History requires yt-dlp. Select yt-dlp in Settings, or disable Download History to use the legacy provider.");
        if (string.IsNullOrWhiteSpace(path)) throw new HistoryException("yt-dlp could not be found.");
        var file = new FileInfo(path);
        string signature = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
        lock (ProviderGate) {
            if (verifiedProvider == signature) return;
            var output = new StringBuilder();
            var sync = new object();
            using (var probe = new Process()) {
                probe.StartInfo = new ProcessStartInfo(path,
                    "--ignore-config --no-break-on-existing --abort-on-unavailable-fragments --write-info-json --help") {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                };
                DataReceivedEventHandler receive = (sender, e) => {
                    if (e.Data != null) lock (sync) { if (output.Length < 500000) output.AppendLine(e.Data); }
                };
                probe.OutputDataReceived += receive;
                probe.ErrorDataReceived += receive;
                try {
                    probe.Start(); probe.BeginOutputReadLine(); probe.BeginErrorReadLine();
                    if (!probe.WaitForExit(15000)) throw new HistoryException("The yt-dlp capability check timed out; no media download was started.");
                    probe.WaitForExit();
                    string help;
                    lock (sync) help = output.ToString();
                    if (probe.ExitCode != 0 || !help.Contains("--download-archive") || !help.Contains("--no-break-on-existing"))
                        throw new HistoryException("The selected executable does not support the required yt-dlp archive options. Update/select a compatible yt-dlp executable before enabling protection.");
                }
                finally {
                    try { if (!probe.HasExited) { probe.Kill(); probe.WaitForExit(5000); } }
                    catch (InvalidOperationException) { }
                }
            }
            verifiedProvider = signature;
        }
    }
    public static void Start(Process process, Func<bool> cancelled, Action<string> notify) {
        if (!DownloadHistorySettings.Enabled) {
            process.Start();
            return;
        }
        if (process.StartInfo.UseShellExecute) throw new HistoryException("Protected downloads require a directly managed downloader process.");
        var options = DownloadHistorySettings.Options(DownloadHistorySettings.Current, Downloads.fileNameSchema);
        string actualTemplate;
        string protectedArguments = HistoryCommandPolicy.Build(process.StartInfo.Arguments, options.LibraryPath, options.ArchivePath, out actualTemplate);
        options.Template = actualTemplate;
        CheckProvider(process.StartInfo.FileName);
        if (cancelled()) throw new OperationCanceledException("Protected download cancelled before launch.");
        process.StartInfo.Arguments = protectedArguments;
        notify("Download History: " + options.ArchivePath);
        notify("Protected mode saves source metadata, ignores external configs, and aborts incomplete fragments. Output quality/container selections are unchanged.");
        var guard = HistoryProcessGuard.Start(process, options, cancelled, notify);
        Guards.Add(process, guard);
        guard.Completion.ContinueWith(result => {
            if (result.IsFaulted) {
                string message = result.Exception.GetBaseException().Message;
                DownloadHistorySettings.RecordFailure(message);
                Log.Write("Download History postflight failed: " + message);
            }
            else DownloadHistorySettings.Record(result.Result);
        }, TaskScheduler.Default);
    }
    public static bool Complete(Process process, Action<string> notify) {
        HistoryProcessGuard guard;
        if (!Guards.TryGetValue(process, out guard)) return true;
        try {
            HistoryReport result = guard.Completion.GetAwaiter().GetResult();
            notify("Download History: " + result.State + "; " + result.Entries.Count + " source identities tracked.");
            return true;
        }
        catch (Exception ex) {
            DownloadHistorySettings.RecordFailure(ex.Message);
            notify("Download History could not validate the completed run: " + ex.Message);
            return false;
        }
        finally { Guards.Remove(process); }
    }
}

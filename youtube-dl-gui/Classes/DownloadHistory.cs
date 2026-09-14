#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

internal enum DownloadHistoryState {
    Disabled,
    Dormant,
    Healthy,
    Missing,
    Partial,
    Unsafe,
    Unavailable,
    Invalid
}

internal sealed class DownloadHistoryReport {
    public DownloadHistoryState State { get; set; }
    public int ArchiveEntries { get; set; }
    public int CompletedMedia { get; set; }
    public int IdentifiedMedia { get; set; }
    public int MetadataRecovered { get; set; }
    public int FilenameRecovered { get; set; }
    public int UnresolvedMedia { get; set; }
    public string Message { get; set; } = string.Empty;
}

internal sealed class DownloadHistoryLease : IDisposable {
    private Mutex? mutex;

    internal DownloadHistoryLease(string name) {
        mutex = new Mutex(false, name);
        try {
            mutex.WaitOne();
        }
        catch (AbandonedMutexException) {
            // The previous owner terminated without releasing the archive lease.
            // Ownership transfers to this process, so protected work can continue safely.
        }
    }

    public void Dispose() {
        Mutex? owned = Interlocked.Exchange(ref mutex, null);
        if (owned is null) return;
        try { owned.ReleaseMutex(); }
        finally { owned.Dispose(); }
    }
}

internal static class DownloadHistory {
    private const string ConfigName = "DownloadHistory";
    private static readonly object Sync = new();
    private static string? PreparedKey;
    private static DownloadHistoryReport LastReportInternal = new() { State = DownloadHistoryState.Disabled };

    private static bool fEnabled = IniProvider.Read(nameof(Enabled), false, ConfigName);
    private static string fArchivePath = IniProvider.Read(nameof(ArchivePath), string.Empty, ConfigName);
    private static bool fKeepBackup = IniProvider.Read(nameof(KeepBackup), true, ConfigName);
    private static bool fFailIfUnavailable = IniProvider.Read(nameof(FailIfUnavailable), true, ConfigName);
    private static bool fEverEnabled = IniProvider.Read(nameof(EverEnabled), false, ConfigName);
    private static bool fNeedsReconciliation = IniProvider.Read(nameof(NeedsReconciliation), false, ConfigName);

    public static bool Enabled {
        get => fEnabled;
        set {
            if (fEnabled == value) return;
            if (!value && (fEnabled || fEverEnabled)) {
                NeedsReconciliation = true;
            }
            fEnabled = value;
            IniProvider.Write(value, ConfigName, nameof(Enabled));
            if (value) {
                EverEnabled = true;
            }
            PreparedKey = null;
        }
    }

    public static string ArchivePath {
        get => fArchivePath;
        set {
            value ??= string.Empty;
            if (fArchivePath == value) return;
            fArchivePath = value;
            IniProvider.Write(value, ConfigName, nameof(ArchivePath));
            NeedsReconciliation = true;
            PreparedKey = null;
        }
    }

    public static bool KeepBackup {
        get => fKeepBackup;
        set {
            if (fKeepBackup == value) return;
            fKeepBackup = value;
            IniProvider.Write(value, ConfigName, nameof(KeepBackup));
        }
    }

    public static bool FailIfUnavailable {
        get => fFailIfUnavailable;
        set {
            if (fFailIfUnavailable == value) return;
            fFailIfUnavailable = value;
            IniProvider.Write(value, ConfigName, nameof(FailIfUnavailable));
        }
    }

    public static bool EverEnabled {
        get => fEverEnabled;
        private set {
            if (fEverEnabled == value) return;
            fEverEnabled = value;
            IniProvider.Write(value, ConfigName, nameof(EverEnabled));
        }
    }

    public static bool NeedsReconciliation {
        get => fNeedsReconciliation;
        private set {
            if (fNeedsReconciliation == value) return;
            fNeedsReconciliation = value;
            IniProvider.Write(value, ConfigName, nameof(NeedsReconciliation));
        }
    }

    public static DownloadHistoryReport LastReport => LastReportInternal;

    public static string DefaultArchivePath => Path.Combine(GetLibraryRoot(), "yt-dlp-archive.txt");

    public static string EffectiveArchivePath {
        get {
            if (!ArchivePath.IsNullEmptyWhitespace()) {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(ArchivePath));
            }
            return DefaultArchivePath;
        }
    }

    public static bool HasRequiredIdTemplate(string? schema) =>
        !schema.IsNullEmptyWhitespace() && schema!.IndexOf("%(id)s", StringComparison.OrdinalIgnoreCase) >= 0;

    public static string AddRequiredIdTemplate(string? schema) {
        string value = schema.IsNullEmptyWhitespace() ? "%(title)s.%(ext)s" : schema!;
        if (HasRequiredIdTemplate(value)) return value;
        const string ext = ".%(ext)s";
        int index = value.LastIndexOf(ext, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? value.Insert(index, "-%(id)s") : value + "-%(id)s.%(ext)s";
    }

    public static bool TryGetArchiveArguments(string fileNameSchema, string? customArguments, out string archiveArguments, out string error) {
        archiveArguments = string.Empty;
        error = string.Empty;
        if (!Enabled) {
            LastReportInternal = new DownloadHistoryReport {
                State = EverEnabled ? DownloadHistoryState.Dormant : DownloadHistoryState.Disabled,
                Message = EverEnabled ? "Download History is disabled. The preserved archive may be stale." : "Download History has never been enabled."
            };
            return true;
        }

        if (Downloads.YtdlType is not ((int)GitID.YtDlp) and not ((int)GitID.YtDlpNightly)) {
            LastReportInternal = new DownloadHistoryReport { State = DownloadHistoryState.Unsafe, Message = "Download History requires yt-dlp or yt-dlp nightly." };
            error = "Download History uses yt-dlp archive semantics and is not enabled for the selected youtube-dl provider. Select yt-dlp/yt-dlp-nightly or disable Download History.";
            return false;
        }

        if (!HasRequiredIdTemplate(fileNameSchema)) {
            LastReportInternal = new DownloadHistoryReport { State = DownloadHistoryState.Unsafe, Message = "The filename format does not contain %(id)s." };
            error = "Download History requires %(id)s in the filename format so the library can be validated or reconstructed.";
            return false;
        }

        if (ContainsOption(customArguments, "--download-archive")) {
            error = "Download History is enabled and owns --download-archive. Remove the custom --download-archive argument or disable Download History.";
            return false;
        }
        if (ContainsOption(customArguments, "--break-on-existing") || ContainsOption(customArguments, "--break-per-input")) {
            error = "--break-on-existing and --break-per-input are not allowed while Download History protection is enabled because they can stop collection traversal before new media is discovered.";
            return false;
        }

        if (!EnsureReady(out error)) return false;
        archiveArguments = $"--download-archive \"{EffectiveArchivePath}\" --no-break-on-existing";
        return true;
    }

    public static DownloadHistoryLease AcquireArchiveLease() => new(GetArchiveMutexName());

    public static DownloadHistoryReport ValidateAndReconcile(bool force) {
        lock (Sync) {
            string key = GetLibraryRoot() + "|" + EffectiveArchivePath;
            if (!force && !NeedsReconciliation && PreparedKey == key && LastReportInternal.State == DownloadHistoryState.Healthy) {
                return LastReportInternal;
            }
            using DownloadHistoryLease lease = AcquireArchiveLease();
            LastReportInternal = ReconcileCore();
            if (LastReportInternal.State == DownloadHistoryState.Healthy) {
                PreparedKey = key;
                NeedsReconciliation = false;
            }
            return LastReportInternal;
        }
    }

    private static bool EnsureReady(out string error) {
        DownloadHistoryReport report = ValidateAndReconcile(false);
        if (report.State == DownloadHistoryState.Healthy) {
            error = string.Empty;
            return true;
        }
        error = report.Message.IsNullEmptyWhitespace() ? "Download History is not in a safe state." : report.Message;
        return false;
    }

    private static DownloadHistoryReport ReconcileCore() {
        DownloadHistoryReport report = new();
        if (!Enabled) {
            report.State = EverEnabled ? DownloadHistoryState.Dormant : DownloadHistoryState.Disabled;
            report.Message = EverEnabled ? "Download History is disabled; archive preserved and potentially stale." : "Download History is disabled.";
            return report;
        }

        string libraryRoot;
        string archive;
        try {
            libraryRoot = GetLibraryRoot();
            archive = EffectiveArchivePath;
        }
        catch (Exception ex) {
            report.State = DownloadHistoryState.Invalid;
            report.Message = "Download History path is invalid: " + ex.Message;
            return report;
        }

        string? parent = Path.GetDirectoryName(archive);
        if (parent.IsNullEmptyWhitespace()) {
            report.State = DownloadHistoryState.Invalid;
            report.Message = "Download archive location does not have a valid parent directory.";
            return report;
        }
        string? root = Path.GetPathRoot(parent!);
        if (!root.IsNullEmptyWhitespace() && !Directory.Exists(root)) {
            report.State = DownloadHistoryState.Unavailable;
            report.Message = "Download archive storage is unavailable: " + root;
            return report;
        }

        try {
            if (!Directory.Exists(libraryRoot)) Directory.CreateDirectory(libraryRoot);
            if (!Directory.Exists(parent)) {
                if (!ArchivePath.IsNullEmptyWhitespace() && FailIfUnavailable) {
                    report.State = DownloadHistoryState.Unavailable;
                    report.Message = "Configured download archive directory is unavailable: " + parent;
                    return report;
                }
                Directory.CreateDirectory(parent!);
            }
        }
        catch (Exception ex) {
            report.State = DownloadHistoryState.Unavailable;
            report.Message = "Download archive storage cannot be accessed: " + ex.Message;
            return report;
        }

        HashSet<string> archiveEntries = new(StringComparer.Ordinal);
        bool archiveExists = File.Exists(archive);
        bool archiveNeedsRewrite = false;
        string backup = archive + ".bak";
        if (archiveExists) {
            if (!TryReadArchive(archive, archiveEntries, out string archiveError)) {
                archiveEntries.Clear();
                if (!TryReadArchive(backup, archiveEntries, out _)) {
                    archiveNeedsRewrite = true;
                    Log.Write("Download History archive is damaged and no valid backup is available: " + archiveError);
                }
                else {
                    archiveNeedsRewrite = true;
                    Log.Write("Download History recovered identities from the backup archive after the primary archive failed validation.");
                }
            }
        }
        else if (TryReadArchive(backup, archiveEntries, out _)) {
            archiveNeedsRewrite = true;
            Log.Write("Download History recovered identities from the backup archive after the primary archive was missing.");
        }

        List<string> mediaFiles;
        try {
            mediaFiles = EnumerateCompletedMedia(libraryRoot).ToList();
        }
        catch (Exception ex) {
            report.State = DownloadHistoryState.Unavailable;
            report.Message = "Media library cannot be scanned safely: " + ex.Message;
            return report;
        }

        report.CompletedMedia = mediaFiles.Count;
        HashSet<string> recovered = new(StringComparer.Ordinal);
        foreach (string media in mediaFiles) {
            string? entry = TryRecoverFromInfoJson(media);
            if (entry is not null) {
                report.MetadataRecovered++;
            }
            else {
                entry = TryRecoverYoutubeFromFilename(media);
                if (entry is not null) report.FilenameRecovered++;
            }

            if (entry is null) {
                report.UnresolvedMedia++;
                continue;
            }
            report.IdentifiedMedia++;
            recovered.Add(entry);
        }

        if (report.UnresolvedMedia > 0) {
            report.ArchiveEntries = archiveEntries.Count;
            report.State = report.IdentifiedMedia > 0 ? DownloadHistoryState.Partial : DownloadHistoryState.Unsafe;
            report.Message = $"Download History cannot safely enable protection: {report.UnresolvedMedia:N0} completed media file(s) have no authoritative recoverable source identity. No archive changes were written.";
            return report;
        }

        bool changed = archiveNeedsRewrite;
        foreach (string entry in recovered) {
            if (archiveEntries.Add(entry)) changed = true;
        }

        if (!archiveExists || changed) {
            try {
                WriteArchiveAtomically(archive, archiveEntries);
            }
            catch (Exception ex) {
                report.State = DownloadHistoryState.Unavailable;
                report.Message = "Download archive could not be written safely: " + ex.Message;
                return report;
            }
        }

        report.ArchiveEntries = archiveEntries.Count;
        report.State = DownloadHistoryState.Healthy;
        report.Message = archiveExists
            ? $"Download History is healthy. {report.ArchiveEntries:N0} archive entr{(report.ArchiveEntries == 1 ? "y" : "ies")} reconciled."
            : $"Download History archive created and validated with {report.ArchiveEntries:N0} entr{(report.ArchiveEntries == 1 ? "y" : "ies")}.";
        return report;
    }

    private static string GetArchiveMutexName() {
        string normalized = EffectiveArchivePath.ToUpperInvariant();
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return @"Local\youtube-dl-gui-download-history-" + string.Concat(digest.Take(12).Select(value => value.ToString("x2")));
    }

    private static string GetLibraryRoot() {
        string path = Downloads.downloadPath;
        if (path.StartsWith("./") || path.StartsWith(".\\")) {
            path = Path.Combine(Program.ProgramPath, path.Substring(2));
        }
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }

    private static IEnumerable<string> EnumerateCompletedMedia(string root) {
        if (!Directory.Exists(root)) yield break;
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
            string name = Path.GetFileName(file);
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".info.json", StringComparison.OrdinalIgnoreCase) ||
                ext is ".json" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".srt" or ".vtt" or ".ass" or ".lrc" or ".description" or ".txt") {
                continue;
            }
            if (ext is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi" or ".flv" or ".m4v" or ".mp3" or ".m4a" or ".aac" or ".opus" or ".ogg" or ".wav" or ".flac" or ".wma") {
                yield return file;
            }
        }
    }

    private static string? TryRecoverFromInfoJson(string mediaPath) {
        string directory = Path.GetDirectoryName(mediaPath) ?? string.Empty;
        string stem = Path.Combine(directory, Path.GetFileNameWithoutExtension(mediaPath));
        string[] candidates = [stem + ".info.json"];
        foreach (string candidate in candidates) {
            if (!File.Exists(candidate)) continue;
            try {
                string json = File.ReadAllText(candidate);
                Match id = Regex.Match(json, "\\\"id\\\"\\s*:\\s*\\\"(?<v>[^\\\"]+)\\\"", RegexOptions.CultureInvariant);
                Match extractor = Regex.Match(json, "\\\"(?:extractor_key|extractor)\\\"\\s*:\\s*\\\"(?<v>[^\\\"]+)\\\"", RegexOptions.CultureInvariant);
                if (!id.Success || !extractor.Success) continue;
                string ex = extractor.Groups["v"].Value.Trim().ToLowerInvariant();
                string sourceId = id.Groups["v"].Value.Trim();
                if (ex.Length == 0 || sourceId.Length == 0) continue;
                if (ex == "youtube") ex = "youtube";
                return ex + " " + sourceId;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    private static string? TryRecoverYoutubeFromFilename(string mediaPath) {
        string name = Path.GetFileNameWithoutExtension(mediaPath);
        Match match = Regex.Match(name, "-(?<id>[A-Za-z0-9_-]{11})(?:_[A-Za-z0-9_-]+)?$", RegexOptions.CultureInvariant);
        return match.Success ? "youtube " + match.Groups["id"].Value : null;
    }

    private static bool TryReadArchive(string path, HashSet<string> entries, out string error) {
        error = string.Empty;
        if (!File.Exists(path)) return false;
        try {
            foreach (string line in File.ReadAllLines(path)) {
                string entry = line.Trim();
                if (entry.Length == 0) continue;
                int separator = entry.IndexOf(' ');
                if (separator <= 0 || separator == entry.Length - 1 || entry.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) {
                    error = "Invalid archive entry: " + entry;
                    return false;
                }
                entries.Add(entry);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            error = ex.Message;
            return false;
        }
    }

    private static void WriteArchiveAtomically(string path, HashSet<string> entries) {
        string temp = path + ".tmp";
        string backup = path + ".bak";
        string content = string.Join(Environment.NewLine, entries.OrderBy(x => x, StringComparer.Ordinal));
        if (content.Length > 0) content += Environment.NewLine;
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        try {
            if (File.Exists(path)) {
                File.Replace(temp, path, KeepBackup ? backup : null, true);
            }
            else {
                File.Move(temp, path);
            }
        }
        finally {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool ContainsOption(string? arguments, string option) {
        if (arguments.IsNullEmptyWhitespace()) return false;
        return Regex.IsMatch(arguments!, "(^|\\s)" + Regex.Escape(option) + "(?:=|\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

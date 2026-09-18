#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

internal enum DownloadHistoryState {
    Disabled,
    Dormant,
    Healthy,
    Migratable,
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
    public int MigrationCount { get; set; }
    public bool CanReconcile { get; set; }
    public string Message { get; set; } = string.Empty;
    internal string ArchiveDigest { get; set; } = string.Empty;
}

internal sealed class DownloadHistoryLease : IDisposable {
    private Mutex? mutex;
    private FileStream? fileLock;
    private string? fileLockPath;

    private DownloadHistoryLease(Mutex ownedMutex) {
        mutex = ownedMutex;
    }

    internal DownloadHistoryLease(string name, Func<bool>? cancellationRequested = null) {
        Mutex candidate = new(false, name);
        try {
            while (true) {
                if (cancellationRequested?.Invoke() == true) throw new OperationCanceledException("Download History lease wait was cancelled.");
                try {
                    if (candidate.WaitOne(100)) break;
                }
                catch (AbandonedMutexException) {
                    // The previous owner terminated without releasing the archive lease.
                    // Ownership transfers to this process, so protected work can continue safely.
                    break;
                }
            }
            mutex = candidate;
        }
        catch {
            candidate.Dispose();
            throw;
        }
    }

    internal static bool TryAcquire(string name, out DownloadHistoryLease? lease) {
        Mutex candidate = new(false, name);
        bool acquired;
        try { acquired = candidate.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) {
            candidate.Dispose();
            lease = null;
            return false;
        }
        lease = new DownloadHistoryLease(candidate);
        return true;
    }

    internal void AcquireFileLock(string path, Func<bool>? cancellationRequested = null) {
        string? parent = Path.GetDirectoryName(path);
        while (true) {
            if (cancellationRequested?.Invoke() == true) throw new OperationCanceledException("Download History file-lock wait was cancelled.");
            try {
                fileLock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                fileLockPath = path;
                return;
            }
            catch (IOException ex) {
                int nativeError = ex.HResult & 0xFFFF;
                if (nativeError is not 32 and not 33 || parent.IsNullEmptyWhitespace() || !Directory.Exists(parent)) throw;
                if (cancellationRequested?.Invoke() == true) throw new OperationCanceledException("Download History file-lock wait was cancelled.");
                Thread.Sleep(100);
            }
        }
    }

    internal bool TryAcquireFileLock(string path) {
        try {
            fileLock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            fileLockPath = path;
            return true;
        }
        catch (IOException ex) {
            int nativeError = ex.HResult & 0xFFFF;
            if (nativeError is not 32 and not 33) throw;
            return false;
        }
    }

    public void Dispose() {
        FileStream? lockFile = Interlocked.Exchange(ref fileLock, null);
        string? lockPath = Interlocked.Exchange(ref fileLockPath, null);
        lockFile?.Dispose();
        if (lockPath is not null) {
            try { File.Delete(lockPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Mutex? owned = Interlocked.Exchange(ref mutex, null);
        if (owned is null) return;
        try { owned.ReleaseMutex(); }
        finally { owned.Dispose(); }
    }
}

internal sealed class DownloadHistoryExecution {
    internal string ArchivePath { get; }
    internal bool KeepBackup { get; }

    internal DownloadHistoryExecution(string archivePath, bool keepBackup) {
        ArchivePath = archivePath;
        KeepBackup = keepBackup;
    }

    public DownloadHistoryLease AcquireValidatedLease() => DownloadHistory.AcquireValidatedExecutionLease(ArchivePath, KeepBackup, null);

    public DownloadHistoryLease AcquireValidatedLease(Func<bool> cancellationRequested) =>
        DownloadHistory.AcquireValidatedExecutionLease(ArchivePath, KeepBackup, cancellationRequested);


    // This overload deliberately assumes the execution lease is still held by the caller.
    public void RefreshBackupAfterRun() => DownloadHistory.RefreshBackupAfterRun(ArchivePath, KeepBackup);
}

internal sealed class DownloadHistorySettingsSnapshot {
    internal bool Enabled { get; init; }
    internal string ArchivePath { get; init; } = string.Empty;
    internal bool KeepBackup { get; init; }
    internal bool FailIfUnavailable { get; init; }
    internal bool EverEnabled { get; init; }
    internal bool NeedsReconciliation { get; init; }
    internal string BoundLibraryRoot { get; init; } = string.Empty;
    internal string BoundArchivePath { get; init; } = string.Empty;
    internal string InventoryRoots { get; init; } = string.Empty;
    internal string? PreparedKey { get; init; }
    internal DownloadHistoryReport LastReport { get; init; } = new();
}

internal sealed class DownloadHistoryAnalysis {
    internal DownloadHistoryReport Report { get; } = new();
    internal string LibraryRoot { get; init; } = string.Empty;
    internal string ArchivePath { get; init; } = string.Empty;
    internal bool ArchiveExists { get; set; }
    internal bool ArchiveValid { get; set; }
    internal bool ArchiveNeedsRewrite { get; set; }
    internal bool ArchiveWasInvalid { get; set; }
    internal bool UsedBackup { get; set; }
    internal bool RecoverMissingEntries { get; set; }
    internal HashSet<string> ArchiveEntries { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> RecoveredEntries { get; } = new(StringComparer.Ordinal);
}

internal static class DownloadHistory {
    private const string ConfigName = "DownloadHistory";
    private static readonly object Sync = new();
    private static string? PreparedKey;
    private static DownloadHistoryReport LastReportInternal = new() { State = DownloadHistoryState.Disabled };

    private static bool fEnabled = IniProvider.Read(false, false, ConfigName, nameof(Enabled));
    private static string fArchivePath = IniProvider.Read(string.Empty, string.Empty, ConfigName, nameof(ArchivePath));
    private static bool fKeepBackup = IniProvider.Read(false, true, ConfigName, nameof(KeepBackup));
    private static bool fFailIfUnavailable = IniProvider.Read(false, true, ConfigName, nameof(FailIfUnavailable));
    private static bool fEverEnabled = IniProvider.Read(false, false, ConfigName, nameof(EverEnabled));
    private static bool fNeedsReconciliation = IniProvider.Read(false, false, ConfigName, nameof(NeedsReconciliation));
    private static string fBoundLibraryRoot = IniProvider.Read(string.Empty, string.Empty, ConfigName, nameof(BoundLibraryRoot));
    private static string fBoundArchivePath = IniProvider.Read(string.Empty, string.Empty, ConfigName, nameof(BoundArchivePath));
    private static string fInventoryRoots = IniProvider.Read(string.Empty, string.Empty, ConfigName, nameof(InventoryRoots));
    private static string fKnownFileNameSchemas = IniProvider.Read(string.Empty, string.Empty, ConfigName, "KnownFileNameSchemas");

    private sealed class FileNameIdentityMatcher {
        private sealed class Candidate {
            internal string Value { get; }
            internal string[] Entries { get; }

            internal Candidate(string value, IEnumerable<string> entries) {
                Value = value;
                Entries = entries.OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
            }
        }

        private sealed class Node {
            internal Dictionary<char, Node> Next { get; } = new();
            internal Node? Failure { get; set; }
            internal List<Candidate> Outputs { get; } = [];
        }

        private readonly Node root = new();

        internal FileNameIdentityMatcher(IEnumerable<string> archiveEntries) {
            Dictionary<string, HashSet<string>> candidates = new(StringComparer.Ordinal);
            foreach (string entry in archiveEntries) {
                int separator = entry.IndexOf(' ');
                if (separator <= 0 || separator == entry.Length - 1) continue;
                string sourceId = entry.Substring(separator + 1);
                foreach (string value in DownloadHistory.SourceIdFileNameCandidates(sourceId)) {
                    if (!candidates.TryGetValue(value, out HashSet<string>? matches)) {
                        matches = new HashSet<string>(StringComparer.Ordinal);
                        candidates.Add(value, matches);
                    }
                    matches.Add(entry);
                }
            }

            foreach (KeyValuePair<string, HashSet<string>> item in candidates) Add(item.Key, item.Value);
            BuildFailureLinks();
        }

        private void Add(string value, IEnumerable<string> entries) {
            Node node = root;
            foreach (char character in value) {
                if (!node.Next.TryGetValue(character, out Node? next)) {
                    next = new Node();
                    node.Next.Add(character, next);
                }
                node = next;
            }
            node.Outputs.Add(new Candidate(value, entries));
        }

        private void BuildFailureLinks() {
            root.Failure = root;
            Queue<Node> pending = new();
            foreach (Node child in root.Next.Values) {
                child.Failure = root;
                pending.Enqueue(child);
            }

            while (pending.Count > 0) {
                Node current = pending.Dequeue();
                foreach (KeyValuePair<char, Node> edge in current.Next) {
                    Node fallback = current.Failure ?? root;
                    while (!ReferenceEquals(fallback, root) && !fallback.Next.ContainsKey(edge.Key)) {
                        fallback = fallback.Failure ?? root;
                    }

                    if (fallback.Next.TryGetValue(edge.Key, out Node? nextFallback) && !ReferenceEquals(nextFallback, edge.Value)) {
                        edge.Value.Failure = nextFallback;
                    }
                    else {
                        edge.Value.Failure = root;
                    }

                    if (edge.Value.Failure.Outputs.Count > 0) edge.Value.Outputs.AddRange(edge.Value.Failure.Outputs);
                    pending.Enqueue(edge.Value);
                }
            }
        }

        internal string? Match(string mediaPath) {
            string fileName = Path.GetFileName(mediaPath);
            Node node = root;
            string? match = null;
            HashSet<string> evaluated = new(StringComparer.Ordinal);

            foreach (char character in fileName) {
                while (!ReferenceEquals(node, root) && !node.Next.ContainsKey(character)) {
                    node = node.Failure ?? root;
                }

                if (node.Next.TryGetValue(character, out Node? next)) node = next;
                else node = root;

                foreach (Candidate candidate in node.Outputs) {
                    if (!evaluated.Add(candidate.Value) || !DownloadHistory.FileNameMatchesSourceId(mediaPath, candidate.Value)) continue;
                    foreach (string entry in candidate.Entries) {
                        if (match is not null && !string.Equals(match, entry, StringComparison.Ordinal)) return null;
                        match = entry;
                    }
                }
            }
            return match;
        }
    }

    public static bool Enabled {
        get => fEnabled;
        set {
            if (fEnabled == value) return;
            if (!value && (fEnabled || fEverEnabled)) {
                NeedsReconciliation = true;
            }
            fEnabled = value;
            IniProvider.Write(value, ConfigName, nameof(Enabled));
            if (value) EverEnabled = true;
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

    public static string BoundLibraryRoot {
        get => fBoundLibraryRoot;
        private set {
            value ??= string.Empty;
            if (string.Equals(fBoundLibraryRoot, value, StringComparison.Ordinal)) return;
            fBoundLibraryRoot = value;
            IniProvider.Write(value, ConfigName, nameof(BoundLibraryRoot));
        }
    }

    public static string BoundArchivePath {
        get => fBoundArchivePath;
        private set {
            value ??= string.Empty;
            if (string.Equals(fBoundArchivePath, value, StringComparison.Ordinal)) return;
            fBoundArchivePath = value;
            IniProvider.Write(value, ConfigName, nameof(BoundArchivePath));
        }
    }

    public static string InventoryRoots => fInventoryRoots;

    public static DownloadHistoryReport LastReport => LastReportInternal;

    public static DownloadHistorySettingsSnapshot CaptureSettings() => new() {
        Enabled = fEnabled,
        ArchivePath = fArchivePath,
        KeepBackup = fKeepBackup,
        FailIfUnavailable = fFailIfUnavailable,
        EverEnabled = fEverEnabled,
        NeedsReconciliation = fNeedsReconciliation,
        BoundLibraryRoot = fBoundLibraryRoot,
        BoundArchivePath = fBoundArchivePath,
        InventoryRoots = fInventoryRoots,
        PreparedKey = PreparedKey,
        LastReport = LastReportInternal
    };

    public static void RestoreSettings(DownloadHistorySettingsSnapshot snapshot) {
        fEnabled = snapshot.Enabled;
        fArchivePath = snapshot.ArchivePath;
        fKeepBackup = snapshot.KeepBackup;
        fFailIfUnavailable = snapshot.FailIfUnavailable;
        fEverEnabled = snapshot.EverEnabled;
        fNeedsReconciliation = snapshot.NeedsReconciliation;
        fBoundLibraryRoot = snapshot.BoundLibraryRoot;
        fBoundArchivePath = snapshot.BoundArchivePath;
        fInventoryRoots = snapshot.InventoryRoots;
        PreparedKey = snapshot.PreparedKey;
        LastReportInternal = snapshot.LastReport;
        IniProvider.Write(fEnabled, ConfigName, nameof(Enabled));
        IniProvider.Write(fArchivePath, ConfigName, nameof(ArchivePath));
        IniProvider.Write(fKeepBackup, ConfigName, nameof(KeepBackup));
        IniProvider.Write(fFailIfUnavailable, ConfigName, nameof(FailIfUnavailable));
        IniProvider.Write(fEverEnabled, ConfigName, nameof(EverEnabled));
        IniProvider.Write(fNeedsReconciliation, ConfigName, nameof(NeedsReconciliation));
        IniProvider.Write(fBoundLibraryRoot, ConfigName, nameof(BoundLibraryRoot));
        IniProvider.Write(fBoundArchivePath, ConfigName, nameof(BoundArchivePath));
        IniProvider.Write(fInventoryRoots, ConfigName, nameof(InventoryRoots));
    }

    public static void ResetHistory() {
        if (Enabled) throw new InvalidOperationException("Disable Download History before resetting it.");
        string archive = EffectiveArchivePath;
        string? parent = Path.GetDirectoryName(archive);
        if (parent.IsNullEmptyWhitespace() || !Directory.Exists(parent)) {
            throw new InvalidOperationException("Download History cannot be reset because the archive storage is unavailable.");
        }

        using DownloadHistoryLease lease = AcquireManagementLease(archive, "reset");
        foreach (string path in new[] { archive, archive + ".bak", archive + ".tmp", archive + ".bak.tmp" }) {
            if (File.Exists(path)) File.Delete(path);
        }
        EverEnabled = false;
        BoundLibraryRoot = string.Empty;
        BoundArchivePath = string.Empty;
        NeedsReconciliation = true;
        PreparedKey = null;
        LastReportInternal = new DownloadHistoryReport {
            State = DownloadHistoryState.Dormant,
            CanReconcile = true,
            Message = "Download History was reset. Existing media must be reconciled before protection can be enabled again."
        };
    }

    public static void RefreshBackupAfterRun() {
        if (!Enabled) return;
        string archive = EffectiveArchivePath;
        using DownloadHistoryLease lease = AcquireArchiveLease(archive);
        RefreshBackupAfterRun(archive, KeepBackup);
    }

    internal static void RefreshBackupAfterRun(string archive, bool keepBackup) {
        if (!keepBackup) return;
        try {
            HashSet<string> entries = new(StringComparer.Ordinal);
            if (!TryReadArchive(archive, entries, out string error)) {
                if (!error.IsNullEmptyWhitespace()) {
                    Log.Write("Download History did not refresh its backup because the primary archive failed validation: " + error);
                }
                PreparedKey = null;
                return;
            }
            HashSet<string> backupEntries = new(StringComparer.Ordinal);
            if (TryReadArchive(archive + ".bak", backupEntries, out _) && backupEntries.Any(entry => !entries.Contains(entry))) {
                PreparedKey = null;
                Log.Write("Download History preserved its previous backup because the primary archive lost existing entries. The next protected operation will reconcile the ledger before downloading.");
                return;
            }
            CopyArchiveToBackupAtomically(archive);
        }
        catch (Exception ex) {
            PreparedKey = null;
            Log.Write("Download History could not refresh its backup after the provider exited: " + ex.Message);
        }
    }

    public static bool IsCurrentLibraryPath(string? candidateDownloadPath) {
        try {
            string candidate = candidateDownloadPath.IsNullEmptyWhitespace() ? Downloads.DefaultDownloadPath : candidateDownloadPath!;
            return PathEquals(GetLibraryRoot(), ResolveLibraryRoot(candidate));
        }
        catch { return false; }
    }

    public static string DefaultArchivePath => Path.Combine(GetLibraryRoot(), "yt-dlp-archive.txt");

    public static string EffectiveArchivePath {
        get {
            if (!ArchivePath.IsNullEmptyWhitespace()) {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(ArchivePath));
            }
            if (EverEnabled && !BoundArchivePath.IsNullEmptyWhitespace()) {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(BoundArchivePath));
            }
            return DefaultArchivePath;
        }
    }

    public static bool HasRequiredIdTemplate(string? schema) {
        if (schema.IsNullEmptyWhitespace()) return false;
        string fileTemplate = GetSchemaFileTemplate(schema!);
        return !fileTemplate.IsNullEmptyWhitespace() && fileTemplate.IndexOf("%(id)s", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string AddRequiredIdTemplate(string? schema) {
        string value = schema.IsNullEmptyWhitespace() ? "%(title)s.%(ext)s" : schema!;
        if (HasRequiredIdTemplate(value)) return value;
        const string ext = ".%(ext)s";
        int index = value.LastIndexOf(ext, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? value.Insert(index, "-%(id)s") : value + "-%(id)s.%(ext)s";
    }

    public static bool TryGetArchiveArguments(string fileNameSchema, string? customArguments, out string archiveArguments, out string error) =>
        TryGetArchiveArguments(fileNameSchema, customArguments, out archiveArguments, out error, out _);

    public static bool TryGetArchiveArguments(string fileNameSchema, string? customArguments, out string archiveArguments, out string error, out DownloadHistoryExecution? execution) {
        lock (Sync) {
            archiveArguments = string.Empty;
            error = string.Empty;
            execution = null;
            if (!Enabled) {
                LastReportInternal = DisabledReport();
                return true;
            }

            if (Downloads.YtdlType is not ((int)GitID.YtDlp) and not ((int)GitID.YtDlpNightly)) {
                LastReportInternal = new DownloadHistoryReport { State = DownloadHistoryState.Unsafe, Message = "Download History requires yt-dlp or yt-dlp nightly." };
                error = "Download History uses yt-dlp archive semantics and is not enabled for the selected youtube-dl provider. Select yt-dlp/yt-dlp-nightly or disable Download History.";
                return false;
            }

            if (!HasRequiredIdTemplate(fileNameSchema)) {
                LastReportInternal = new DownloadHistoryReport { State = DownloadHistoryState.Unsafe, Message = "The filename format does not contain %(id)s in the output filename." };
                error = "Download History requires %(id)s in the output filename so the library can be validated or reconstructed.";
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
            if (ContainsShortOption(customArguments, "-o") || ContainsOption(customArguments, "--output") || ContainsOption(customArguments, "--id") ||
                ContainsShortOption(customArguments, "-P") || ContainsOption(customArguments, "--paths")) {
                error = "Custom output templates or paths are not allowed while Download History protection is enabled because the app must keep %(id)s-bearing media inside the validated library namespace.";
                return false;
            }
            if (ContainsOption(customArguments, "--parse-metadata") || ContainsOption(customArguments, "--replace-in-metadata") ||
                ContainsOption(customArguments, "--metadata-from-title")) {
                error = "Custom metadata rewriting is not allowed while Download History protection is enabled because changing source identity fields can corrupt archive identity. Remove the metadata rewrite or disable Download History.";
                return false;
            }
            if (ContainsOption(customArguments, "--force-write-archive") ||
                ContainsOption(customArguments, "--force-write-download-archive") ||
                ContainsOption(customArguments, "--force-download-archive")) {
                error = "Forced archive writes are not allowed while Download History protection is enabled because simulated or skipped downloads must not be recorded as completed media.";
                return false;
            }
            if (ContainsOption(customArguments, "--no-part") || ContainsOption(customArguments, "--trim-filenames") || ContainsOption(customArguments, "--trim-file-names")) {
                error = "--no-part and --trim-filenames/--trim-file-names are not allowed while Download History protection is enabled because incomplete or truncated filenames can defeat safe ID-based recovery.";
                return false;
            }
            if (ContainsOption(customArguments, "--plugin-dirs")) {
                error = "Custom yt-dlp plugin directories are not allowed while Download History protection is enabled because plugins can replace extractors or mutate protected state. Disable Download History if plugins are required.";
                return false;
            }
            if (ContainsOption(customArguments, "--config-locations") || ContainsOption(customArguments, "--alias")) {
                error = "Custom yt-dlp config locations and runtime aliases are not allowed while Download History protection is enabled because they can inject options that bypass archive and filename integrity checks. Move compatible options into the app's custom arguments instead.";
                return false;
            }
            if (ContainsOption(customArguments, "--exec") || ContainsOption(customArguments, "--exec-before-download") ||
                ContainsOption(customArguments, "--use-postprocessor")) {
                error = "Arbitrary exec and plugin postprocessor hooks are not allowed while Download History protection is enabled because they can change protected identities or files outside the validated postprocessing model. Use built-in yt-dlp postprocessing options or disable Download History.";
                return false;
            }
            if (ContainsOption(customArguments, "--")) {
                error = "A standalone -- option terminator is not allowed while Download History protection is enabled because it would turn the app's native archive protection arguments into positional inputs. Remove the terminator or disable Download History.";
                return false;
            }

            if (!EnsureReady(out error)) return false;
            if (!RememberFileNameSchema(fileNameSchema, out error)) return false;
            string preparedArchive = EffectiveArchivePath;
            execution = new DownloadHistoryExecution(preparedArchive, KeepBackup);
            archiveArguments = $"--ignore-config --no-plugin-dirs --download-archive \"{preparedArchive}\" --no-break-on-existing";
            return true;
        }
    }

    public static DownloadHistoryLease AcquireArchiveLease() => AcquireArchiveLease(EffectiveArchivePath);

    private static DownloadHistoryLease AcquireArchiveLease(string archivePath, Func<bool>? cancellationRequested = null) {
        DownloadHistoryLease lease = new(GetArchiveMutexName(archivePath), cancellationRequested);
        try {
            string? parent = Path.GetDirectoryName(archivePath);
            if (!parent.IsNullEmptyWhitespace() && Directory.Exists(parent)) {
                lease.AcquireFileLock(archivePath + ".lock", cancellationRequested);
            }
            return lease;
        }
        catch {
            lease.Dispose();
            throw;
        }
    }

    private static bool TryAcquireArchiveLease(string archivePath, out DownloadHistoryLease? lease) {
        lease = null;
        if (!DownloadHistoryLease.TryAcquire(GetArchiveMutexName(archivePath), out DownloadHistoryLease? candidate)) return false;
        try {
            string? parent = Path.GetDirectoryName(archivePath);
            if (!parent.IsNullEmptyWhitespace() && Directory.Exists(parent) && !candidate!.TryAcquireFileLock(archivePath + ".lock")) {
                candidate.Dispose();
                return false;
            }
            lease = candidate;
            return true;
        }
        catch {
            candidate?.Dispose();
            throw;
        }
    }

    private static DownloadHistoryLease AcquireManagementLease(string archivePath, string operation) {
        if (TryAcquireArchiveLease(archivePath, out DownloadHistoryLease? lease)) return lease!;
        throw new InvalidOperationException("Download History cannot " + operation + " while another protected download or history operation is using this archive. Try again after the active operation finishes.");
    }

    private static DownloadHistoryReport BusyReport(string operation) => new() {
        State = DownloadHistoryState.Unavailable,
        CanReconcile = true,
        Message = "Download History cannot " + operation + " while another protected download or history operation is using this archive. Try again after the active operation finishes."
    };

    private static void ValidatePreparedExecution(string archivePath, bool keepBackup) {
        if (!fEnabled) {
            throw new InvalidOperationException("Download History was disabled after this download command was prepared. Regenerate the command before starting the download.");
        }
        if (!PathEquals(archivePath, EffectiveArchivePath) ||
            (!BoundArchivePath.IsNullEmptyWhitespace() && !PathEquals(archivePath, BoundArchivePath)) ||
            fKeepBackup != keepBackup) {
            throw new InvalidOperationException("Download History settings changed after this download command was prepared. Regenerate the command before starting the download.");
        }
    }

    internal static DownloadHistoryLease AcquireValidatedExecutionLease(string archivePath, bool keepBackup, Func<bool>? cancellationRequested = null) {
        lock (Sync) ValidatePreparedExecution(archivePath, keepBackup);
        DownloadHistoryLease lease = AcquireArchiveLease(archivePath, cancellationRequested);
        try {
            lock (Sync) ValidatePreparedExecution(archivePath, keepBackup);
            if (!File.Exists(archivePath)) {
                throw new InvalidOperationException("The prepared Download History archive is no longer available. Regenerate the download command after validating Download History.");
            }
            HashSet<string> entries = new(StringComparer.Ordinal);
            if (!TryReadArchive(archivePath, entries, out string error)) {
                throw new InvalidOperationException("The prepared Download History archive is no longer valid: " + error);
            }
            if (keepBackup) {
                string backupPath = archivePath + ".bak";
                HashSet<string> backupEntries = new(StringComparer.Ordinal);
                if (!TryReadArchive(backupPath, backupEntries, out string backupError)) {
                    lock (Sync) {
                        PreparedKey = null;
                        NeedsReconciliation = true;
                    }
                    throw new InvalidOperationException("The prepared Download History backup is no longer available or valid. Regenerate the download command after validating Download History." + (backupError.IsNullEmptyWhitespace() ? string.Empty : " " + backupError));
                }
                if (backupEntries.Any(entry => !entries.Contains(entry))) {
                    lock (Sync) PreparedKey = null;
                    throw new InvalidOperationException("The prepared Download History archive lost entries that remain in its last-good backup. Regenerate the download command so the ledger can be reconciled before downloading.");
                }
            }
            if (!CanWriteArchiveLocation(archivePath, out string writeError)) {
                throw new InvalidOperationException("The prepared Download History archive is no longer writable: " + writeError);
            }
            return lease;
        }
        catch {
            lease.Dispose();
            throw;
        }
    }

    public static DownloadHistoryReport AnalyzeLibrary() {
        if (!Enabled) {
            LastReportInternal = DisabledReport();
            return LastReportInternal;
        }
        DownloadHistoryReport report = AnalyzeLibrary(ArchivePath, fInventoryRoots);
        LastReportInternal = report;
        return report;
    }

    public static DownloadHistoryReport AnalyzeLibrary(string configuredArchivePath) => AnalyzeLibrary(configuredArchivePath, fInventoryRoots);

    public static DownloadHistoryReport AnalyzeLibrary(string configuredArchivePath, string configuredInventoryRoots) {
        lock (Sync) {
            if (!TryResolvePaths(configuredArchivePath, out string libraryRoot, out string archive, out DownloadHistoryReport? pathError)) {
                return pathError!;
            }
            if (!TryResolveInventoryRoots(libraryRoot, configuredInventoryRoots, out List<string> scanRoots, out DownloadHistoryReport? rootError)) return rootError!;
            if (!TryAcquireArchiveLease(archive, out DownloadHistoryLease? lease)) return BusyReport("validate the library");
            bool recoverMissingEntries = CandidateRequiresLibraryRecovery(libraryRoot, archive) || InventoryRootsChanged(configuredInventoryRoots);
            using (lease!) return AnalyzeCore(libraryRoot, scanRoots, archive, recoverMissingEntries).Report;
        }
    }

    public static DownloadHistoryReport ReconcileLibrary(string configuredArchivePath, bool keepBackup, bool allowMigration) =>
        ReconcileLibrary(configuredArchivePath, keepBackup, allowMigration, fInventoryRoots);

    public static DownloadHistoryReport ReconcileLibrary(string configuredArchivePath, bool keepBackup, bool allowMigration, string configuredInventoryRoots) {
        lock (Sync) {
            if (!TryResolvePaths(configuredArchivePath, out string libraryRoot, out string archive, out DownloadHistoryReport? pathError)) {
                return pathError!;
            }
            if (!TryResolveInventoryRoots(libraryRoot, configuredInventoryRoots, out List<string> scanRoots, out DownloadHistoryReport? rootError)) return rootError!;
            if (!TryAcquireArchiveLease(archive, out DownloadHistoryLease? lease)) return BusyReport("reconcile the library");
            using (lease!) {
                if (!TryInitializeNewDefaultLibrary(libraryRoot, archive, lease!, out DownloadHistoryReport? initializeError)) return initializeError!;
                bool recoverMissingEntries = CandidateRequiresLibraryRecovery(libraryRoot, archive) || InventoryRootsChanged(configuredInventoryRoots);
                DownloadHistoryAnalysis analysis = AnalyzeCore(libraryRoot, scanRoots, archive, recoverMissingEntries);
                return ReconcileAnalysis(analysis, keepBackup, allowMigration);
            }
        }
    }

    public static DownloadHistoryReport RebuildLibrary(string configuredArchivePath, bool keepBackup, bool allowMigration) =>
        RebuildLibrary(configuredArchivePath, keepBackup, allowMigration, fInventoryRoots);

    public static DownloadHistoryReport RebuildLibrary(string configuredArchivePath, bool keepBackup, bool allowMigration, string configuredInventoryRoots) {
        lock (Sync) {
            if (!TryResolvePaths(configuredArchivePath, out string libraryRoot, out string archive, out DownloadHistoryReport? pathError)) {
                return pathError!;
            }
            if (!TryResolveInventoryRoots(libraryRoot, configuredInventoryRoots, out List<string> scanRoots, out DownloadHistoryReport? rootError)) return rootError!;
            if (!TryAcquireArchiveLease(archive, out DownloadHistoryLease? lease)) return BusyReport("rebuild the library");
            using (lease!) {
                if (!TryInitializeNewDefaultLibrary(libraryRoot, archive, lease!, out DownloadHistoryReport? initializeError)) return initializeError!;
                // Rebuild is an explicit inventory operation: recover every authoritative identity
                // visible in the physical library while preserving all valid existing archive entries.
                DownloadHistoryAnalysis analysis = AnalyzeCore(libraryRoot, scanRoots, archive, true);
                return ReconcileAnalysis(analysis, keepBackup, allowMigration);
            }
        }
    }

    private static void PersistSettingsFailClosed(bool enabled, string archivePath, bool keepBackup, bool failIfUnavailable,
        bool everEnabled, bool needsReconciliation, string boundLibraryRoot, string boundArchivePath, string inventoryRoots) {
        IniProvider.Write(false, ConfigName, nameof(Enabled));
        IniProvider.Write(archivePath, ConfigName, nameof(ArchivePath));
        IniProvider.Write(keepBackup, ConfigName, nameof(KeepBackup));
        IniProvider.Write(failIfUnavailable, ConfigName, nameof(FailIfUnavailable));
        IniProvider.Write(everEnabled, ConfigName, nameof(EverEnabled));
        IniProvider.Write(needsReconciliation, ConfigName, nameof(NeedsReconciliation));
        IniProvider.Write(boundLibraryRoot, ConfigName, nameof(BoundLibraryRoot));
        IniProvider.Write(boundArchivePath, ConfigName, nameof(BoundArchivePath));
        IniProvider.Write(inventoryRoots, ConfigName, nameof(InventoryRoots));
        IniProvider.Write(enabled, ConfigName, nameof(Enabled));
    }

    public static void CommitSettings(bool enabled, string configuredArchivePath, bool keepBackup, DownloadHistoryReport? preparedReport) =>
        CommitSettings(enabled, configuredArchivePath, keepBackup, preparedReport, fInventoryRoots);

    public static void CommitSettings(bool enabled, string configuredArchivePath, bool keepBackup, DownloadHistoryReport? preparedReport, string configuredInventoryRoots) {
        lock (Sync) {
            configuredArchivePath ??= string.Empty;
            string normalizedInventoryRoots;
            try { normalizedInventoryRoots = NormalizeInventoryRootsForStorage(configuredInventoryRoots); }
            catch (Exception ex) { throw new InvalidOperationException("Download History inventory path is invalid: " + ex.Message, ex); }
            string? preparedKey = null;
            if (enabled) {
                if (Downloads.YtdlType is not ((int)GitID.YtDlp) and not ((int)GitID.YtDlpNightly)) {
                    throw new InvalidOperationException("Download History requires yt-dlp or yt-dlp nightly.");
                }
                if (!HasRequiredIdTemplate(Downloads.fileNameSchema)) {
                    throw new InvalidOperationException("Download History cannot be enabled until the output filename contains %(id)s.");
                }
                if (preparedReport?.State != DownloadHistoryState.Healthy) {
                    throw new InvalidOperationException("Download History settings cannot be enabled until the candidate library state is healthy.");
                }
                if (!TryResolvePaths(configuredArchivePath, out _, out string preparedArchive, out DownloadHistoryReport? pathError)) {
                    throw new InvalidOperationException(pathError?.Message ?? "Download History path is invalid.");
                }

                using DownloadHistoryLease? previousLease = ShouldWaitForPreviousArchive(preparedArchive) ? AcquireManagementLease(BoundArchivePath, "change archive settings") : null;
                using DownloadHistoryLease lease = AcquireManagementLease(preparedArchive, "save settings");
                HashSet<string> entries = new(StringComparer.Ordinal);
                if (!TryReadArchive(preparedArchive, entries, out string archiveError)) {
                    throw new InvalidOperationException("Download History settings were not saved because the prepared archive is no longer valid: " + archiveError);
                }
                if (!CanWriteArchiveLocation(preparedArchive, out string writeError)) {
                    throw new InvalidOperationException("Download History settings were not saved because the prepared archive is no longer writable: " + writeError);
                }
                string preparedDigest = preparedReport!.ArchiveDigest;
                string currentDigest = ComputeArchiveDigest(entries);
                if (preparedDigest.IsNullEmptyWhitespace() || !string.Equals(preparedDigest, currentDigest, StringComparison.Ordinal)) {
                    throw new InvalidOperationException("Download History settings were not saved because the prepared archive changed after reconciliation. Re-run validation/reconciliation and try again.");
                }
                if (keepBackup) CopyArchiveToBackupAtomically(preparedArchive);
                preparedKey = preparedArchive;
            }

            bool oldEnabled = fEnabled;
            string oldArchivePath = fArchivePath;
            bool oldKeepBackup = fKeepBackup;
            bool oldFailIfUnavailable = fFailIfUnavailable;
            bool oldEverEnabled = fEverEnabled;
            bool oldNeedsReconciliation = fNeedsReconciliation;
            string oldBoundLibraryRoot = fBoundLibraryRoot;
            string oldBoundArchivePath = fBoundArchivePath;
            string oldInventoryRoots = fInventoryRoots;
            using DownloadHistoryLease? disableLease = !enabled && oldEnabled && !oldBoundArchivePath.IsNullEmptyWhitespace()
                ? AcquireManagementLease(oldBoundArchivePath, "disable protection")
                : null;
            bool pathChanged = !string.Equals(oldArchivePath, configuredArchivePath, StringComparison.Ordinal);
            bool nextEverEnabled = enabled || oldEverEnabled;
            string nextBoundLibraryRoot = enabled ? GetLibraryRoot() : oldBoundLibraryRoot;
            string nextBoundArchivePath = enabled
                ? (configuredArchivePath.IsNullEmptyWhitespace()
                    ? (oldEverEnabled && !oldBoundArchivePath.IsNullEmptyWhitespace() ? oldBoundArchivePath : Path.Combine(nextBoundLibraryRoot, "yt-dlp-archive.txt"))
                    : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredArchivePath)))
                : oldBoundArchivePath;
            bool nextNeedsReconciliation = enabled
                ? false
                : oldNeedsReconciliation || oldEnabled || oldEverEnabled || pathChanged;

            try {
                PersistSettingsFailClosed(enabled, configuredArchivePath, keepBackup, true,
                    nextEverEnabled, nextNeedsReconciliation, nextBoundLibraryRoot, nextBoundArchivePath, normalizedInventoryRoots);
            }
            catch {
                try {
                    PersistSettingsFailClosed(oldEnabled, oldArchivePath, oldKeepBackup, oldFailIfUnavailable,
                        oldEverEnabled, oldNeedsReconciliation, oldBoundLibraryRoot, oldBoundArchivePath, oldInventoryRoots);
                }
                catch { }
                throw;
            }

            fArchivePath = configuredArchivePath;
            fKeepBackup = keepBackup;
            fFailIfUnavailable = true;
            fEnabled = enabled;
            fEverEnabled = nextEverEnabled;
            fNeedsReconciliation = nextNeedsReconciliation;
            fBoundLibraryRoot = nextBoundLibraryRoot;
            fBoundArchivePath = nextBoundArchivePath;
            fInventoryRoots = normalizedInventoryRoots;
            if (enabled) {
                PreparedKey = preparedKey;
                LastReportInternal = preparedReport!;
            }
            else {
                PreparedKey = null;
                LastReportInternal = DisabledReport();
            }
        }
    }

    public static DownloadHistoryReport ValidateAndReconcile(bool force, bool allowMigration = false) {
        lock (Sync) {
            if (!Enabled) {
                LastReportInternal = DisabledReport();
                return LastReportInternal;
            }
            if (!TryResolvePaths(ArchivePath, out string libraryRoot, out string archive, out DownloadHistoryReport? pathError)) {
                LastReportInternal = pathError!;
                return LastReportInternal;
            }
            if (!TryResolveInventoryRoots(libraryRoot, fInventoryRoots, out List<string> scanRoots, out DownloadHistoryReport? rootError)) {
                LastReportInternal = rootError!;
                return LastReportInternal;
            }

            string key = archive;
            if (!force && !NeedsReconciliation && PreparedKey == key && LastReportInternal.State == DownloadHistoryState.Healthy) {
                return LastReportInternal;
            }

            if (!TryAcquireArchiveLease(archive, out DownloadHistoryLease? lease)) {
                LastReportInternal = BusyReport("validate or reconcile the library");
                return LastReportInternal;
            }
            DownloadHistoryReport report;
            using (lease!) {
                DownloadHistoryAnalysis analysis = AnalyzeCore(libraryRoot, scanRoots, archive, CandidateRequiresLibraryRecovery(libraryRoot, archive));
                report = ReconcileAnalysis(analysis, KeepBackup, allowMigration);
            }
            LastReportInternal = report;
            if (report.State == DownloadHistoryState.Healthy) {
                PreparedKey = key;
                NeedsReconciliation = false;
                EverEnabled = true;
                BoundLibraryRoot = libraryRoot;
                BoundArchivePath = archive;
            }
            return report;
        }
    }

    private static DownloadHistoryReport ReconcileAnalysis(DownloadHistoryAnalysis analysis, bool keepBackup, bool allowMigration) {
        DownloadHistoryReport report = analysis.Report;
        if (!report.CanReconcile) return report;

        bool changed = analysis.ArchiveNeedsRewrite || !analysis.ArchiveExists;
        if (analysis.RecoverMissingEntries) {
            foreach (string entry in analysis.RecoveredEntries) {
                if (analysis.ArchiveEntries.Add(entry)) changed = true;
            }
        }

        if (changed) {
            try {
                WriteArchiveAtomically(analysis.ArchivePath, analysis.ArchiveEntries);
            }
            catch (Exception ex) {
                report.State = DownloadHistoryState.Unavailable;
                report.CanReconcile = false;
                report.Message = "Download archive could not be written safely: " + ex.Message;
                return report;
            }
        }

        if (keepBackup) {
            try {
                CopyArchiveToBackupAtomically(analysis.ArchivePath);
            }
            catch (Exception ex) {
                report.State = DownloadHistoryState.Unavailable;
                report.CanReconcile = true;
                report.ArchiveEntries = analysis.ArchiveEntries.Count;
                report.Message = "The primary Download History archive is valid, but its backup could not be refreshed: " + ex.Message;
                return report;
            }
        }

        report.ArchiveEntries = analysis.ArchiveEntries.Count;
        report.ArchiveDigest = ComputeArchiveDigest(analysis.ArchiveEntries);
        report.State = DownloadHistoryState.Healthy;
        report.CanReconcile = true;
        report.Message = $"Download History is healthy. {report.ArchiveEntries:N0} archive entr{(report.ArchiveEntries == 1 ? "y" : "ies")} validated without modifying existing media.";
        return report;
    }

    private static bool EnsureReady(out string error) {
        if (NeedsReconciliation) {
            LastReportInternal = new DownloadHistoryReport {
                State = DownloadHistoryState.Unsafe,
                CanReconcile = true,
                Message = "Download History requires explicit reconciliation before protected downloading can continue. Open Download History and validate or rebuild the archive."
            };
            error = LastReportInternal.Message;
            return false;
        }

        DownloadHistoryReport report = ValidateArchiveForProtectedExecution();
        LastReportInternal = report;
        if (report.State == DownloadHistoryState.Healthy) {
            PreparedKey = EffectiveArchivePath;
            error = string.Empty;
            return true;
        }
        error = report.Message.IsNullEmptyWhitespace() ? "Download History is not in a safe state." : report.Message;
        return false;
    }

    private static DownloadHistoryReport ValidateArchiveForProtectedExecution() {
        if (!TryResolvePaths(ArchivePath, out _, out string archive, out DownloadHistoryReport? pathError)) return pathError!;
        if (!TryAcquireArchiveLease(archive, out DownloadHistoryLease? lease)) return BusyReport("validate the archive");

        using (lease!) {
            HashSet<string> entries = new(StringComparer.Ordinal);
            bool primaryExists = File.Exists(archive);
            bool primaryValid = TryReadArchive(archive, entries, out string primaryError);
            string backup = archive + ".bak";
            HashSet<string> backupEntries = new(StringComparer.Ordinal);
            bool backupValid = TryReadArchive(backup, backupEntries, out string backupError);

            if (!primaryValid && !backupValid) {
                return new DownloadHistoryReport {
                    State = primaryExists ? DownloadHistoryState.Invalid : DownloadHistoryState.Missing,
                    CanReconcile = false,
                    Message = primaryExists
                        ? "The Download History archive is invalid and no valid last-good backup is available. Run Rebuild Archive before protected downloading. " + primaryError
                        : "The Download History archive and its last-good backup are unavailable. Run Rebuild Archive before protected downloading." +
                          (backupError.IsNullEmptyWhitespace() ? string.Empty : " " + backupError)
                };
            }

            if (!CanWriteArchiveLocation(archive, out string writeError)) {
                return new DownloadHistoryReport {
                    State = DownloadHistoryState.Unavailable,
                    CanReconcile = false,
                    Message = "Download archive location is not writable: " + writeError
                };
            }

            bool changed = false;
            if (!primaryValid) {
                entries.Clear();
                foreach (string entry in backupEntries) entries.Add(entry);
                changed = true;
            }
            else if (backupValid) {
                foreach (string entry in backupEntries) {
                    if (entries.Add(entry)) changed = true;
                }
            }

            if (changed) {
                try { WriteArchiveAtomically(archive, entries); }
                catch (Exception ex) {
                    return new DownloadHistoryReport {
                        State = DownloadHistoryState.Unavailable,
                        CanReconcile = false,
                        Message = "Download History could not restore its native archive safely: " + ex.Message
                    };
                }
            }

            if (KeepBackup) {
                try { CopyArchiveToBackupAtomically(archive); }
                catch (Exception ex) {
                    return new DownloadHistoryReport {
                        State = DownloadHistoryState.Unavailable,
                        CanReconcile = false,
                        ArchiveEntries = entries.Count,
                        Message = "The Download History archive is valid, but its last-good backup could not be refreshed: " + ex.Message
                    };
                }
            }

            return new DownloadHistoryReport {
                State = DownloadHistoryState.Healthy,
                CanReconcile = true,
                ArchiveEntries = entries.Count,
                Message = $"Download History is healthy. {entries.Count:N0} native archive entr{(entries.Count == 1 ? "y" : "ies")} validated without scanning media roots."
            };
        }
    }

    private static DownloadHistoryReport DisabledReport() => new() {
        State = EverEnabled ? DownloadHistoryState.Dormant : DownloadHistoryState.Disabled,
        CanReconcile = true,
        Message = EverEnabled ? "Download History is disabled; archive preserved and potentially stale." : "Download History is disabled."
    };

    private static bool CandidateRequiresLibraryRecovery(string libraryRoot, string candidateArchive) {
        if (!Enabled || !EverEnabled || NeedsReconciliation) return true;
        if (BoundLibraryRoot.IsNullEmptyWhitespace() || !PathEquals(libraryRoot, BoundLibraryRoot)) return true;
        if (BoundArchivePath.IsNullEmptyWhitespace()) return true;
        return !PathEquals(candidateArchive, BoundArchivePath);
    }

    private static bool IsPreviouslyInitializedNamespace(string libraryRoot, string archive) {
        if (!EverEnabled) return false;
        if (BoundArchivePath.IsNullEmptyWhitespace()) return true;
        return PathEquals(archive, BoundArchivePath);
    }

    private static bool IsDefaultArchiveForLibrary(string libraryRoot, string archive) =>
        PathEquals(archive, Path.Combine(libraryRoot, "yt-dlp-archive.txt"));

    private static bool CanInitializeNewDefaultLibrary(string libraryRoot, string archive) {
        if (!IsDefaultArchiveForLibrary(libraryRoot, archive) || IsPreviouslyInitializedNamespace(libraryRoot, archive)) return false;
        string? root = Path.GetPathRoot(libraryRoot);
        return !root.IsNullEmptyWhitespace() && Directory.Exists(root);
    }

    private static bool TryInitializeNewDefaultLibrary(string libraryRoot, string archive, DownloadHistoryLease lease, out DownloadHistoryReport? error) {
        error = null;
        if (Directory.Exists(libraryRoot)) return true;
        if (!CanInitializeNewDefaultLibrary(libraryRoot, archive)) {
            error = new DownloadHistoryReport { State = DownloadHistoryState.Unavailable, CanReconcile = false, Message = "Media library path is unavailable: " + libraryRoot };
            return false;
        }
        try {
            Directory.CreateDirectory(libraryRoot);
            lease.AcquireFileLock(archive + ".lock");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            error = new DownloadHistoryReport { State = DownloadHistoryState.Unavailable, CanReconcile = false, Message = "The new media library directory could not be initialized safely: " + ex.Message };
            return false;
        }
    }

    private static bool ShouldWaitForPreviousArchive(string preparedArchive) =>
        !BoundArchivePath.IsNullEmptyWhitespace() && !PathEquals(preparedArchive, BoundArchivePath);

    private static bool PathEquals(string left, string right) {
        try {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool TryResolvePaths(string configuredArchivePath, out string libraryRoot, out string archive, out DownloadHistoryReport? error) {
        libraryRoot = string.Empty;
        archive = string.Empty;
        error = null;
        try {
            libraryRoot = GetLibraryRoot();
            archive = configuredArchivePath.IsNullEmptyWhitespace()
                ? (EverEnabled && !BoundArchivePath.IsNullEmptyWhitespace()
                    ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(BoundArchivePath))
                    : Path.Combine(libraryRoot, "yt-dlp-archive.txt"))
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredArchivePath));
            return true;
        }
        catch (Exception ex) {
            error = new DownloadHistoryReport {
                State = DownloadHistoryState.Invalid,
                CanReconcile = false,
                Message = "Download History path is invalid: " + ex.Message
            };
            return false;
        }
    }

    private static DownloadHistoryAnalysis AnalyzeCore(string libraryRoot, string archive, bool recoverMissingEntries) =>
        AnalyzeCore(libraryRoot, new[] { libraryRoot }, archive, recoverMissingEntries);

    private static IEnumerable<string> ParseConfiguredInventoryRoots(string configuredInventoryRoots) {
        if (configuredInventoryRoots.IsNullEmptyWhitespace()) yield break;
        foreach (string raw in configuredInventoryRoots.Split(new[] { '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            string candidate = raw.Trim();
            if (candidate.Length > 0) yield return ResolveLibraryRoot(candidate);
        }
    }

    private static bool IsPathWithin(string parent, string candidate) {
        string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedParent, normalizedCandidate, StringComparison.OrdinalIgnoreCase)) return true;
        return normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> MinimizeInventoryRoots(IEnumerable<string> roots) {
        List<string> result = [];
        foreach (string root in roots.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path.Length)) {
            if (result.Any(parent => IsPathWithin(parent, root))) continue;
            result.RemoveAll(child => IsPathWithin(root, child));
            result.Add(root);
        }
        return result;
    }

    private static string NormalizeInventoryRootsForStorage(string configuredInventoryRoots) =>
        string.Join("|", MinimizeInventoryRoots(ParseConfiguredInventoryRoots(configuredInventoryRoots)));

    private static bool InventoryRootsChanged(string configuredInventoryRoots) {
        try { return !string.Equals(NormalizeInventoryRootsForStorage(configuredInventoryRoots), fInventoryRoots, StringComparison.OrdinalIgnoreCase); }
        catch { return true; }
    }

    private static bool TryResolveInventoryRoots(string libraryRoot, string configuredInventoryRoots, out List<string> scanRoots, out DownloadHistoryReport? error) {
        scanRoots = [];
        error = null;
        try {
            List<string> candidates = [libraryRoot];
            candidates.AddRange(ParseConfiguredInventoryRoots(configuredInventoryRoots));
            scanRoots = MinimizeInventoryRoots(candidates);
            return true;
        }
        catch (Exception ex) {
            error = new DownloadHistoryReport {
                State = DownloadHistoryState.Invalid,
                CanReconcile = false,
                Message = "Download History inventory path is invalid: " + ex.Message
            };
            return false;
        }
    }

    private static DownloadHistoryAnalysis AnalyzeCore(string libraryRoot, IReadOnlyList<string> scanRoots, string archive, bool recoverMissingEntries) {
        DownloadHistoryAnalysis analysis = new() { LibraryRoot = libraryRoot, ArchivePath = archive };
        DownloadHistoryReport report = analysis.Report;

        if (!Directory.Exists(libraryRoot)) {
            if (CanInitializeNewDefaultLibrary(libraryRoot, archive)) {
                analysis.RecoverMissingEntries = true;
                report.State = DownloadHistoryState.Missing;
                report.CanReconcile = true;
                report.Message = "This is a new media library. Its download directory and initial Download History archive can be created safely.";
                return analysis;
            }
            report.State = DownloadHistoryState.Unavailable;
            report.CanReconcile = false;
            report.Message = "Media library path is unavailable: " + libraryRoot;
            return analysis;
        }

        foreach (string scanRoot in scanRoots) {
            if (!Directory.Exists(scanRoot)) {
                report.State = DownloadHistoryState.Unavailable;
                report.CanReconcile = false;
                report.Message = "Configured Download History inventory path is unavailable: " + scanRoot;
                return analysis;
            }
        }

        string? parent = Path.GetDirectoryName(archive);
        if (parent.IsNullEmptyWhitespace()) {
            report.State = DownloadHistoryState.Invalid;
            report.CanReconcile = false;
            report.Message = "Download archive location does not have a valid parent directory.";
            return analysis;
        }
        string? root = Path.GetPathRoot(parent!);
        if (!root.IsNullEmptyWhitespace() && !Directory.Exists(root)) {
            report.State = DownloadHistoryState.Unavailable;
            report.CanReconcile = false;
            report.Message = "Download archive storage is unavailable: " + root;
            return analysis;
        }
        if (!Directory.Exists(parent)) {
            report.State = DownloadHistoryState.Unavailable;
            report.CanReconcile = false;
            report.Message = "Configured download archive directory is unavailable: " + parent;
            return analysis;
        }
        if (!CanWriteArchiveLocation(archive, out string writeError)) {
            report.State = DownloadHistoryState.Unavailable;
            report.CanReconcile = false;
            report.Message = "Download archive location is not writable: " + writeError;
            return analysis;
        }

        analysis.ArchiveExists = File.Exists(archive);
        string backup = archive + ".bak";
        if (analysis.ArchiveExists) {
            if (TryReadArchive(archive, analysis.ArchiveEntries, out string archiveError)) {
                analysis.ArchiveValid = true;
                HashSet<string> backupEntries = new(StringComparer.Ordinal);
                if (TryReadArchive(backup, backupEntries, out _)) {
                    foreach (string entry in backupEntries) {
                        if (analysis.ArchiveEntries.Add(entry)) analysis.ArchiveNeedsRewrite = true;
                    }
                }
            }
            else {
                analysis.ArchiveWasInvalid = true;
                analysis.ArchiveEntries.Clear();
                if (TryReadArchive(backup, analysis.ArchiveEntries, out _)) {
                    analysis.UsedBackup = true;
                    analysis.ArchiveNeedsRewrite = true;
                }
                else {
                    analysis.ArchiveNeedsRewrite = true;
                    Log.Write("Download History archive is damaged and no valid backup is available: " + archiveError);
                }
            }
        }
        else if (TryReadArchive(backup, analysis.ArchiveEntries, out _)) {
            analysis.UsedBackup = true;
            analysis.ArchiveNeedsRewrite = true;
        }

        analysis.RecoverMissingEntries = recoverMissingEntries || !analysis.ArchiveExists || analysis.ArchiveWasInvalid || analysis.UsedBackup;

        FileNameIdentityMatcher filenameMatcher = new(analysis.ArchiveEntries);
        try {
            foreach (string scanRoot in scanRoots) {
                foreach (string media in EnumerateCompletedMedia(scanRoot)) {
                    string? entry = TryRecoverFromInfoJson(media, out _, out _);
                    bool fromMetadata = entry is not null;
                    if (entry is null) entry = filenameMatcher.Match(media);

                    // A current valid native archive is the success marker. An unarchived final-looking
                    // file may be residue from a failed provider run and must remain eligible for retry.
                    // Recovery states deliberately trust authoritative physical-library identities instead.
                    if (!analysis.RecoverMissingEntries && analysis.ArchiveValid &&
                        (entry is null || !analysis.ArchiveEntries.Contains(entry))) {
                        continue;
                    }

                    report.CompletedMedia++;
                    if (entry is null) {
                        report.UnresolvedMedia++;
                        continue;
                    }

                    if (fromMetadata) report.MetadataRecovered++;
                    else report.FilenameRecovered++;

                    report.IdentifiedMedia++;
                    analysis.RecoveredEntries.Add(entry);
                }
            }
        }
        catch (Exception ex) {
            report.State = DownloadHistoryState.Unavailable;
            report.CanReconcile = false;
            report.Message = "Media library cannot be scanned safely: " + ex.Message;
            return analysis;
        }
        report.MigrationCount = 0;
        report.ArchiveEntries = analysis.ArchiveEntries.Count;

        if (report.UnresolvedMedia > 0) {
            report.State = report.IdentifiedMedia > 0 ? DownloadHistoryState.Partial : DownloadHistoryState.Unsafe;
            report.CanReconcile = false;
            report.Message = $"Download History cannot safely enable protection: {report.UnresolvedMedia:N0} completed media file(s) have no authoritative recoverable source identity. No archive or media changes were written.";
            return analysis;
        }

        if (analysis.RecoverMissingEntries) {
            foreach (string entry in analysis.RecoveredEntries) {
                if (!analysis.ArchiveEntries.Contains(entry)) analysis.ArchiveNeedsRewrite = true;
            }
        }

        if (!analysis.ArchiveExists && !analysis.UsedBackup && report.CompletedMedia == 0 && IsPreviouslyInitializedNamespace(libraryRoot, archive)) {
            report.State = DownloadHistoryState.Missing;
            report.CanReconcile = false;
            report.Message = "The previously initialized Download History archive and its backup are missing, and the library contains no completed media from which history can be reconstructed. Reset History explicitly to start over.";
            return analysis;
        }
        if (analysis.ArchiveWasInvalid && !analysis.UsedBackup && report.CompletedMedia == 0) {
            report.State = DownloadHistoryState.Invalid;
            report.CanReconcile = false;
            report.Message = "The Download History archive is invalid, no valid backup exists, and the library contains no completed media from which history can be reconstructed.";
            return analysis;
        }

        report.CanReconcile = true;
        if (analysis.ArchiveWasInvalid) {
            report.State = DownloadHistoryState.Invalid;
            report.Message = analysis.UsedBackup
                ? "The primary archive is invalid but a valid backup can be restored and cross-checked against the library."
                : "The primary archive is invalid but the existing library can safely reconstruct it.";
        }
        else if (!analysis.ArchiveExists) {
            report.State = DownloadHistoryState.Missing;
            report.Message = analysis.UsedBackup
                ? "The primary archive is missing but a valid backup can be restored and cross-checked against the library."
                : report.CompletedMedia == 0
                    ? "This is a new empty library. Download History can initialize a new archive."
                    : "The archive is missing, but all completed media identities are recoverable and the archive can be rebuilt safely.";
        }
        else if (analysis.ArchiveNeedsRewrite) {
            report.State = DownloadHistoryState.Missing;
            report.Message = "The archive is incomplete relative to the existing library and can be reconciled safely before downloading.";
        }
        else {
            report.State = DownloadHistoryState.Healthy;
            report.Message = $"Download History is healthy. {report.ArchiveEntries:N0} archive entr{(report.ArchiveEntries == 1 ? "y" : "ies")} validated against {report.CompletedMedia:N0} trusted completed media file(s).";
        }
        return analysis;
    }

    private static string GetArchiveMutexName(string archivePath) {
        string normalized = Path.GetFullPath(archivePath).ToUpperInvariant();
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return @"Local\youtube-dl-gui-download-history-" + string.Concat(digest.Take(12).Select(value => value.ToString("x2")));
    }

    private static string GetLibraryRoot() => ResolveLibraryRoot(Downloads.downloadPath);

    private static string ResolveLibraryRoot(string path) {
        if (path.StartsWith("./") || path.StartsWith(".\\")) {
            path = Path.Combine(Program.ProgramPath, path.Substring(2));
        }
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }

    private static IEnumerable<string> EnumerateCompletedMedia(string root) {
        if (!Directory.Exists(root)) yield break;
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0) {
            string directory = pending.Pop();
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)) {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) {
                    throw new IOException("Download History will not traverse reparse-point media files inside the protected library: " + file);
                }
                string name = Path.GetFileName(file);
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".info.json", StringComparison.OrdinalIgnoreCase) ||
                    ext is ".json" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".srt" or ".vtt" or ".ass" or ".lrc" or ".description" or ".txt") {
                    continue;
                }
                if (ext is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi" or ".flv" or ".m4v" or ".3gp" or ".3g2" or
                    ".f4v" or ".mk3d" or ".divx" or ".ogv" or ".nut" or ".swf" or
                    ".ts" or ".m2ts" or ".mts" or ".vob" or ".wmv" or ".asf" or ".mpg" or ".mpeg" or ".mpe" or ".mpv" or ".m2v" or
                    ".mp3" or ".mp2" or ".m4a" or ".m4b" or ".m4r" or ".aac" or ".opus" or ".ogg" or ".oga" or ".ogx" or ".spx" or ".vorbis" or ".weba" or
                    ".wav" or ".flac" or ".wma" or ".mka" or ".ape" or ".alac" or ".aiff" or ".aif" or ".aifc" or ".tta" or
                    ".f4a" or ".f4b" or ".ac3" or ".eac3" or ".dts") {
                    yield return file;
                }
            }
            foreach (string child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)) {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) {
                    throw new IOException("Download History will not traverse reparse-point directories inside the protected library: " + child);
                }
                pending.Push(child);
            }
        }
    }

    private static string? TryRecoverFromInfoJson(string mediaPath, out string? sourceId, out string? infoPath) {
        sourceId = null;
        infoPath = null;
        string directory = Path.GetDirectoryName(mediaPath) ?? string.Empty;
        string stem = Path.Combine(directory, Path.GetFileNameWithoutExtension(mediaPath));
        string candidate = stem + ".info.json";
        if (!File.Exists(candidate)) return null;
        try {
            string json = File.ReadAllText(candidate);
            JavaScriptSerializer serializer = new() {
                MaxJsonLength = Math.Max(2 * 1024 * 1024, json.Length),
                RecursionLimit = 256
            };
            if (serializer.DeserializeObject(json) is not Dictionary<string, object> root) return null;
            string? recoveredId = root.TryGetValue("id", out object? idValue) ? idValue as string : null;
            string? extractor = null;
            foreach (string key in new[] { "extractor_key", "ie_key", "extractor" }) {
                if (root.TryGetValue(key, out object? value) && value is string text && !text.IsNullEmptyWhitespace()) {
                    extractor = text;
                    break;
                }
            }
            if (recoveredId.IsNullEmptyWhitespace() || extractor.IsNullEmptyWhitespace()) return null;
            string normalizedId = recoveredId!.Trim();
            if (!TryCreateArchiveEntry(extractor!, normalizedId, out string archiveEntry)) return null;
            sourceId = normalizedId;
            infoPath = candidate;
            return archiveEntry;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static IEnumerable<string> SourceIdFileNameCandidates(string sourceId) {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string candidate in new[] {
            sourceId,
            SanitizeSourceIdForFileName(sourceId),
            SanitizeSourceIdCompat(sourceId, false),
            SanitizeSourceIdCompat(sourceId, true)
        }) {
            if (!candidate.IsNullEmptyWhitespace() && seen.Add(candidate)) yield return candidate;
        }
    }

    private static string SanitizeSourceIdForFileName(string sourceId) {
        StringBuilder result = new();
        foreach (char value in sourceId) {
            if (value < 32 || value == 127) continue;
            result.Append(value switch {
                '/' => '\u29F8',
                '\\' => '\u29F9',
                '"' => '\uFF02',
                '*' => '\uFF0A',
                ':' => '\uFF1A',
                '<' => '\uFF1C',
                '>' => '\uFF1E',
                '?' => '\uFF1F',
                '|' => '\uFF5C',
                _ => value
            });
        }
        return result.Length == 0 ? "_" : result.ToString();
    }

    private static string SanitizeSourceIdCompat(string sourceId, bool restricted) {
        StringBuilder result = new();
        foreach (char value in sourceId) {
            if (value < 32 || value == 127 || value == '?') continue;
            if (value == '"') {
                if (!restricted) result.Append('\'');
                continue;
            }
            if (value == ':') {
                result.Append(restricted ? "_-" : " -");
                continue;
            }
            if (value is '/' or '\\' or '|' or '*' or '<' or '>') {
                result.Append('_');
                continue;
            }
            if (restricted && (char.IsWhiteSpace(value) || value > 127 || "!&'()[]{}$;`^,#".IndexOf(value) >= 0)) {
                result.Append('_');
                continue;
            }
            result.Append(value);
        }
        return result.Length == 0 ? "_" : result.ToString();
    }

    private static bool FileNameMatchesSourceId(string mediaPath, string sourceId) {
        if (SchemaFileNameContainsSourceId(mediaPath, sourceId)) return true;
        string name = Path.GetFileNameWithoutExtension(mediaPath);
        if (Regex.IsMatch(name, Regex.Escape("-" + sourceId) + "(?:_[A-Za-z0-9_-]+)?$", RegexOptions.CultureInvariant)) return true;
        return Regex.IsMatch(name, "\\[" + Regex.Escape(sourceId) + "\\]$", RegexOptions.CultureInvariant);
    }

    private const string KnownSchemaEncodingPrefix = "v2:";

    private static string[] DecodeKnownFileNameSchemas(string value) {
        if (value.IsNullEmptyWhitespace()) return Array.Empty<string>();
        if (!value.StartsWith(KnownSchemaEncodingPrefix, StringComparison.Ordinal)) {
            return value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(candidate => candidate.Trim()).Where(candidate => candidate.Length > 0).ToArray();
        }

        List<string> decoded = [];
        foreach (string encoded in value.Substring(KnownSchemaEncodingPrefix.Length).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)) {
            try {
                string candidate = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Trim();
                if (candidate.Length > 0) decoded.Add(candidate);
            }
            catch (FormatException) { }
        }
        return decoded.ToArray();
    }

    private static string EncodeKnownFileNameSchemas(IEnumerable<string> schemas) => KnownSchemaEncodingPrefix + string.Join("|",
        schemas.Select(schema => Convert.ToBase64String(Encoding.UTF8.GetBytes(schema))));

    private static bool RememberFileNameSchema(string schema, out string error) {
        error = string.Empty;
        if (!HasRequiredIdTemplate(schema)) return true;
        string normalized = schema.Trim();
        List<string> known = DecodeKnownFileNameSchemas(fKnownFileNameSchemas).ToList();
        if (known.Any(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase))) return true;
        try {
            known.Add(normalized);
            string updated = EncodeKnownFileNameSchemas(known);
            IniProvider.Write(updated, ConfigName, "KnownFileNameSchemas");
            fKnownFileNameSchemas = updated;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            error = "Download History could not persist the protected filename format needed for future archive recovery: " + ex.Message;
            return false;
        }
    }

    private static IEnumerable<string> RecoveryFileNameSchemas() {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        if (!Downloads.fileNameSchema.IsNullEmptyWhitespace() && seen.Add(Downloads.fileNameSchema)) {
            yield return Downloads.fileNameSchema;
        }
        foreach (string schema in DecodeKnownFileNameSchemas(fKnownFileNameSchemas)) {
            if (seen.Add(schema)) yield return schema;
        }
    }

    private static bool SchemaFileNameContainsSourceId(string mediaPath, string sourceId) {
        foreach (string schema in RecoveryFileNameSchemas()) {
            string template = GetSchemaFileTemplate(schema);
            if (template.IsNullEmptyWhitespace() || template.IndexOf("%(id)s", StringComparison.OrdinalIgnoreCase) < 0) continue;
            string pattern = BuildSchemaRegex(template, Regex.Escape(sourceId));
            if (Regex.IsMatch(Path.GetFileName(mediaPath), pattern, RegexOptions.CultureInvariant)) return true;
        }
        return false;
    }

    private static string GetSchemaFileTemplate(string schema) {
        string normalized = schema.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator >= 0 ? normalized.Substring(separator + 1) : normalized;
    }

    private static string BuildSchemaRegex(string template, string idPattern) {
        const string idToken = "%(id)s";
        const string conversionTypes = "diouxXeEfFgGcrsBjhlqDSU";
        StringBuilder pattern = new("^");
        int position = 0;
        while (position < template.Length) {
            int tokenStart = template.IndexOf("%(", position, StringComparison.Ordinal);
            if (tokenStart < 0) {
                pattern.Append(Regex.Escape(template.Substring(position)));
                break;
            }
            pattern.Append(Regex.Escape(template.Substring(position, tokenStart - position)));
            int close = template.IndexOf(')', tokenStart + 2);
            if (close < 0) {
                pattern.Append(Regex.Escape(template.Substring(tokenStart)));
                break;
            }
            int tokenEnd = close + 1;
            while (tokenEnd < template.Length && conversionTypes.IndexOf(template[tokenEnd]) < 0) tokenEnd++;
            if (tokenEnd >= template.Length) {
                pattern.Append(Regex.Escape(template.Substring(tokenStart)));
                break;
            }
            string token = template.Substring(tokenStart, tokenEnd + 1 - tokenStart);
            pattern.Append(string.Equals(token, idToken, StringComparison.OrdinalIgnoreCase) ? idPattern : ".*?");
            position = tokenEnd + 1;
        }
        pattern.Append('$');
        return pattern.ToString();
    }

    private static string ComputeArchiveDigest(IEnumerable<string> entries) {
        string normalized = string.Join("\n", entries.OrderBy(entry => entry, StringComparer.Ordinal));
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return string.Concat(digest.Select(value => value.ToString("x2")));
    }

    private static bool CanWriteArchiveLocation(string archive, out string error) {
        error = string.Empty;
        try {
            if (File.Exists(archive)) {
                using FileStream stream = new(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                return true;
            }
            string parent = Path.GetDirectoryName(archive)!;
            string probe = Path.Combine(parent, ".youtube-dl-gui-history-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            }
            finally { if (File.Exists(probe)) File.Delete(probe); }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryCreateArchiveEntry(string extractor, string sourceId, out string entry) {
        entry = string.Empty;
        if (extractor is null || sourceId is null) return false;
        string normalizedExtractor = extractor.Trim();
        string normalizedId = sourceId.Trim();
        if (normalizedExtractor.Length == 0 || normalizedId.Length == 0) return false;
        if (normalizedExtractor.Any(char.IsWhiteSpace) || normalizedExtractor.Any(char.IsControl) || normalizedId.Any(char.IsControl)) return false;
        entry = normalizedExtractor.ToLowerInvariant() + " " + normalizedId;
        return true;
    }

    private static bool IsValidArchiveEntry(string entry) {
        int separator = entry.IndexOf(' ');
        if (separator <= 0 || separator == entry.Length - 1) return false;
        return TryCreateArchiveEntry(entry.Substring(0, separator), entry.Substring(separator + 1), out string normalized)
            && string.Equals(entry, normalized, StringComparison.Ordinal);
    }

    private static bool TryReadArchive(string path, HashSet<string> entries, out string error) {
        error = string.Empty;
        if (!File.Exists(path)) return false;
        HashSet<string> parsed = new(StringComparer.Ordinal);
        try {
            foreach (string line in File.ReadAllLines(path)) {
                string entry = line.Trim();
                if (entry.Length == 0) continue;
                if (!IsValidArchiveEntry(entry)) {
                    error = "Invalid archive entry: " + entry;
                    return false;
                }
                parsed.Add(entry);
            }
            foreach (string entry in parsed) entries.Add(entry);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            error = ex.Message;
            return false;
        }
    }

    private static void WriteArchiveAtomically(string path, HashSet<string> entries) {
        foreach (string entry in entries) {
            if (!IsValidArchiveEntry(entry)) throw new InvalidDataException("Refusing to write invalid Download History archive entry: " + entry);
        }
        string temp = path + ".tmp";
        string content = string.Join(Environment.NewLine, entries.OrderBy(x => x, StringComparer.Ordinal));
        if (content.Length > 0) content += Environment.NewLine;
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        try {
            if (File.Exists(path)) File.Replace(temp, path, null, true);
            else File.Move(temp, path);
        }
        finally {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void CopyArchiveToBackupAtomically(string archive) {
        if (!File.Exists(archive)) return;
        string backup = archive + ".bak";
        string temp = backup + ".tmp";
        File.Copy(archive, temp, true);
        try {
            if (File.Exists(backup)) File.Replace(temp, backup, null, true);
            else File.Move(temp, backup);
        }
        finally {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool ContainsOption(string? arguments, string option) {
        if (arguments.IsNullEmptyWhitespace()) return false;
        foreach (string token in TokenizeArguments(arguments!)) {
            int equals = token.IndexOf('=');
            string name = equals >= 0 ? token.Substring(0, equals) : token;
            if (name.Equals(option, StringComparison.OrdinalIgnoreCase) ||
                (name.StartsWith("--", StringComparison.Ordinal) && option.StartsWith(name, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsShortOption(string? arguments, string option) {
        if (arguments.IsNullEmptyWhitespace()) return false;
        foreach (string token in TokenizeArguments(arguments!)) {
            if (token.Equals(option, StringComparison.Ordinal) ||
                token.StartsWith(option + "=", StringComparison.Ordinal) ||
                (token.Length > option.Length && token.StartsWith(option, StringComparison.Ordinal))) {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> TokenizeArguments(string arguments) {
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < arguments.Length;) {
            if (!inQuotes && char.IsWhiteSpace(arguments[i])) {
                if (current.Length > 0) {
                    yield return current.ToString();
                    current.Clear();
                }
                i++;
                continue;
            }

            int slashStart = i;
            while (i < arguments.Length && arguments[i] == '\\') i++;
            int slashes = i - slashStart;
            if (i < arguments.Length && arguments[i] == '"') {
                current.Append('\\', slashes / 2);
                if ((slashes & 1) == 0) inQuotes = !inQuotes;
                else current.Append('"');
                i++;
                continue;
            }

            current.Append('\\', slashes);
            if (i >= arguments.Length) break;
            if (arguments[i] == '"') {
                inQuotes = !inQuotes;
                i++;
                continue;
            }
            current.Append(arguments[i]);
            i++;
        }

        if (current.Length > 0) yield return current.ToString();
    }
}

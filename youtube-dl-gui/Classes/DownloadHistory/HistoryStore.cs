#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace youtube_dl_gui.History {
    internal sealed class HistoryException : IOException {
        public HistoryException(string message) : base(message) { }
    }

    internal sealed class HistoryBusyException : IOException {
        public HistoryBusyException(string message) : base(message) { }
    }

    internal sealed class HistoryOptions {
        public Func<bool> Cancelled { get; set; }
        public string LibraryPath { get; set; }
        public string ArchivePath { get; set; }
        public string Template { get; set; }
        public bool UseInfoJson { get; set; } = true;
        public bool KeepBackup { get; set; } = true;
        public bool StopWhenMissing { get; set; }
        // Empty means no assumption. This is an explicit user declaration, not a URL guess.
        public string LegacyExtractor { get; set; } = "";
    }

    internal sealed class HistoryIdentity {
        public string Extractor { get; private set; }
        public string Id { get; private set; }
        public string Entry { get { return Extractor + " " + Id; } }
        private HistoryIdentity(string extractor, string id) { Extractor = extractor; Id = id; }
        public static HistoryIdentity Parse(string entry) {
            if (entry == null || entry.Length > 4096) throw new HistoryException("Invalid archive entry.");
            int split = entry.IndexOf(' ');
            if (split < 1) throw new HistoryException("An archive entry must contain an extractor and a source ID.");
            string extractor = entry.Substring(0, split);
            string id = entry.Substring(split + 1);
            if (!Regex.IsMatch(extractor, @"\A[A-Za-z0-9_.:+-]+\z") || string.IsNullOrWhiteSpace(id)
                || id != id.Trim() || id.Any(char.IsControl)) {
                throw new HistoryException("Invalid extractor/source identity in archive.");
            }
            return new HistoryIdentity(extractor.ToLowerInvariant(), id);
        }
    }

    internal static class HistoryTemplate {
        public static void Validate(string template) {
            if (string.IsNullOrWhiteSpace(template) || template.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0
                || Path.IsPathRooted(template) || template.Split('\\', '/').Any(s => s == "..")) {
                throw new HistoryException("Download History requires a relative filename format inside the library.");
            }
            string name = template.Split('\\', '/').Last();
            bool hasId = false;
            for (int i = 0; i < name.Length; i++) {
                if (name[i] != '%') continue;
                if (i + 1 < name.Length && name[i + 1] == '%') { i++; continue; }
                if (name.Substring(i).StartsWith("%(id)s", StringComparison.Ordinal)) hasId = true;
            }
            if (!hasId) throw new HistoryException("Download History requires %(id)s in the media filename, not only in a folder name. Keep IDs so history can be validated or rebuilt.");
            if (!name.EndsWith(".%(ext)s", StringComparison.Ordinal)) {
                throw new HistoryException("Protected filenames must end in .%(ext)s so completed media and sidecars can be distinguished safely.");
            }
        }
        public static string Suggest(string template) {
            if (string.IsNullOrWhiteSpace(template)) return "%(title)s-%(id)s.%(ext)s";
            string suffix = ".%(ext)s";
            return template.EndsWith(suffix, StringComparison.Ordinal)
                ? template.Substring(0, template.Length - suffix.Length) + "-%(id)s" + suffix
                : template + "-%(id)s" + suffix;
        }
    }

    [DataContract]
    internal sealed class HistoryRecord {
        [DataMember] public string Path;
        [DataMember] public string Entry;
        [DataMember] public long Length;
        [DataMember] public long WriteTicks;
        [DataMember] public bool Complete;
    }

    [DataContract]
    internal sealed class HistoryCheckpoint {
        [DataMember] public int Version = 1;
        [DataMember] public string SynchronizedUtc;
        [DataMember] public string LibraryPath;
        [DataMember] public List<string> Entries = new List<string>();
        [DataMember] public List<HistoryRecord> Files = new List<HistoryRecord>();
    }

    [DataContract]
    internal sealed class HistoryPending {
        [DataMember] public int Version = 1;
        [DataMember] public string ArchivePath;
        [DataMember] public int ProcessId;
        [DataMember] public long ProcessStartTicks;
    }

    [DataContract]
    internal sealed class HistoryMove {
        [DataMember] public string Source;
        [DataMember] public string Destination;
    }

    [DataContract]
    internal sealed class HistoryMetadata {
        [DataMember(Name = "id")] public string Id = null;
        [DataMember(Name = "extractor_key")] public string ExtractorKey = null;
        [DataMember(Name = "extractor")] public string Extractor = null;
        [DataMember(Name = "_type")] public string Type = null;
    }

    internal sealed class HistoryMigration {
        public string MediaPath;
        public string MetadataPath;
        public HistoryIdentity Identity;
    }

    internal sealed class HistoryReport {
        public readonly List<HistoryRecord> Media = new List<HistoryRecord>();
        public readonly List<HistoryMigration> Migrations = new List<HistoryMigration>();
        public readonly List<string> Unresolved = new List<string>();
        public readonly List<string> Incomplete = new List<string>();
        public readonly HashSet<string> Entries = new HashSet<string>(StringComparer.Ordinal);
        public bool ArchiveMissing;
        public bool ArchiveDamaged;
        public bool BackupUsed;
        public int MetadataIdentified;
        public int EmbeddedIds;
        public int HistoricalOnly;
        public int AddedEntries;
        public int TotalMedia { get { return Media.Count(r => r.Complete) + Migrations.Count + Unresolved.Count; } }
        public bool Recoverable { get { return Unresolved.Count == 0 && Migrations.Count == 0; } }
        public string State {
            get {
                if (Unresolved.Count > 0) return EmbeddedIds + Migrations.Count > 0 ? "Partial" : "Unsafe";
                if (Migrations.Count > 0) return "Migratable";
                if (ArchiveMissing) return "Missing";
                if (ArchiveDamaged || AddedEntries > 0) return "Stale";
                return Incomplete.Count > 0 ? "Incomplete (retryable files excluded)" : "Healthy";
            }
        }
        public string Summary {
            get {
                return State + Environment.NewLine
                    + "Completed media candidates: " + TotalMedia + Environment.NewLine
                    + "With embedded IDs: " + EmbeddedIds + Environment.NewLine
                    + "Identified through metadata: " + MetadataIdentified + Environment.NewLine
                    + "Need filename migration: " + Migrations.Count + Environment.NewLine
                    + "Unresolved: " + Unresolved.Count + Environment.NewLine
                    + "Incomplete/temporary files excluded: " + Incomplete.Count + Environment.NewLine
                    + "Archive identities: " + Entries.Count + Environment.NewLine
                    + "Historical identities without current media: " + HistoricalOnly + Environment.NewLine
                    + "Identities missing from primary archive: " + AddedEntries
                    + (BackupUsed ? Environment.NewLine + "Recovery used the backup archive." : "")
                    + (Unresolved.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, Unresolved.Take(12)) : "");
            }
        }
    }

    // One lease spans preflight, the child process, postflight and any recovery writes.
    // FileShare.None coordinates GUI instances and mapped/UNC aliases on the same server.
    internal sealed class HistoryStore : IDisposable {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> MediaExtensions = new HashSet<string>(
            (".3gp .3g2 .aac .aiff .aif .alac .asf .avi .f4v .flac .flv .m2ts .m4a .m4v .mka .mkv "
            + ".mov .mp2 .mp3 .mp4 .mpeg .mpg .mts .oga .ogg .ogv .opus .ts .vob .wav .webm .wma .wmv").Split(' '), StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SidecarExtensions = new HashSet<string>(
            ".json .jpg .jpeg .png .webp .gif .bmp .srt .vtt .ass .ssa .lrc .ttml .srv1 .srv2 .srv3 .xml .description .txt .url .nfo .mhtml .lock .bak".Split(' '), StringComparer.OrdinalIgnoreCase);
        private readonly HistoryOptions options;
        private readonly List<FileStream> locks = new List<FileStream>();
        private bool disposed;
        private bool ownsPending;
        public string LibraryPath { get; private set; }
        public string ArchivePath { get; private set; }
        private string StatePath { get { return ArchivePath + ".state.json"; } }
        private string RetryPath { get { return Path.Combine(LibraryPath, ".ytdlg-history.retry.json"); } }
        private string PendingPath { get { return ArchivePath + ".pending.json"; } }
        private string LibraryPendingPath { get { return Path.Combine(LibraryPath, ".ytdlg-history.pending.json"); } }
        private string MigrationPath { get { return ArchivePath + ".migration.json"; } }

        private HistoryStore(HistoryOptions options) {
            this.options = new HistoryOptions { LibraryPath = options.LibraryPath, ArchivePath = options.ArchivePath,
                Template = options.Template, UseInfoJson = options.UseInfoJson, KeepBackup = options.KeepBackup,
                StopWhenMissing = options.StopWhenMissing, LegacyExtractor = options.LegacyExtractor, Cancelled = options.Cancelled };
        }
        private string LibraryPrefix { get { return LibraryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; } }
        public static HistoryStore Open(HistoryOptions options) {
            if (options == null) throw new ArgumentNullException(nameof(options));
            HistoryTemplate.Validate(options.Template);
            var store = new HistoryStore(options);
            try {
                store.LibraryPath = Canonical(options.LibraryPath);
                store.ArchivePath = string.IsNullOrWhiteSpace(options.ArchivePath)
                    ? Path.Combine(store.LibraryPath, "yt-dlp-archive.txt") : Canonical(options.ArchivePath);
                RequireDirectory(store.LibraryPath);
                RequireDirectory(Path.GetDirectoryName(store.ArchivePath));
                if (MediaExtensions.Contains(Path.GetExtension(store.ArchivePath)) || store.ArchivePath.EndsWith(".info.json", StringComparison.OrdinalIgnoreCase)) {
                    throw new HistoryException("The archive cannot use a media or .info.json filename.");
                }
                RejectReparse(store.ArchivePath, false);
                var names = new[] { Path.Combine(store.LibraryPath, ".ytdlg-history.lock"), store.ArchivePath + ".lock" };
                foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)) {
                    RejectReparse(name, false);
                    try { store.locks.Add(new FileStream(name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
                    catch (IOException ex) when ((ex.HResult & 0xffff) == 32 || (ex.HResult & 0xffff) == 33) {
                        throw new HistoryBusyException("Another protected job is using this library or archive.");
                    }
                }
                return store;
            }
            catch { store.Dispose(); throw; }
        }

        internal static string Canonical(string path) {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new HistoryException("The library and custom archive paths must be absolute.");
            string full = Path.GetFullPath(path);
            return full == Path.GetPathRoot(full) ? full : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        private static void RequireDirectory(string path) {
            RejectReparse(path, true);
            if ((File.GetAttributes(path) & FileAttributes.Directory) == 0) throw new HistoryException("Storage is not a directory: " + path);
        }
        private static bool Exists(string path) {
            try { return (File.GetAttributes(path) & FileAttributes.Directory) == 0; }
            catch (FileNotFoundException) { return false; }
            // DirectoryNotFound / access denied is unavailable storage, not an absent archive.
        }
        private static void RejectReparse(string path, bool required) {
            string current = path;
            bool first = true;
            while (!string.IsNullOrEmpty(current)) {
                try {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new HistoryException("Reparse points/symlinks are not supported for protected storage: " + current);
                }
                catch (FileNotFoundException) { if (!first || required) throw; }
                catch (DirectoryNotFoundException) { if (!first || required) throw; }
                first = false;
                current = Path.GetDirectoryName(current);
            }
        }
        private void CheckCancellation() {
            if (options.Cancelled != null && options.Cancelled()) throw new OperationCanceledException("Download History scan cancelled.");
        }
        private void CheckOpen() {
            CheckCancellation();
            if (disposed) throw new ObjectDisposedException(nameof(HistoryStore));
            RequireDirectory(LibraryPath);
            RequireDirectory(Path.GetDirectoryName(ArchivePath));
        }
        private IEnumerable<string> EnumerateLibrary() {
            var directories = new Stack<string>();
            directories.Push(LibraryPath);
            while (directories.Count > 0) {
                string directory = directories.Pop();
                CheckCancellation();
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory)) {
                    CheckCancellation();
                    FileAttributes attr = File.GetAttributes(entry);
                    if ((attr & FileAttributes.ReparsePoint) != 0) throw new HistoryException("The library contains a reparse point; no files were silently skipped: " + entry);
                    if ((attr & FileAttributes.Directory) != 0) directories.Push(entry);
                    else yield return entry;
                }
            }
        }
        private bool IsServiceFile(string path) {
            if (string.Equals(path, Path.Combine(LibraryPath, ".ytdlg-history.lock"), StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, LibraryPendingPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, RetryPath, StringComparison.OrdinalIgnoreCase)) return true;
            if (new[] { ArchivePath, ArchivePath + ".bak", ArchivePath + ".lock", StatePath, PendingPath, MigrationPath }
                .Any(p => string.Equals(path, p, StringComparison.OrdinalIgnoreCase))) return true;
            return new[] { RetryPath + ".tmp-", LibraryPendingPath + ".tmp-", ArchivePath + ".corrupt-", ArchivePath + ".reset-", ArchivePath + ".bak.reset-", StatePath + ".reset-",
                ArchivePath + ".tmp-", StatePath + ".tmp-", PendingPath + ".tmp-", MigrationPath + ".tmp-", ArchivePath + ".bak.tmp-" }
                .Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && Regex.IsMatch(path.Substring(prefix.Length), @"\A(?:[0-9]{8}T[0-9]{6}-)?[0-9a-f]{32}\z"));
        }
        private static bool Temporary(string path) {
            string name = Path.GetFileName(path);
            return name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(name, @"(?i)\.(?:part(?:-Frag\d+)?|temp|frag\d+|f\d+)\.")
                || Regex.IsMatch(name, @"(?i)\.part-Frag\d+$");
        }
        private static HashSet<string> ReadArchive(string path) {
            RejectReparse(path, false);
            var entries = new HashSet<string>(StringComparer.Ordinal);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream, Utf8, true)) {
                if (stream.Length > 128L * 1024 * 1024) throw new HistoryException("Archive exceeds the safe read limit (128 MiB).");
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    entries.Add(HistoryIdentity.Parse(line).Entry);
                }
            }
            return entries;
        }
        private static T ReadJson<T>(string path, int maxBytes) {
            RejectReparse(path, true);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                if (stream.Length > maxBytes) throw new HistoryException("Metadata exceeds its safe read limit: " + path);
                var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 4000000 });
                return (T)serializer.ReadObject(stream);
            }
        }
        private static string Json<T>(T value) {
            using (var stream = new MemoryStream()) {
                new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 4000000 }).WriteObject(stream, value);
                return Utf8.GetString(stream.ToArray());
            }
        }
        private static void AtomicWrite(string path, string text) {
            RejectReparse(path, false);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try {
                byte[] bytes = Utf8.GetBytes(text);
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private HistoryCheckpoint LoadCheckpoint() {
            if (!Exists(StatePath)) return new HistoryCheckpoint();
            HistoryCheckpoint checkpoint = ReadJson<HistoryCheckpoint>(StatePath, 128 * 1024 * 1024);
            if (checkpoint == null || checkpoint.Version != 1 || checkpoint.Entries == null || checkpoint.Files == null) throw new HistoryException("Invalid Download History checkpoint; restore a trusted copy before proceeding.");
            foreach (string entry in checkpoint.Entries) HistoryIdentity.Parse(entry);
            foreach (HistoryRecord record in checkpoint.Files) {
                if (record == null || string.IsNullOrWhiteSpace(record.Path) || Path.IsPathRooted(record.Path)
                    || record.Path.Split('\\', '/').Any(s => s == "..")) throw new HistoryException("Unsafe path in Download History checkpoint.");
                if (record.Entry != null) HistoryIdentity.Parse(record.Entry);
            }
            if (!string.IsNullOrEmpty(checkpoint.LibraryPath)
                && !string.Equals(Canonical(checkpoint.LibraryPath), LibraryPath, StringComparison.OrdinalIgnoreCase)) {
                // Historical entries belong to the archive, but relative file records must not
                // be used to attest a different physical library after a scope/root change.
                checkpoint.Files.Clear();
            }
            if (checkpoint.Files.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1))
                throw new HistoryException("Duplicate paths in Download History checkpoint.");
            return checkpoint;
        }
        private HistoryCheckpoint LoadRetries() {
            if (!Exists(RetryPath)) return new HistoryCheckpoint();
            var state = ReadJson<HistoryCheckpoint>(RetryPath, 128 * 1024 * 1024);
            if (state == null || state.Version != 1 || state.Entries == null || state.Files == null)
                throw new HistoryException("Invalid library retry manifest. Restore it before rebuilding history.");
            foreach (string entry in state.Entries) HistoryIdentity.Parse(entry);
            foreach (HistoryRecord record in state.Files) {
                if (record == null || record.Complete || string.IsNullOrWhiteSpace(record.Path)
                    || Path.IsPathRooted(record.Path) || record.Path.Split('\\', '/').Any(s => s == ".."))
                    throw new HistoryException("Invalid failed-file record in the library retry manifest.");
                if (record.Entry != null) HistoryIdentity.Parse(record.Entry);
            }
            if (state.Files.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new HistoryException("Duplicate failed-file records in the library retry manifest.");
            return state;
        }
        private void SaveRetries(HistoryReport report) {
            var retries = LoadRetries();
            var failures = new HashSet<string>(retries.Entries, StringComparer.Ordinal);
            failures.UnionWith(report.Media.Where(r => !r.Complete && r.Entry != null).Select(r => r.Entry));
            failures.ExceptWith(report.Entries);
            var records = retries.Files.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var record in report.Media) {
                if (record.Complete) records.Remove(record.Path);
                else records[record.Path] = record;
            }
            var kept = records.Values.Where(r => r.Entry == null || !report.Entries.Contains(r.Entry)).ToList();
            AtomicWrite(RetryPath, Json(new HistoryCheckpoint { Entries = failures.ToList(), Files = kept }));
        }
        private void CheckPending() {
            if (ownsPending) return;
            if (Exists(LibraryPendingPath)) CheckPendingFile(LibraryPendingPath);
            if (Exists(PendingPath)) CheckPendingFile(PendingPath);
        }
        private void CheckPendingFile(string path) {
            HistoryPending pending = ReadJson<HistoryPending>(path, 8192);
            if (pending != null && !string.IsNullOrEmpty(pending.ArchivePath)
                && !string.Equals(pending.ArchivePath, ArchivePath, StringComparison.OrdinalIgnoreCase))
                throw new HistoryException("This library has an interrupted protected run using another archive. Recover that archive first: " + pending.ArchivePath);
            if (pending == null || pending.Version != 1 || pending.ProcessId == 0 || pending.ProcessId < -1) throw new HistoryException("An interrupted launch has no confirmed child-process identity. Verify that all downloaders have stopped before explicitly recovering the interrupted run.");
            if (pending.ProcessId == -1) return;
            try {
                using (var process = System.Diagnostics.Process.GetProcessById(pending.ProcessId)) {
                    if (!process.HasExited && process.StartTime.ToUniversalTime().Ticks == pending.ProcessStartTicks)
                        throw new HistoryException("A downloader from an interrupted GUI session is still running. Wait for it to exit before validating or rebuilding history.");
                }
            }
            catch (ArgumentException) { /* The recorded process no longer exists. */ }
        }

        public HistoryReport Inspect() {
            CheckOpen();
            CheckPending();
            if (Exists(MigrationPath)) throw new HistoryException("A filename migration was interrupted. Recover the migration before rebuilding history.");
            var report = new HistoryReport();
            var checkpoint = LoadCheckpoint();
            var current = new HashSet<string>(StringComparer.Ordinal);
            report.ArchiveMissing = !Exists(ArchivePath);
            if (!report.ArchiveMissing) {
                try { current = ReadArchive(ArchivePath); }
                catch (Exception ex) when (ex is HistoryException || ex is DecoderFallbackException) { report.ArchiveDamaged = true; }
            }
            report.Entries.UnionWith(current);
            if ((report.ArchiveMissing || report.ArchiveDamaged) && Exists(ArchivePath + ".bak")) {
                try { report.Entries.UnionWith(ReadArchive(ArchivePath + ".bak")); report.BackupUsed = true; }
                catch (Exception ex) when (ex is HistoryException || ex is DecoderFallbackException) { /* Never trust a malformed backup. */ }
            }
            report.Entries.UnionWith(checkpoint.Entries.Select(e => HistoryIdentity.Parse(e).Entry));
            var authoritative = new HashSet<string>(report.Entries, StringComparer.Ordinal);
            var prior = checkpoint.Files.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
            var retries = LoadRetries();
            foreach (var record in retries.Files) prior[record.Path] = record;
            var retryIds = new HashSet<string>(checkpoint.Files.Where(r => !r.Complete && r.Entry != null).Select(r => r.Entry), StringComparer.Ordinal);
            retryIds.UnionWith(retries.Entries);
            var known = authoritative.Select(HistoryIdentity.Parse).GroupBy(k => k.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            bool interrupted = Exists(PendingPath) || Exists(LibraryPendingPath);
            var onDisk = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in EnumerateLibrary()) {
                if (IsServiceFile(path)) continue;
                if (Temporary(path)) { report.Incomplete.Add(path); continue; }
                string ext = Path.GetExtension(path);
                if (!MediaExtensions.Contains(ext)) {
                    if (!SidecarExtensions.Contains(ext)) report.Unresolved.Add("Unrecognized file type: " + path);
                    continue;
                }
                string relative = path.Substring(LibraryPrefix.Length);
                var file = new FileInfo(path);
                if (file.Length == 0 || Exists(path + ".part") || Exists(path + ".ytdl")) { report.Incomplete.Add(path); continue; }
                string stem = Path.GetFileNameWithoutExtension(path);
                string metadataPath = Path.Combine(Path.GetDirectoryName(path), stem + ".info.json");
                HistoryIdentity identity = null;
                bool metadataFound = false;
                if (options.UseInfoJson && Exists(metadataPath)) {
                    try {
                        var metadata = ReadJson<HistoryMetadata>(metadataPath, 16 * 1024 * 1024);
                        if (metadata == null || (!string.IsNullOrEmpty(metadata.Type) && metadata.Type != "video")) throw new HistoryException("Metadata is not an individual media record.");
                        string key = metadata.ExtractorKey;
                        // extractor and extractor_key are not interchangeable for every provider.
                        if (string.IsNullOrWhiteSpace(key) && metadata.Extractor == "youtube") key = "youtube";
                        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(metadata.Id)) throw new HistoryException("Metadata lacks the canonical extractor_key/source ID.");
                        identity = HistoryIdentity.Parse(key + " " + metadata.Id);
                        metadataFound = true;
                    }
                    catch (Exception ex) when (ex is SerializationException || ex is HistoryException || ex is System.Xml.XmlException) {
                        report.Unresolved.Add("Invalid/ambiguous metadata for " + path + ": " + ex.Message);
                        continue;
                    }
                }
                HistoryRecord previous;
                prior.TryGetValue(relative, out previous);
                bool checkpointMatched = previous != null && previous.Complete && previous.Length == file.Length
                    && previous.WriteTicks == file.LastWriteTimeUtc.Ticks && previous.Entry != null;
                if (identity == null && checkpointMatched) identity = HistoryIdentity.Parse(previous.Entry);
                var marker = Regex.Match(stem, @"\[(?<extractor>[A-Za-z0-9_.:+-]+) (?<id>[^\[\]\r\n]+)\]$");
                if (marker.Success) {
                    var embedded = HistoryIdentity.Parse(marker.Groups["extractor"].Value + " " + marker.Groups["id"].Value);
                    if (identity != null && identity.Entry != embedded.Entry) { report.Unresolved.Add("Filename/metadata identity conflict: " + path); continue; }
                    identity = embedded;
                }
                if (identity == null) {
                    // A bare ID does not identify a website. Only unique trusted archive evidence or
                    // an explicit legacy-YouTube declaration can supply the missing namespace.
                    var matches = ResolveSuffix(stem, known).Take(2).ToArray();
                    if (matches.Length == 1) identity = matches[0];
                    else if (matches.Length > 1) { report.Unresolved.Add("Ambiguous extractor namespace: " + path); continue; }
                    else if (options.LegacyExtractor == "youtube") {
                        Match youtube = Regex.Match(stem, @"(?:^|[-\[ ])(?<id>[A-Za-z0-9_-]{11})\]?$", RegexOptions.RightToLeft);
                        if (youtube.Success) identity = HistoryIdentity.Parse("youtube " + youtube.Groups["id"].Value);
                    }
                }
                if (identity == null) {
                    if (interrupted || (previous != null && !previous.Complete)) {
                        report.Incomplete.Add(path);
                        report.Media.Add(Record(relative, null, file, false));
                    }
                    else report.Unresolved.Add("No authoritative source identity: " + path);
                    continue;
                }
                bool trustedCompleted = authoritative.Contains(identity.Entry);
                bool retryOnly = (interrupted && (previous == null || !previous.Complete || previous.Length != file.Length || previous.WriteTicks != file.LastWriteTimeUtc.Ticks))
                    || retryIds.Contains(identity.Entry) || (previous != null && !previous.Complete);
                if (retryOnly && !trustedCompleted) {
                    report.Incomplete.Add(path);
                    report.Media.Add(Record(relative, identity.Entry, file, false));
                    continue;
                }
                if (metadataFound) report.MetadataIdentified++;
                bool embedsId = metadataFound || checkpointMatched
                    ? stem.IndexOf(identity.Id, StringComparison.Ordinal) >= 0 : HasId(stem, identity.Id);
                if (!embedsId) {
                    if (metadataFound) report.Migrations.Add(new HistoryMigration { MediaPath = path, MetadataPath = metadataPath, Identity = identity });
                    else report.Unresolved.Add("Media filename does not contain its source ID: " + path);
                    continue;
                }
                report.EmbeddedIds++;
                report.Entries.Add(identity.Entry);
                onDisk.Add(identity.Entry);
                report.Media.Add(Record(relative, identity.Entry, file, true));
            }
            report.HistoricalOnly = report.Entries.Count(e => !onDisk.Contains(e));
            report.AddedEntries = report.Entries.Count(e => !current.Contains(e));
            return report;
        }
        private static HistoryRecord Record(string relative, string entry, FileInfo file, bool complete) {
            return new HistoryRecord { Path = relative, Entry = entry, Length = file.Length, WriteTicks = file.LastWriteTimeUtc.Ticks, Complete = complete };
        }
        private static bool HasId(string stem, string id) {
            return !string.IsNullOrEmpty(id) && (stem == id || stem.EndsWith("-" + id, StringComparison.Ordinal)
                || stem.EndsWith("[" + id + "]", StringComparison.Ordinal) || stem.EndsWith(" " + id + "]", StringComparison.Ordinal));
        }
        private static IEnumerable<HistoryIdentity> ResolveSuffix(string stem, Dictionary<string, HistoryIdentity[]> known) {
            var found = new HashSet<string>(StringComparer.Ordinal);
            string bare = stem.EndsWith("]", StringComparison.Ordinal) ? stem.Substring(0, stem.Length - 1) : stem;
            for (int i = 0; i < bare.Length; i++) {
                if (i > 0 && bare[i - 1] != '-' && bare[i - 1] != '[' && bare[i - 1] != ' ') continue;
                HistoryIdentity[] matches;
                if (known.TryGetValue(bare.Substring(i), out matches)) {
                    foreach (var match in matches) if (HasId(stem, match.Id) && found.Add(match.Entry)) yield return match;
                }
            }
        }
        public HistoryReport Reconcile(bool explicitRebuild = false) {
            HistoryReport report = Inspect();
            if (!report.Recoverable) throw new HistoryException("Download History is not safe to enable. " + report.Summary);
            if (report.ArchiveMissing && options.StopWhenMissing && !explicitRebuild
                && (report.TotalMedia > 0 || report.Entries.Count > 0 || Exists(StatePath))) {
                throw new HistoryException("The archive is missing. The selected policy requires an explicit rebuild before downloading.");
            }
            if (report.ArchiveMissing && Exists(StatePath) && report.Entries.Count == 0 && report.TotalMedia == 0 && !explicitRebuild) {
                throw new HistoryException("Previously used history is missing and there is no recoverable evidence. Review and explicitly rebuild the empty library.");
            }
            CheckCancellation();
            // Failed-file evidence belongs to the physical library, even when a custom
            // archive moves between roots. Save it before advancing the archive checkpoint.
            SaveRetries(report);
            string text = string.Concat(report.Entries.OrderBy(e => e, StringComparer.Ordinal).Select(e => e + "\n"));
            if (report.ArchiveDamaged) File.Copy(ArchivePath, ArchivePath + ".corrupt-" + Guid.NewGuid().ToString("N"));
            if (options.KeepBackup && !report.ArchiveMissing && !report.ArchiveDamaged) {
                AtomicWrite(ArchivePath + ".bak", string.Concat(ReadArchive(ArchivePath).OrderBy(e => e, StringComparer.Ordinal).Select(e => e + "\n")));
            }
            AtomicWrite(ArchivePath, text);
            if (!ReadArchive(ArchivePath).SetEquals(report.Entries)) throw new HistoryException("Archive verification failed after replacement.");
            var state = new HistoryCheckpoint { LibraryPath = LibraryPath, SynchronizedUtc = DateTime.UtcNow.ToString("o"), Entries = report.Entries.ToList(), Files = report.Media };
            AtomicWrite(StatePath, Json(state));
            if (options.KeepBackup) AtomicWrite(ArchivePath + ".bak", text);
            report.ArchiveMissing = report.ArchiveDamaged = false;
            report.AddedEntries = 0;
            return report;
        }
        public void BeginRun() {
            Reconcile();
            // Once a child can exist, cancellation must not interrupt its durable postflight.
            options.Cancelled = null;
            string pending = Json(new HistoryPending { ArchivePath = ArchivePath });
            AtomicWrite(LibraryPendingPath, pending);
            AtomicWrite(PendingPath, pending);
            ownsPending = true;
        }
        public void TrackProcess(System.Diagnostics.Process process) {
            if (!ownsPending) throw new InvalidOperationException("No protected run was prepared.");
            var pending = new HistoryPending { ArchivePath = ArchivePath, ProcessId = -1 };
            if (!process.HasExited) { pending.ProcessId = process.Id; pending.ProcessStartTicks = process.StartTime.ToUniversalTime().Ticks; }
            AtomicWrite(PendingPath, Json(pending));
            AtomicWrite(LibraryPendingPath, Json(pending));
        }
        // The caller must stop/reap the child before calling this or releasing the lease.
        public HistoryReport FinishRun() {
            if (!ownsPending) throw new InvalidOperationException("No protected run is owned.");
            HistoryReport report = Reconcile(true);
            File.Delete(PendingPath);
            File.Delete(LibraryPendingPath);
            ownsPending = false;
            return report;
        }
        public void RequireManualRecovery() {
            CheckOpen();
            string pending = Json(new HistoryPending { ArchivePath = ArchivePath });
            AtomicWrite(LibraryPendingPath, pending);
            AtomicWrite(PendingPath, pending);
            ownsPending = false;
        }
        public void RecoverInterruptedLaunch() {
            CheckOpen();
            // Deliberate UI-confirmed action: the user must first stop every downloader.
            if (Exists(PendingPath) || Exists(LibraryPendingPath)) {
                foreach (string path in new[] { PendingPath, LibraryPendingPath }) {
                    if (!Exists(path)) continue;
                    HistoryPending pending = ReadJson<HistoryPending>(path, 8192);
                    if (pending == null || pending.Version != 1) throw new HistoryException("Invalid interrupted-run marker.");
                    if (pending.ProcessId != 0) CheckPendingFile(path);
                    else if (!string.IsNullOrEmpty(pending.ArchivePath) && !string.Equals(pending.ArchivePath, ArchivePath, StringComparison.OrdinalIgnoreCase))
                        throw new HistoryException("Recover the interrupted run with its original archive: " + pending.ArchivePath);
                }
                ownsPending = true;
                FinishRun();
            }
        }
        public HistoryReport Migrate() {
            HistoryReport report = Inspect();
            if (report.Unresolved.Count > 0) throw new HistoryException("Resolve unidentified files before migrating. " + report.Summary);
            var moves = new List<HistoryMove>();
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plannedSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var migration in report.Migrations) {
                string oldStem = Path.Combine(Path.GetDirectoryName(migration.MediaPath), Path.GetFileNameWithoutExtension(migration.MediaPath));
                string newStem = oldStem + "-" + migration.Identity.Id;
                if (migration.Identity.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new HistoryException("The source ID cannot be embedded safely in a Windows filename.");
                foreach (string source in Directory.EnumerateFiles(Path.GetDirectoryName(oldStem), Path.GetFileName(oldStem) + ".*")) {
                    if (source != migration.MediaPath && !SidecarExtensions.Contains(Path.GetExtension(source))) continue;
                    string destination = newStem + source.Substring(oldStem.Length);
                    string existingDestination;
                    if (plannedSources.TryGetValue(source, out existingDestination)) {
                        if (existingDestination == destination) continue;
                        throw new HistoryException("Conflicting metadata for migration: " + source);
                    }
                    plannedSources.Add(source, destination);
                    if (!targets.Add(destination) || File.Exists(destination) || Directory.Exists(destination)) throw new HistoryException("Migration would overwrite a file: " + destination);
                    moves.Add(new HistoryMove { Source = source, Destination = destination });
                }
            }
            if (moves.Count == 0) return Reconcile(true);
            AtomicWrite(MigrationPath, Json(moves));
            try {
                foreach (var move in moves) File.Move(move.Source, move.Destination);
                File.Delete(MigrationPath);
                return Reconcile(true);
            }
            catch {
                // Keep the journal if rollback itself fails. Never overwrite either pathname.
                if (!Exists(MigrationPath)) AtomicWrite(MigrationPath, Json(moves));
                RecoverMigration();
                throw;
            }
        }
        public void RecoverMigration() {
            CheckOpen();
            if (!Exists(MigrationPath)) return;
            var moves = ReadJson<List<HistoryMove>>(MigrationPath, 128 * 1024 * 1024);
            if (moves == null) throw new HistoryException("Invalid migration journal.");
            foreach (var move in moves) {
                if (move == null || !WithinLibrary(move.Source) || !WithinLibrary(move.Destination)) throw new HistoryException("Unsafe path in migration journal.");
                RejectReparse(move.Source, false);
                RejectReparse(move.Destination, false);
                if (Exists(move.Source) == Exists(move.Destination)) throw new HistoryException("Migration recovery needs manual resolution; both or neither filenames exist.");
            }
            foreach (var move in moves.AsEnumerable().Reverse()) if (Exists(move.Destination)) File.Move(move.Destination, move.Source);
            File.Delete(MigrationPath);
        }
        private bool WithinLibrary(string path) {
            return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)
                && Path.GetFullPath(path).StartsWith(LibraryPrefix, StringComparison.OrdinalIgnoreCase);
        }
        public void Reset() {
            CheckOpen();
            CheckPending();
            if (Exists(PendingPath) || Exists(LibraryPendingPath) || Exists(MigrationPath)) throw new HistoryException("Recover the interrupted operation before resetting history.");
            // Reset is separate from disable and retains the old files for deliberate recovery.
            string suffix = ".reset-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N");
            foreach (string path in new[] { ArchivePath, ArchivePath + ".bak", StatePath }) if (Exists(path)) File.Move(path, path + suffix);
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true;
            for (int i = locks.Count - 1; i >= 0; i--) locks[i].Dispose();
            locks.Clear();
            // Persistent lock files are intentional; deleting them creates a lock/unlink race.
        }
    }
}

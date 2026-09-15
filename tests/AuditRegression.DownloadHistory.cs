using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    private sealed class DownloadHistoryFixture : IDisposable {
        private readonly Type downloads;
        private readonly Type history;
        private readonly object snapshot;
        private readonly string oldDownloadPath;
        private readonly string oldFileNameSchema;
        private readonly int oldYtdlType;
        private readonly string oldKnownFileNameSchemas;
        public readonly string Root;

        public DownloadHistoryFixture(bool createRoot) {
            downloads = T("youtube_dl_gui.Downloads");
            history = T("youtube_dl_gui.DownloadHistory");
            snapshot = Call(history, null, "CaptureSettings");
            oldDownloadPath = (string)downloads.GetProperty("downloadPath", All).GetValue(null, null);
            oldFileNameSchema = (string)downloads.GetProperty("fileNameSchema", All).GetValue(null, null);
            oldYtdlType = (int)downloads.GetProperty("YtdlType", All).GetValue(null, null);
            oldKnownFileNameSchemas = (string)history.GetField("fKnownFileNameSchemas", All).GetValue(null);

            Root = Path.Combine(Environment.CurrentDirectory, "download-history-" + Guid.NewGuid().ToString("N"));
            if (createRoot) Directory.CreateDirectory(Root);
            Set(downloads, null, "downloadPath", Root);
            Set(downloads, null, "fileNameSchema", "%(title)s-%(id)s.%(ext)s");
            Set(downloads, null, "YtdlType", 0); // yt-dlp

            history.GetField("fEnabled", All).SetValue(null, false);
            history.GetField("fArchivePath", All).SetValue(null, string.Empty);
            history.GetField("fKeepBackup", All).SetValue(null, true);
            history.GetField("fFailIfUnavailable", All).SetValue(null, true);
            history.GetField("fEverEnabled", All).SetValue(null, false);
            history.GetField("fNeedsReconciliation", All).SetValue(null, false);
            history.GetField("fBoundLibraryRoot", All).SetValue(null, string.Empty);
            history.GetField("fBoundArchivePath", All).SetValue(null, string.Empty);
            history.GetField("fKnownFileNameSchemas", All).SetValue(null, string.Empty);
            history.GetField("PreparedKey", All).SetValue(null, null);
        }

        public string Archive { get { return Path.Combine(Root, "yt-dlp-archive.txt"); } }
        public Type History { get { return history; } }
        public Type Downloads { get { return downloads; } }

        public void Dispose() {
            try { Call(history, null, "RestoreSettings", snapshot); } catch { }
            try { Set(downloads, null, "downloadPath", oldDownloadPath); } catch { }
            try { Set(downloads, null, "fileNameSchema", oldFileNameSchema); } catch { }
            try { Set(downloads, null, "YtdlType", oldYtdlType); } catch { }
            try { history.GetField("fKnownFileNameSchemas", All).SetValue(null, oldKnownFileNameSchemas); } catch { }
            try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { }
        }
    }

    private static string DownloadHistoryStateName(object report) {
        return Get(report, "State").ToString();
    }

    private static bool DownloadHistoryCanReconcile(object report) {
        return (bool)Get(report, "CanReconcile");
    }

    private static string[] DownloadHistoryArchiveLines(string path) {
        if (!File.Exists(path)) return new string[0];
        return File.ReadAllLines(path).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
    }

    private static string DownloadHistoryWriteMedia(string root, string name) {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "fixture", Encoding.UTF8);
        return path;
    }

    private static string DownloadHistoryWriteMediaWithInfo(string root, string name, string extractorKey, string id) {
        string media = DownloadHistoryWriteMedia(root, name);
        string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
        File.WriteAllText(stem + ".info.json", "{\"id\":\"" + id + "\",\"extractor_key\":\"" + extractorKey + "\"}", Encoding.UTF8);
        return media;
    }

    private static object DownloadHistoryReconcile(DownloadHistoryFixture fixture, string configuredArchive, bool allowMigration) {
        object report = Call(fixture.History, null, "ReconcileLibrary", configuredArchive, true, allowMigration);
        Equal("Healthy", DownloadHistoryStateName(report));
        return report;
    }

    private static object DownloadHistoryEnable(DownloadHistoryFixture fixture, string configuredArchive) {
        object report = DownloadHistoryReconcile(fixture, configuredArchive, true);
        Call(fixture.History, null, "CommitSettings", true, configuredArchive, true, report);
        Equal(true, fixture.History.GetProperty("Enabled", All).GetValue(null, null));
        return report;
    }

    private static bool DownloadHistoryArguments(Type history, string schema, string custom, out string arguments, out string error, out object execution) {
        object[] call = { schema, custom, null, null, null };
        bool result = (bool)Call(history, null, "TryGetArchiveArguments", call);
        arguments = (string)call[2];
        error = (string)call[3];
        execution = call[4];
        return result;
    }

    private static void DownloadHistoryNeverEnabledIsNoOp() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(false)) {
            Set(fixture.Downloads, null, "fileNameSchema", "%(title)s.%(ext)s");
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s.%(ext)s", null, out arguments, out error, out execution));
            Equal(string.Empty, arguments);
            Equal(null, execution);
            Require(!Directory.Exists(fixture.Root), "Never-enabled Download History created the media library or archive");

            Set(fixture.Downloads, null, "fileNameSchema", "%(title)s-%(id)s.%(ext)s");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(analysis));
            Equal(true, DownloadHistoryCanReconcile(analysis));
            Require(!Directory.Exists(fixture.Root), "Dry-run validation created a new library directory");

            object reconciled = DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal(0, Get(reconciled, "ArchiveEntries"));
            Require(Directory.Exists(fixture.Root), "Explicit reconciliation did not initialize a new default library directory");
            Require(File.Exists(fixture.Archive), "Explicit reconciliation did not create the initial archive");
        }
    }

    private static void DownloadHistoryTemplateAndArguments() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            Equal(true, Call(fixture.History, null, "HasRequiredIdTemplate", "%(uploader)s\\%(title)s-%(id)s.%(ext)s"));
            Equal(false, Call(fixture.History, null, "HasRequiredIdTemplate", "%(id)s\\%(title)s.%(ext)s"));
            Equal("%(uploader)s\\%(title)s-%(id)s.%(ext)s",
                Call(fixture.History, null, "AddRequiredIdTemplate", "%(uploader)s\\%(title)s.%(ext)s"));

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(arguments.Contains("--download-archive \"" + fixture.Archive + "\""), "Native archive path was not generated");
            Require(arguments.Contains("--no-break-on-existing"), "Safe collection traversal option was not generated");
            Require(arguments.IndexOf("--break-on-existing", StringComparison.Ordinal) < 0, "Unsafe break-on-existing was generated");
            Require(execution != null, "Protected arguments did not return an execution context");

            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--download-archive=other.txt", out arguments, out error, out execution));
            Require(error.Contains("owns --download-archive"), "Conflicting custom archive argument was not rejected clearly");
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "\"--download-archive\" \"other.txt\"", out arguments, out error, out execution));
            Require(error.Contains("owns --download-archive"), "Quoted custom archive option token bypassed native archive ownership");
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--break-on-existing", out arguments, out error, out execution));
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "\"--break-on-existing\"", out arguments, out error, out execution));
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--break-per-input", out arguments, out error, out execution));
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "\"--break-per-input\"", out arguments, out error, out execution));
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s.%(ext)s", null, out arguments, out error, out execution));

            Set(fixture.Downloads, null, "YtdlType", 2); // youtube-dl, not yt-dlp
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Set(fixture.Downloads, null, "YtdlType", 0);
        }
    }

    private static void DownloadHistoryRejectsCustomArgumentsThatBreakProtection() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string[] unsafeArguments = {
                "-o custom-%(title)s.%(ext)s",
                "-ocustom-%(id)s.%(ext)s",
                "--output=custom-%(id)s.%(ext)s",
                "-P elsewhere",
                "-Pelsewhere",
                "--paths=home:elsewhere",
                "--force-write-archive",
                "--force-write-download-archive",
                "--force-download-archive",
                "--no-part",
                "--trim-filenames 80",
                "--trim-file-names 80",
                "--break-per-in",
                "--download-arch legacy.txt",
                "-- https://example.invalid/video",
                "\"--\" https://example.invalid/video"
            };
            foreach (string custom in unsafeArguments) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(!string.IsNullOrEmpty(error), "Unsafe custom argument was rejected without an explanation: " + custom);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--write-info-json --skip-download -p secret -O %(id)s", out arguments, out error, out execution));
            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "-o custom.%(ext)s --no-part --force-write-archive", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRecoversSupportedMediaExtensions() {
        string[] extensions = { ".f4v", ".mk3d", ".divx", ".ogv", ".f4a", ".f4b", ".m4r", ".ogx", ".spx", ".vorbis", ".weba", ".nut", ".swf", ".mp2", ".tta", ".aifc" };
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string[] ids = new string[extensions.Length];
            for (int i = 0; i < extensions.Length; i++) {
                ids[i] = ((char)('A' + i)).ToString() + "1234567890";
                DownloadHistoryWriteMedia(fixture.Root, "Media-" + ids[i] + extensions[i]);
            }

            object enabled = DownloadHistoryEnable(fixture, string.Empty);
            Equal(extensions.Length, Get(enabled, "ArchiveEntries"));
            File.Delete(fixture.Archive);
            if (File.Exists(fixture.Archive + ".bak")) File.Delete(fixture.Archive + ".bak");

            object rebuilt = DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal(extensions.Length, Get(rebuilt, "ArchiveEntries"));
            string[] archiveLines = DownloadHistoryArchiveLines(fixture.Archive);
            foreach (string id in ids) Require(archiveLines.Contains("youtube " + id), "Archive rebuild omitted supported media extension for ID " + id);
        }
    }

    private static void DownloadHistoryRebuildsDeletedArchiveFromIds() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Video-" + id + ".webm");
            DownloadHistoryWriteMedia(fixture.Root, "Video-copy-" + id + ".mp4");
            object enabled = DownloadHistoryEnable(fixture, string.Empty);
            Equal(1, Get(enabled, "ArchiveEntries"));
            Equal(1, DownloadHistoryArchiveLines(fixture.Archive).Length);

            File.Delete(fixture.Archive);
            if (File.Exists(fixture.Archive + ".bak")) File.Delete(fixture.Archive + ".bak");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(analysis));
            Equal(true, DownloadHistoryCanReconcile(analysis));
            object rebuilt = DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal(1, Get(rebuilt, "ArchiveEntries"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }
    }

    private static void DownloadHistoryRecoversHistoricalProtectedSchemas() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Set(fixture.Downloads, null, "fileNameSchema", "%(id)s--%(title)s.%(ext)s");
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(id)s--%(title)s.%(ext)s", null, out arguments, out error, out execution));
            DownloadHistoryWriteMedia(fixture.Root, id + "--historical-title.mp4");

            Set(fixture.Downloads, null, "fileNameSchema", "NEW-%(id)s.%(ext)s");
            File.Delete(fixture.Archive);
            File.Delete(fixture.Archive + ".bak");
            object rebuilt = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, true);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Require(DownloadHistoryArchiveLines(fixture.Archive).Contains("youtube " + id), "Archive loss could not recover a file written with an earlier protected filename schema");
        }
    }

    private static void DownloadHistoryUnderstandsIdPlacementFromSchema() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Set(fixture.Downloads, null, "fileNameSchema", "%(id)s--%(title)s.%(ext)s");
            DownloadHistoryWriteMedia(fixture.Root, id + "--Title.webm");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal(1, Get(analysis, "FilenameRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
            DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }
    }

    private static void DownloadHistoryMigratesLegacyMetadata() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string original = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Legacy Title.mp4", "Youtube", id);
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Migratable", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "MigrationCount"));
            Equal(1, Get(analysis, "MetadataRecovered"));

            object declined = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, false);
            Equal("Migratable", DownloadHistoryStateName(declined));
            Require(File.Exists(original), "Migration occurred without explicit approval");
            Require(!File.Exists(fixture.Archive), "Archive was written before migration approval");

            object migrated = DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal(1, Get(migrated, "MigrationCount"));
            string migratedMedia = Path.Combine(fixture.Root, "Legacy Title-" + id + ".mp4");
            Require(File.Exists(migratedMedia), "Legacy media was not renamed with its authoritative ID");
            Require(File.Exists(Path.Combine(fixture.Root, "Legacy Title-" + id + ".info.json")), "Legacy metadata did not follow the migrated media filename");
            Require(!File.Exists(original), "Original legacy media path remained after successful migration");
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }
    }

    private static void DownloadHistoryMigrationFailureRollsBackMedia() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Type migrationType = T("youtube_dl_gui.DownloadHistoryMigration");
            Type listType = typeof(List<>).MakeGenericType(migrationType);
            System.Collections.IList migrations = (System.Collections.IList)Activator.CreateInstance(listType);
            object migration = Activator.CreateInstance(migrationType, true);
            string mediaSource = DownloadHistoryWriteMedia(fixture.Root, "Legacy.mp4");
            string metadataSource = Path.Combine(fixture.Root, "Legacy.info.json");
            File.WriteAllText(metadataSource, "{}", Encoding.UTF8);
            string mediaTarget = Path.Combine(fixture.Root, "Legacy-9qFjkwAElDs.mp4");
            string metadataTarget = Path.Combine(fixture.Root, "missing-directory", "Legacy-9qFjkwAElDs.info.json");
            Set(migrationType, migration, "MediaSource", mediaSource);
            Set(migrationType, migration, "MediaTarget", mediaTarget);
            Set(migrationType, migration, "MetadataSource", metadataSource);
            Set(migrationType, migration, "MetadataTarget", metadataTarget);
            migrations.Add(migration);

            Throws<DirectoryNotFoundException>(() => Call(fixture.History, null, "ApplyMigrations", migrations));
            Require(File.Exists(mediaSource), "Failed migration did not restore the original media path");
            Require(File.Exists(metadataSource), "Failed migration did not preserve the original metadata path");
            Require(!File.Exists(mediaTarget), "Failed migration left the media at the migrated path");
        }
    }

    private static void DownloadHistoryBlocksPartialAndUnsafeLibraries() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Known-" + id + ".mp4");
            DownloadHistoryWriteMedia(fixture.Root, "Unknown.mp4");
            object partial = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Partial", DownloadHistoryStateName(partial));
            Equal(false, DownloadHistoryCanReconcile(partial));
            Require(!File.Exists(fixture.Archive), "Partial library unexpectedly initialized an archive");

            File.Delete(Path.Combine(fixture.Root, "Known-" + id + ".mp4"));
            for (int i = 0; i < 2048; i++) DownloadHistoryWriteMedia(fixture.Root, "unknown\\item-" + i.ToString("D4") + ".mp4");
            object unsafeReport = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Unsafe", DownloadHistoryStateName(unsafeReport));
            Equal(false, DownloadHistoryCanReconcile(unsafeReport));
            Require((int)Get(unsafeReport, "UnresolvedMedia") >= 2049, "Large unidentified library was not counted as unresolved");
            Require(!File.Exists(fixture.Archive), "Unsafe library silently created an empty archive");
        }
    }

    private static void DownloadHistoryUnexpectedLossRequiresExplicitReset() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            object report = DownloadHistoryEnable(fixture, string.Empty);
            Require(File.Exists(fixture.Archive), "Initial archive was not created");
            File.Delete(fixture.Archive);
            if (File.Exists(fixture.Archive + ".bak")) File.Delete(fixture.Archive + ".bak");

            object missing = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(missing));
            Equal(false, DownloadHistoryCanReconcile(missing));

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Call(fixture.History, null, "ResetHistory");
            Equal(false, fixture.History.GetProperty("EverEnabled", All).GetValue(null, null));
            object resetState = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(resetState));
            Equal(true, DownloadHistoryCanReconcile(resetState));
        }
    }

    private static void DownloadHistoryBackupRefreshRejectsCorruption() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            File.WriteAllText(fixture.Archive, "youtube 9qFjkwAElDs\r\nyoutube aB_Cd-Ef123\r\n", Encoding.UTF8);
            Call(fixture.History, null, "RefreshBackupAfterRun");
            string backupBefore = File.ReadAllText(fixture.Archive + ".bak", Encoding.UTF8);
            Require(backupBefore.Contains("youtube aB_Cd-Ef123"), "Successful native archive append was not copied to backup");

            File.WriteAllText(fixture.Archive, "youtube poisoned123\r\ninvalid-line-without-space\r\n", Encoding.UTF8);
            Call(fixture.History, null, "RefreshBackupAfterRun");
            Equal(backupBefore, File.ReadAllText(fixture.Archive + ".bak", Encoding.UTF8));

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            string repaired = File.ReadAllText(fixture.Archive, Encoding.UTF8);
            Require(repaired.Contains("youtube 9qFjkwAElDs") && repaired.Contains("youtube aB_Cd-Ef123"), "Valid backup was not restored after primary corruption");
            Require(repaired.IndexOf("poisoned123", StringComparison.Ordinal) < 0, "Corrupt primary content survived backup recovery");
        }
    }

    private static void DownloadHistoryValidArchiveTruncationPreservesBackup() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            File.WriteAllText(fixture.Archive, "youtube oldEntry001\r\n", Encoding.UTF8);
            Call(fixture.History, null, "RefreshBackupAfterRun");
            Require(DownloadHistoryArchiveLines(fixture.Archive + ".bak").Contains("youtube oldEntry001"), "Known-good archive entry was not backed up");

            File.WriteAllText(fixture.Archive, "youtube newEntry002\r\n", Encoding.UTF8);
            Call(fixture.History, null, "RefreshBackupAfterRun");
            string[] preserved = DownloadHistoryArchiveLines(fixture.Archive + ".bak");
            Require(preserved.Contains("youtube oldEntry001") && !preserved.Contains("youtube newEntry002"), "A truncated primary archive overwrote the last good backup");

            object report = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, true);
            Equal("Healthy", DownloadHistoryStateName(report));
            string[] repaired = DownloadHistoryArchiveLines(fixture.Archive);
            Require(repaired.Contains("youtube oldEntry001") && repaired.Contains("youtube newEntry002"), "Reconciliation did not union the valid primary archive with its last good backup");
            string[] refreshed = DownloadHistoryArchiveLines(fixture.Archive + ".bak");
            Require(refreshed.Contains("youtube oldEntry001") && refreshed.Contains("youtube newEntry002"), "Reconciliation did not refresh the backup after repairing archive truncation");
        }
    }

    private static void DownloadHistoryCorruptArchiveDoesNotTrustPartialLines() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Recovered-" + id + ".mp4");
            File.WriteAllText(fixture.Archive, "youtube poisoned123\r\ninvalid-line-without-space\r\n", Encoding.UTF8);
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Invalid", DownloadHistoryStateName(analysis));
            Equal(true, DownloadHistoryCanReconcile(analysis));
            DownloadHistoryReconcile(fixture, string.Empty, true);
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            Equal(1, lines.Length);
            Equal("youtube " + id, lines[0]);
            Require(lines.All(x => x.IndexOf("poisoned123", StringComparison.Ordinal) < 0), "Parser trusted entries preceding a corrupt archive line");
        }
    }

    private static void DownloadHistoryDisableReenableReconcilesChanges() {
        const string first = "9qFjkwAElDs";
        const string second = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "First-" + first + ".mp4");
            DownloadHistoryEnable(fixture, string.Empty);
            string beforeDisable = File.ReadAllText(fixture.Archive, Encoding.UTF8);
            Require(File.Exists(fixture.Archive + ".bak"), "Backup was not preserved before disable test");

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Equal(string.Empty, arguments);
            Equal(beforeDisable, File.ReadAllText(fixture.Archive, Encoding.UTF8));
            Require(File.Exists(fixture.Archive + ".bak"), "Disabling history deleted the backup");
            Equal("Dormant", fixture.History.GetProperty("LastReport", All).GetValue(null, null).GetType().GetProperty("State", All).GetValue(fixture.History.GetProperty("LastReport", All).GetValue(null, null), null).ToString());

            DownloadHistoryWriteMedia(fixture.Root, "Second-" + second + ".webm");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(analysis));
            object reconciled = DownloadHistoryReconcile(fixture, string.Empty, true);
            Call(fixture.History, null, "CommitSettings", true, string.Empty, true, reconciled);
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            Require(lines.Contains("youtube " + first) && lines.Contains("youtube " + second), "Re-enable did not reconcile media downloaded while history was disabled");
        }
    }

    private static void DownloadHistoryValidArchiveDoesNotPromoteUnarchivedFile() {
        const string trusted = "9qFjkwAElDs";
        const string failed = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Trusted-" + trusted + ".mp4");
            DownloadHistoryEnable(fixture, string.Empty);
            DownloadHistoryWriteMedia(fixture.Root, "Failed-looking-" + failed + ".mp4");

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, true);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            Require(lines.Contains("youtube " + trusted), "Trusted archive entry disappeared");
            Require(!lines.Contains("youtube " + failed), "Unarchived final-looking file was promoted into trusted history");
        }
    }

    private static void DownloadHistoryIgnoresFailedAndSidecarFiles() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            File.WriteAllText(Path.Combine(fixture.Root, "Video-9qFjkwAElDs.mp4.part"), "partial");
            File.WriteAllText(Path.Combine(fixture.Root, "Video-9qFjkwAElDs.mp4.ytdl"), "partial");
            File.WriteAllText(Path.Combine(fixture.Root, "Video-9qFjkwAElDs.info.json"), "{}", Encoding.UTF8);
            File.WriteAllText(Path.Combine(fixture.Root, "thumbnail.jpg"), "image");
            File.WriteAllText(Path.Combine(fixture.Root, "subtitle.vtt"), "subtitle");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal(0, Get(analysis, "CompletedMedia"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
            Equal(true, DownloadHistoryCanReconcile(analysis));
        }
    }

    private static void DownloadHistoryDisabledIntervalWithoutIdsFailsSafe() {
        const string trusted = "9qFjkwAElDs";
        const string recoverable = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Trusted-" + trusted + ".mp4");
            DownloadHistoryEnable(fixture, string.Empty);
            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Set(fixture.Downloads, null, "fileNameSchema", "%(title)s.%(ext)s");
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Recoverable.mp4", "Youtube", recoverable);
            DownloadHistoryWriteMedia(fixture.Root, "Unresolved.mp4");

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Partial", DownloadHistoryStateName(analysis));
            Equal(false, DownloadHistoryCanReconcile(analysis));
            Equal(1, Get(analysis, "MigrationCount"));
            Require(File.Exists(Path.Combine(fixture.Root, "Recoverable.mp4")), "Partial migration mutated media despite unresolved files");
            Require(!File.Exists(Path.Combine(fixture.Root, "Recoverable-" + recoverable + ".mp4")), "Partial migration renamed media before the library was safe");
        }
    }

    private static void DownloadHistoryMissingParentHardStopsWithoutPersistence() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string parent = Path.Combine(fixture.Root, "missing-parent");
            string custom = Path.Combine(parent, "history.txt");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", custom);
            Equal("Unavailable", DownloadHistoryStateName(analysis));
            Equal(false, DownloadHistoryCanReconcile(analysis));
            Require(!Directory.Exists(parent), "Validation silently created an unavailable custom archive parent");
            object reconcile = Call(fixture.History, null, "ReconcileLibrary", custom, true, true);
            Equal("Unavailable", DownloadHistoryStateName(reconcile));
            Require(!Directory.Exists(parent), "Reconciliation silently created an unavailable custom archive parent");
            Equal(string.Empty, fixture.History.GetProperty("ArchivePath", All).GetValue(null, null));
        }
    }

    private static void DownloadHistoryManagementFailsFastWhenBusy() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            IDisposable active = (IDisposable)Call(execution.GetType(), execution, "AcquireValidatedLease");
            try {
                object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
                Equal("Unavailable", DownloadHistoryStateName(analysis));
                Require(((string)Get(analysis, "Message")).Contains("another protected download"), "Busy validation did not fail fast with an actionable message");
                Throws<InvalidOperationException>(() => Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null));
                Equal(true, fixture.History.GetProperty("Enabled", All).GetValue(null, null));
            }
            finally { active.Dispose(); }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(false, fixture.History.GetProperty("Enabled", All).GetValue(null, null));
        }
    }

    private static void DownloadHistoryExecutionLeaseRejectsArchiveTruncation() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            File.WriteAllText(fixture.Archive, "youtube oldEntry001\r\n", Encoding.UTF8);
            Call(fixture.History, null, "RefreshBackupAfterRun");

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            File.WriteAllText(fixture.Archive, "youtube newEntry002\r\n", Encoding.UTF8);
            Throws<InvalidOperationException>(() => Call(execution.GetType(), execution, "AcquireValidatedLease"));

            object repairedExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out repairedExecution));
            string[] repaired = DownloadHistoryArchiveLines(fixture.Archive);
            Require(repaired.Contains("youtube oldEntry001") && repaired.Contains("youtube newEntry002"), "Regenerating a rejected command did not reconcile the truncated primary archive with its last-good backup");
            using (IDisposable lease = (IDisposable)Call(repairedExecution.GetType(), repairedExecution, "AcquireValidatedLease")) { }
        }
    }

    private static void DownloadHistoryExecutionContextRejectsSettingChanges() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(execution != null, "Protected command did not capture an archive execution context");

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Throws<InvalidOperationException>(() => Call(execution.GetType(), execution, "AcquireValidatedLease"));

            DownloadHistoryEnable(fixture, string.Empty);
            object reboundExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out reboundExecution));
            string otherDirectory = Path.Combine(fixture.Root, "other-ledger");
            Directory.CreateDirectory(otherDirectory);
            string otherArchive = Path.Combine(otherDirectory, "history.txt");
            object reboundReport = DownloadHistoryReconcile(fixture, otherArchive, true);
            Call(fixture.History, null, "CommitSettings", true, otherArchive, true, reboundReport);
            Throws<InvalidOperationException>(() => Call(reboundExecution.GetType(), reboundExecution, "AcquireValidatedLease"));
        }
    }

    // Legacy audit ID retained so guarded repair history never removes existing coverage.
    private static void DownloadHistoryExecutionContextSurvivesDisableButNotReset() {
        DownloadHistoryExecutionContextRejectsSettingChanges();
    }

    private static void DownloadHistoryLeaseSerializesWorkers() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryReconcile(fixture, string.Empty, true);
            IDisposable first = (IDisposable)Call(fixture.History, null, "AcquireArchiveLease");
            ManualResetEvent started = new ManualResetEvent(false);
            ManualResetEvent acquired = new ManualResetEvent(false);
            Exception workerError = null;
            Thread worker = new Thread(delegate() {
                started.Set();
                try {
                    using (IDisposable second = (IDisposable)Call(fixture.History, null, "AcquireArchiveLease")) acquired.Set();
                }
                catch (Exception ex) { workerError = ex; acquired.Set(); }
            });
            worker.IsBackground = true;
            try {
                worker.Start();
                Require(started.WaitOne(2000), "Second history worker did not start");
                Thread.Sleep(200);
                Require(!acquired.WaitOne(0), "Two workers acquired the same archive lease concurrently");
                first.Dispose();
                first = null;
                Require(acquired.WaitOne(3000), "Waiting history worker did not acquire the archive after release");
                Require(worker.Join(3000), "Waiting history worker did not finish");
                if (workerError != null) throw workerError;
            }
            finally {
                if (first != null) first.Dispose();
                started.Dispose();
                acquired.Dispose();
            }
        }
    }

    private static void DownloadHistoryCancellationIsRecheckedBeforeProcessStart() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string standard = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloader.cs"));
        string extended = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmExtendedDownloader.cs"));

        int standardLease = standard.IndexOf("using DownloadHistoryLease? DownloadLease = HistoryExecution?.AcquireValidatedLease();", StringComparison.Ordinal);
        int standardGuard = standard.IndexOf("CancellationRequested || CurrentDownload.Status == DownloadStatus.Aborted || CurrentDownload.Status == DownloadStatus.AbortForClose", standardLease, StringComparison.Ordinal);
        int standardStart = standard.IndexOf("DownloadProcess.Start();", standardLease, StringComparison.Ordinal);
        Require(standardLease >= 0 && standardGuard > standardLease && standardStart > standardGuard, "Standard worker can start after cancellation while waiting for the archive lease");

        int normalLease = extended.IndexOf("using DownloadHistoryLease? DownloadLease = HistoryExecution?.AcquireValidatedLease();", StringComparison.Ordinal);
        int normalGuard = extended.IndexOf("CancellationRequested || Status == DownloadStatus.Aborted || Status == DownloadStatus.AbortForClose", normalLease, StringComparison.Ordinal);
        int normalStart = extended.IndexOf("DownloadProcess.Start();", normalLease, StringComparison.Ordinal);
        Require(normalLease >= 0 && normalGuard > normalLease && normalStart > normalGuard, "Extended worker can start after cancellation while waiting for the archive lease");

        int batchLease = extended.IndexOf("using DownloadHistoryLease? DownloadLease = BatchHistoryExecution?.AcquireValidatedLease();", StringComparison.Ordinal);
        int batchGuard = extended.IndexOf("CancellationRequested || Status == DownloadStatus.Aborted || Status == DownloadStatus.AbortForClose", batchLease, StringComparison.Ordinal);
        int batchStart = extended.IndexOf("DownloadProcess.Start();", batchLease, StringComparison.Ordinal);
        Require(batchLease >= 0 && batchGuard > batchLease && batchStart > batchGuard, "Extended batch worker can start after cancellation while waiting for the archive lease");
    }

    private static void DownloadHistorySettingsRejectIdRemovalWhileEnabled() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            object report = DownloadHistoryEnable(fixture, string.Empty);
            Set(fixture.Downloads, null, "fileNameSchema", "%(title)s.%(ext)s");
            Throws<InvalidOperationException>(() => Call(fixture.History, null, "CommitSettings", true, string.Empty, true, report));

            string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
            string settingsSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmSettings.cs"));
            Require(settingsSource.Contains("DownloadHistory.Enabled && !DownloadHistory.HasRequiredIdTemplate"), "Main Settings no longer blocks ID removal while history is enabled");
            Require(settingsSource.Contains("DownloadHistory.EverEnabled") && settingsSource.Contains("Keeping media IDs in filenames is strongly recommended"), "Disabled-state ID removal warning is missing");
            Require(settingsSource.Contains("DownloadHistory.IsCurrentLibraryPath"), "Main Settings does not protect the library/archive binding when the download folder changes");
            Require(settingsSource.Contains("unsavedSchema") && settingsSource.Contains("Unsaved download settings"), "Opening Download History does not warn about an unsaved filename-format change");
            Require(settingsSource.Contains("history.ShowDialog(this) != DialogResult.OK"), "Cancelling Download History can overwrite unsaved filename-format UI state");
            Require(settingsSource.Contains("Download History requires yt-dlp or yt-dlp nightly") && settingsSource.Contains("selectedProvider is not"), "Main Settings does not prevent an incompatible provider switch while history is enabled");
            string historyDialogSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloadHistory.cs"));
            Require(historyDialogSource.Contains("Reset History operates only on the currently saved archive"), "Reset History does not guard against an unsaved archive-path change");
        }
    }

    private static void DownloadHistoryLibraryBindingPreventsCrossLibraryReuse() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ledgerDirectory = Path.Combine(fixture.Root, "ledger");
            Directory.CreateDirectory(ledgerDirectory);
            string custom = Path.Combine(ledgerDirectory, "history.txt");
            DownloadHistoryEnable(fixture, custom);

            string secondLibrary = Path.Combine(fixture.Root, "second-library");
            Directory.CreateDirectory(secondLibrary);
            Set(fixture.Downloads, null, "downloadPath", secondLibrary);
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", custom);
            Equal("Unsafe", DownloadHistoryStateName(analysis));
            Equal(false, DownloadHistoryCanReconcile(analysis));
            Require(((string)Get(analysis, "Message")).Contains("bound to a different media library"), "Cross-library archive reuse was not explained");
        }
    }

    private static void DownloadHistoryWorkersUsePreparedContext() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string standard = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloader.cs"));
        string extended = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmExtendedDownloader.cs"));
        Require(standard.Contains("HistoryExecution?.AcquireValidatedLease()"), "Standard downloader does not launch through its prepared archive context");
        Require(standard.Contains("HistoryExecution?.RefreshBackupAfterRun()"), "Standard downloader does not refresh archive backup after provider execution");
        Require(standard.IndexOf("DownloadHistory.Enabled ? DownloadHistory.AcquireArchiveLease()", StringComparison.Ordinal) < 0, "Standard downloader still binds locking to mutable live settings");
        Require(extended.Contains("HistoryExecution?.AcquireValidatedLease()") && extended.Contains("BatchHistoryExecution?.AcquireValidatedLease()"), "Extended downloader paths do not launch through prepared archive contexts");
        Require(extended.Contains("HistoryExecution?.RefreshBackupAfterRun()") && extended.Contains("BatchHistoryExecution?.RefreshBackupAfterRun()"), "Extended downloader paths do not refresh archive backups after provider execution");
        Require(extended.IndexOf("DownloadHistory.Enabled ? DownloadHistory.AcquireArchiveLease()", StringComparison.Ordinal) < 0, "Extended downloader still binds locking to mutable live settings");
    }

    private static void DownloadHistoryWiresStandardAndExtendedArguments() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string[] sources = {
                "https://www.youtube.com/watch?v=9qFjkwAElDs",
                "https://www.youtube.com/playlist?list=AUDIT",
                "https://www.youtube.com/ninjasexparty/videos"
            };
            for (int i = 0; i < sources.Length; i++) {
                object info = New("youtube_dl_gui.DownloadInfo", sources[i]);
                Set(info.GetType(), info, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
                Set(info.GetType(), info, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
                if (i > 0) Set(info.GetType(), info, "BatchDownload", true);
                Require((bool)Call(info.GetType(), info, "GenerateArguments", (Action<string>)(delegate(string ignored) { })), "Standard argument generation failed for archive-aware source " + i);
                string args = (string)Get(info, "Arguments");
                Require(args.Contains("--ignore-config"), "Standard protected input did not isolate ambient yt-dlp configuration");
                Require(args.Contains("--download-archive \"" + fixture.Archive + "\""), "Standard input did not use the shared library archive");
                Require(args.Contains("--no-break-on-existing"), "Standard collection path did not preserve full traversal");
                Require(Get(info, "DownloadHistoryExecution") != null, "Standard input did not retain its prepared history context");
            }

            object fallback = New("youtube_dl_gui.DownloadInfo", "https://www.youtube.com/watch?v=9qFjkwAElDs");
            Set(fallback.GetType(), fallback, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
            Set(fallback.GetType(), fallback, "FileNameSchema", string.Empty);
            Require((bool)Call(fallback.GetType(), fallback, "GenerateArguments", (Action<string>)(delegate(string ignored) { })), "Standard fallback filename schema failed with Download History enabled");
            string fallbackArgs = (string)Get(fallback, "Arguments");
            Require(fallbackArgs.Contains("%(title)s-%(id)s.%(ext)s") && fallbackArgs.Contains("--download-archive"), "Standard fallback schema lost the mandatory media ID or archive");

            object mostlyCustom = New("youtube_dl_gui.DownloadInfo", "https://archived.youtube.com/watch?v=9qFjkwAElDs");
            Set(mostlyCustom.GetType(), mostlyCustom, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(mostlyCustom.GetType(), mostlyCustom, "MostlyCustomArguments", true);
            Set(mostlyCustom.GetType(), mostlyCustom, "CustomArguments", "ytarchive:9qFjkwAElDs");
            Set(mostlyCustom.GetType(), mostlyCustom, "FileNameSchema", string.Empty);
            Require((bool)Call(mostlyCustom.GetType(), mostlyCustom, "GenerateArguments", (Action<string>)(delegate(string ignored) { })), "Mostly-custom archive-aware argument generation failed");
            string mostlyCustomArgs = (string)Get(mostlyCustom, "Arguments");
            Require(mostlyCustomArgs.Contains("%(title)s-%(id)s.%(ext)s") && mostlyCustomArgs.Contains("--download-archive"), "Mostly-custom path lost filename-ID or archive protection");

            object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://www.youtube.com/ninjasexparty/videos");
            Set(media.GetType(), media, "FileNameSchema", string.Empty);
            Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(media.GetType(), media, "CustomArguments", "--skip-download");
            Require((bool)Call(media.GetType(), media, "GenerateArguments"), "Extended archive-aware argument generation failed");
            string extendedArgs = (string)Get(media, "Arguments");
            Require(extendedArgs.Contains("--ignore-config"), "Extended protected input did not isolate ambient yt-dlp configuration");
            Require(extendedArgs.Contains("--download-archive \"" + fixture.Archive + "\""), "Extended input did not use the shared library archive");
            Require(extendedArgs.Contains("--no-break-on-existing"), "Extended collection path did not preserve full traversal");
            Require(extendedArgs.Contains("%(title)s-%(id)s.%(ext)s"), "Extended fallback schema lost the mandatory media ID");
            Require(Get(media, "DownloadHistoryExecution") != null, "Extended input did not retain its prepared history context");
        }
    }

    private static void DownloadHistoryIgnoresAmbientYtDlpConfig() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(arguments.Contains("--ignore-config"), "Protected arguments do not isolate downloads from ambient yt-dlp configuration files");
            Require(arguments.IndexOf("--ignore-config", StringComparison.Ordinal) < arguments.IndexOf("--download-archive", StringComparison.Ordinal), "Ambient-config isolation is not part of the native protected argument prefix");

            string[] bypasses = {
                "--config-locations custom.conf",
                "--config-location custom.conf",
                "--config-loc custom.conf",
                "--alias unsafe \"--force-write-archive\" --unsafe"
            };
            foreach (string custom in bypasses) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(!string.IsNullOrEmpty(error), "Ambient-config bypass was rejected without an explanation: " + custom);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--config-locations custom.conf --alias unsafe \"--no-part\"", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void RunDownloadHistoryTests() {
        Test("DOWNLOAD_HISTORY.NeverEnabledIsNoOp", DownloadHistoryNeverEnabledIsNoOp);
        Test("DOWNLOAD_HISTORY.TemplateAndArguments", DownloadHistoryTemplateAndArguments);
        Test("DOWNLOAD_HISTORY.RejectsCustomArgumentsThatBreakProtection", DownloadHistoryRejectsCustomArgumentsThatBreakProtection);
        Test("DOWNLOAD_HISTORY.IgnoresAmbientYtDlpConfig", DownloadHistoryIgnoresAmbientYtDlpConfig);
        Test("DOWNLOAD_HISTORY.RecoversSupportedMediaExtensions", DownloadHistoryRecoversSupportedMediaExtensions);
        Test("DOWNLOAD_HISTORY.RebuildsDeletedArchiveFromIds", DownloadHistoryRebuildsDeletedArchiveFromIds);
        Test("DOWNLOAD_HISTORY.RecoversHistoricalProtectedSchemas", DownloadHistoryRecoversHistoricalProtectedSchemas);
        Test("DOWNLOAD_HISTORY.UnderstandsIdPlacementFromSchema", DownloadHistoryUnderstandsIdPlacementFromSchema);
        Test("DOWNLOAD_HISTORY.MigratesLegacyMetadata", DownloadHistoryMigratesLegacyMetadata);
        Test("DOWNLOAD_HISTORY.MigrationFailureRollsBackMedia", DownloadHistoryMigrationFailureRollsBackMedia);
        Test("DOWNLOAD_HISTORY.BlocksPartialAndUnsafeLibraries", DownloadHistoryBlocksPartialAndUnsafeLibraries);
        Test("DOWNLOAD_HISTORY.UnexpectedLossRequiresExplicitReset", DownloadHistoryUnexpectedLossRequiresExplicitReset);
        Test("DOWNLOAD_HISTORY.BackupRefreshRejectsCorruption", DownloadHistoryBackupRefreshRejectsCorruption);
        Test("DOWNLOAD_HISTORY.ValidArchiveTruncationPreservesBackup", DownloadHistoryValidArchiveTruncationPreservesBackup);
        Test("DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines", DownloadHistoryCorruptArchiveDoesNotTrustPartialLines);
        Test("DOWNLOAD_HISTORY.DisableReenableReconcilesChanges", DownloadHistoryDisableReenableReconcilesChanges);
        Test("DOWNLOAD_HISTORY.ValidArchiveDoesNotPromoteUnarchivedFile", DownloadHistoryValidArchiveDoesNotPromoteUnarchivedFile);
        Test("DOWNLOAD_HISTORY.IgnoresFailedAndSidecarFiles", DownloadHistoryIgnoresFailedAndSidecarFiles);
        Test("DOWNLOAD_HISTORY.DisabledIntervalWithoutIdsFailsSafe", DownloadHistoryDisabledIntervalWithoutIdsFailsSafe);
        Test("DOWNLOAD_HISTORY.MissingParentHardStopsWithoutPersistence", DownloadHistoryMissingParentHardStopsWithoutPersistence);
        Test("DOWNLOAD_HISTORY.ManagementFailsFastWhenBusy", DownloadHistoryManagementFailsFastWhenBusy);
        Test("DOWNLOAD_HISTORY.ExecutionContextSurvivesDisableButNotReset", DownloadHistoryExecutionContextSurvivesDisableButNotReset);
        Test("DOWNLOAD_HISTORY.ExecutionContextRejectsSettingChanges", DownloadHistoryExecutionContextRejectsSettingChanges);
        Test("DOWNLOAD_HISTORY.ExecutionLeaseRejectsArchiveTruncation", DownloadHistoryExecutionLeaseRejectsArchiveTruncation);
        Test("DOWNLOAD_HISTORY.LeaseSerializesWorkers", DownloadHistoryLeaseSerializesWorkers);
        Test("DOWNLOAD_HISTORY.CancellationIsRecheckedBeforeProcessStart", DownloadHistoryCancellationIsRecheckedBeforeProcessStart);
        Test("DOWNLOAD_HISTORY.SettingsRejectIdRemovalWhileEnabled", DownloadHistorySettingsRejectIdRemovalWhileEnabled);
        Test("DOWNLOAD_HISTORY.LibraryBindingPreventsCrossLibraryReuse", DownloadHistoryLibraryBindingPreventsCrossLibraryReuse);
        Test("DOWNLOAD_HISTORY.WorkersUsePreparedContext", DownloadHistoryWorkersUsePreparedContext);
        Test("DOWNLOAD_HISTORY.WiresStandardAndExtendedArguments", DownloadHistoryWiresStandardAndExtendedArguments);
    }
}

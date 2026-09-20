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
            history.GetField("fInventoryRoots", All).SetValue(null, string.Empty);
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
        File.WriteAllText(path, "fixture", new UTF8Encoding(false));
        return path;
    }

    private static string DownloadHistoryWriteMediaWithInfo(string root, string name, string extractorKey, string id) {
        string media = DownloadHistoryWriteMedia(root, name);
        string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
        File.WriteAllText(stem + ".info.json", "{\"id\":\"" + id + "\",\"extractor_key\":\"" + extractorKey + "\"}", new UTF8Encoding(false));
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


    private static void DownloadHistoryRequiresAuthoritativeMetadataForRebuild() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(arguments.Contains("--write-info-json"),
                "Protected arguments did not force authoritative per-media info JSON for total-loss recovery");

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--write-info-json", out arguments, out error, out execution));
            Require(arguments.Contains("--write-info-json"),
                "Compatible explicit info JSON request disturbed protected metadata retention");

            foreach (string custom in new[] {
                "--no-write-info-json",
                "--no-write-info"
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution));
                Require(error.IndexOf("info", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("metadata", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("rebuild", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Conflicting metadata-disable option was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--no-write-info-json", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryHonorsEscapedIdTemplateSemantics() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Equal(true, Call(fixture.History, null, "HasRequiredIdTemplate", "%(title)s-%(id)s.%(ext)s"));
            Equal(false, Call(fixture.History, null, "HasRequiredIdTemplate", "%(title)s-%%(id)s.%(ext)s"));
            Equal(false, Call(fixture.History, null, "HasRequiredIdTemplate", "%(title)s-%%%%(id)s.%(ext)s"));
            Equal(true, Call(fixture.History, null, "HasRequiredIdTemplate", "%%%(id)s.%(ext)s"));

            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(error.IndexOf("%(id)s", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("filename", StringComparison.OrdinalIgnoreCase) >= 0,
                "Escaped literal ID token was rejected without identifying the filename-ID requirement");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            const string id = "aB_Cd-Ef123";
            string archive = Path.Combine(fixture.Root, "escaped-schema-history.txt");
            File.WriteAllText(archive, "youtube " + id + Environment.NewLine, new UTF8Encoding(false));
            DownloadHistoryWriteMedia(fixture.Root, "%" + id + ".mp4");
            string oddRunSchema = "%%%(id)s.%(ext)s";
            fixture.History.GetField("fKnownFileNameSchemas", All).SetValue(null,
                "v2:" + Convert.ToBase64String(new UTF8Encoding(false).GetBytes(oddRunSchema)));

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", archive, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "FilenameRecovered"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
        }
    }

    private static void DownloadHistoryRejectsParentTraversalFilenameSchemas() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string schema in new[] {
                "..\\outside\\%(id)s.%(ext)s",
                "../outside/%(id)s.%(ext)s",
                "creator\\..\\outside\\%(id)s.%(ext)s",
                "%(uploader)s\\%(id)s.%(ext)s",
                ".%(uploader)s\\%(id)s.%(ext)s",
                "..%(uploader)s\\%(id)s.%(ext)s",
                "$YTDL_GUI_SCHEMA_ROOT\\%(id)s.%(ext)s",
                "$YTDL_GUI_SCHEMA_ROOT\\%(id)s.%(ext)s",
                "%YTDL_GUI_SCHEMA_ROOT%\\%(id)s.%(ext)s",
                "$YTDL_GUI_SCHEMA_ROOT-%(id)s.%(ext)s"
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, schema, null, out arguments, out error, out execution));
                Require(error.IndexOf("parent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("directory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("environment", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Potential parent traversal schema was not rejected clearly: " + schema);
                Equal(null, execution);
            }

            foreach (string schema in new[] {
                "creator\\series\\%(id)s.%(ext)s",
                "creator-%(uploader)s\\%(id)s.%(ext)s",
                "...%(uploader)s\\%(id)s.%(ext)s",
                "$$$YTDL_GUI_SCHEMA_ROOT\\%(id)s.%(ext)s",
                "%%YTDL_GUI_SCHEMA_ROOT%\\%(id)s.%(ext)s"
            }) {
                Require(DownloadHistoryArguments(fixture.History, schema, null, out arguments, out error, out execution),
                    "Safe protected filename schema was rejected: " + schema + " :: " + error);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Require(DownloadHistoryArguments(fixture.History, "..\\outside\\%(id)s.%(ext)s",
                null, out arguments, out error, out execution),
                "Disabling Download History unexpectedly kept filename containment enforcement active: " + error);
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsUnsafeProtectedFilenameSchemas() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string[] unsafeSchemas = {
                "%(title)s-%(id)s\" --.%(ext)s",
                "%(title)s-%(id)s\r--.%(ext)s",
                "%(title)s-%(id)s\n--.%(ext)s",
                "%(title)s-%(id)s\t--.%(ext)s"
            };
            foreach (string schema in unsafeSchemas) {
                Equal(false, DownloadHistoryArguments(fixture.History, schema, null, out arguments, out error, out execution));
                Require(error.IndexOf("filename", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("schema", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Unsafe protected filename schema was not rejected clearly");
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, unsafeSchemas[0], null, out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }


    private static void DownloadHistoryDisablesPlaylistConcatenationWhileProtected() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(arguments.Contains("--concat-playlist never"),
                "Protected arguments do not disable yt-dlp's default multi-video playlist concatenation");
            Require(arguments.IndexOf("--no-break-on-existing", StringComparison.Ordinal) >= 0,
                "Playlist concat protection disturbed safe archive traversal arguments");

            foreach (string custom in new[] { "--concat-playlist always", "--concat-playlist=multi_video" }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("concat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("concaten", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Conflicting custom playlist concatenation was not rejected clearly: " + custom);
            }
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

    private static void DownloadHistoryRejectsEmbeddedNullCustomArguments() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string custom = "--proxy http://example.invalid" + '\0' + "--output escaped.%(ext)s";

            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                custom, out arguments, out error, out execution));
            Require(error.IndexOf("null", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("NUL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("control", StringComparison.OrdinalIgnoreCase) >= 0,
                "Embedded NUL custom argument was not rejected clearly");
            Equal(null, execution);

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                custom, out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsUnbalancedCustomArgumentQuotes() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "--proxy \"http://example.invalid",
                "--proxy \"http://example.invalid\\\""
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("quote", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("argument", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Unbalanced custom quoting was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--proxy \"http://example.invalid/path?q=a b\"", out arguments, out error, out execution));
            Require(arguments.Contains("--download-archive"), "Balanced quoted custom value disturbed protected archive arguments");

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--proxy \"http://example.invalid", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsPartialSectionDownloads() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "--download-sections \"*00:00:10-00:00:20\"",
                "--download-sections=\"*00:01:00-inf\""
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("section", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Protected partial-section request was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://www.youtube.com/watch?v=9qFjkwAElDs");
            Set(media.GetType(), media, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
            Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
            object format = New("youtube_dl_gui.YoutubeDlSubdata+Format");
            Set(format.GetType(), format, "Identifier", "18");
            Set(format.GetType(), format, "Extension", "mp4");
            System.Windows.Forms.ListViewItem item = new System.Windows.Forms.ListViewItem();
            item.Tag = format;
            Set(media.GetType(), media, "SelectedVideoItem", item);
            Set(media.GetType(), media, "StartTime", Time(10, 0));

            Equal(false, Call(media.GetType(), media, "GenerateArguments"));
            Equal(null, Get(media, "DownloadHistoryExecution"));

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, Call(media.GetType(), media, "GenerateArguments"));
            Require(((string)Get(media, "Arguments")).Contains("--download-sections"),
                "Disabling Download History unexpectedly removed the existing partial-section behavior");
        }
    }

    private static void DownloadHistoryRejectsClusteredShortOutputOverrides() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "-qooutside-%(id)s.%(ext)s",
                "-qPelsewhere",
                "-sqooutside-%(id)s.%(ext)s",
                "-svPelsewhere"
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Clustered short output/path override was not rejected clearly: " + custom);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "-fbestvideo", out arguments, out error, out execution));
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "-qfbestvideo", out arguments, out error, out execution));
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "-uuserwitho", out arguments, out error, out execution));
        }
    }


    private static void DownloadHistoryRejectsSourceAndExtractorIdentityOverrides() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string[] unsafeArguments = {
                "--load-info-json saved.info.json",
                "--load-info saved.info.json",
                "--use-extractors generic",
                "--use-extractor generic",
                "--ies generic",
                "--force-generic-extractor",
                "--force-generic-ext"
            };
            foreach (string custom in unsafeArguments) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("identity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("extractor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Source/extractor identity override was not rejected clearly: " + custom);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            foreach (string custom in new[] {
                "--load-info-json saved.info.json",
                "--use-extractors generic",
                "--ies generic",
                "--force-generic-extractor"
            }) {
                Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Equal(string.Empty, arguments);
            }
        }
    }

    private static void DownloadHistoryRejectsMetadataIdentityRewrites() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string[] unsafeArguments = {
                "--parse-metadata %(title)s:%(id)s",
                "--parse-met %(title)s:%(id)s",
                "--replace-in-metadata id old new",
                "--metadata-from-title %(id)s"
            };
            foreach (string custom in unsafeArguments) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("metadata", StringComparison.OrdinalIgnoreCase) >= 0, "Identity-changing metadata option was not rejected clearly: " + custom);
            }
            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--parse-metadata %(title)s:%(id)s", out arguments, out error, out execution));
        }
    }

    private static void DownloadHistoryRejectsUnsafeExtensionCompatibility() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "--compat-options allow-unsafe-ext",
                "--compat-options=all",
                "--compat-options=-youtube-dl",
                "--compat-opt youtube-dl,allow-unsafe-ext"
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("extension", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("unsafe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("compat", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Unsafe extension compatibility was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            foreach (string custom in new[] {
                "--compat-options -allow-unsafe-ext",
                "--compat-options youtube-dl",
                "--compat-options youtube-dlc",
                "--compat-options allow-unsafe-ext,-allow-unsafe-ext",
                "--compat-options all,-all"
            }) {
                Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(arguments.Contains("--compat-options -allow-unsafe-ext"),
                    "Protected suffix did not force unsafe extensions off: " + custom);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--compat-options allow-unsafe-ext", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsArbitraryStateMutationHooks() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "--print-to-file \"youtube forged\" forged.txt",
                "--print-to-f \"youtube forged\" forged.txt",
                "--netrc-cmd \"echo credentials\"",
                "--netrc-cm \"echo credentials\""
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("write", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("command", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("state", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("hook", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Arbitrary mutation hook was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--print %(id)s", out arguments, out error, out execution));
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--netrc", out arguments, out error, out execution));

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--print-to-file \"youtube forged\" forged.txt --netrc-cmd \"echo credentials\"", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsCookieWritebackCollisions() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string target in new[] { fixture.Archive, fixture.Archive + ".bak", fixture.Archive + ".lock" }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    "--cookies \"" + target + "\"", out arguments, out error, out execution));
                Require(error.IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (error.IndexOf("archive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         error.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Cookie writeback collision was not rejected clearly: " + target);
                Equal(null, execution);

                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    "--cookies=\"" + target + "\"", out arguments, out error, out execution));
                Equal(null, execution);
            }

            string safeCookies = Path.Combine(fixture.Root, "provider-cookies.txt");
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--cookies \"" + safeCookies + "\"", out arguments, out error, out execution));
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--cookies-from-browser firefox", out arguments, out error, out execution));

            MethodInfo validateAuthCookie = fixture.History.GetMethod("ValidateAuthenticationCookiePath", All);
            if (validateAuthCookie == null) throw new Exception("Missing protected authentication cookie-path validator");
            object[] collisionArgs = { fixture.Archive, execution, string.Empty };
            Equal(false, validateAuthCookie.Invoke(null, collisionArgs));
            Require(((string)collisionArgs[2]).IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0,
                "Authentication cookie collision helper did not return an actionable error");
            object[] safeArgs = { safeCookies, execution, string.Empty };
            Equal(true, validateAuthCookie.Invoke(null, safeArgs));

            string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
            string standardSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Classes", "DataClasses", "DownloadInfo.cs"));
            string extendedSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Classes", "DataClasses", "ExtendedMediaDetails.cs"));
            Require(standardSource.Contains("ValidateAuthenticationCookiePath(Authentication?.CookiesFile, HistoryExecution"),
                "Standard downloader does not validate app-configured cookie writeback against protected archive state");
            Require(extendedSource.Contains("ValidateAuthenticationCookiePath(Authentication?.CookiesFile, HistoryExecution"),
                "Extended downloader does not validate app-configured cookie writeback against protected archive state");

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--cookies \"" + fixture.Archive + "\"", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryExpandsDollarCookiePathsLikeYtDlp() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            const string variable = "YTDL_GUI_AUDIT_COOKIE_PATH";
            string previous = Environment.GetEnvironmentVariable(variable);
            try {
                Environment.SetEnvironmentVariable(variable, fixture.Archive);
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    "--cookies \"$" + variable + "\"", out arguments, out error, out execution));
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    "--cookies \"$" + "{" + variable + "}\"", out arguments, out error, out execution));

                Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    null, out arguments, out error, out execution));
                MethodInfo validateAuthCookie = fixture.History.GetMethod("ValidateAuthenticationCookiePath", All);
                if (validateAuthCookie == null) throw new Exception("Missing authentication cookie-path validator");
                object[] authArgs = { "$" + variable, execution, string.Empty };
                Equal(false, validateAuthCookie.Invoke(null, authArgs));

                string safe = Path.Combine(fixture.Root, "safe-cookie.txt");
                Environment.SetEnvironmentVariable(variable, safe);
                Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    "--cookies \"$" + variable + "\"", out arguments, out error, out execution));

                Environment.SetEnvironmentVariable(variable, "~");
                Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    "--cookies \"$" + variable + "\"", out arguments, out error, out execution));
                Require(error.Length == 0, "Cookie normalization expanded '~' after environment substitution instead of matching yt-dlp expanduser-before-expandvars order");
            }
            finally {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }
    }

    private static void DownloadHistoryRejectsRawChildProcessArguments() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "--postprocessor-args \"ffmpeg_o:-progress forged.txt\"",
                "--ppa \"ffmpeg_o:-progress forged.txt\"",
                "--postprocessor-a \"ffmpeg_o:-progress forged.txt\"",
                "--downloader-args \"curl:--trace-ascii forged.txt\"",
                "--external-downloader-args \"curl:--trace-ascii forged.txt\"",
                "--downloader-a \"curl:--trace-ascii forged.txt\""
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("argument", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("child", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("postprocessor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("downloader", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Raw child-process argument injection was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--remux-video mp4 --split-chapters --downloader aria2c", out arguments, out error, out execution));

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--ppa \"ffmpeg_o:-progress forged.txt\" --downloader-args \"curl:--trace-ascii forged.txt\"",
                out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsDestructiveCacheRemoval() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string cacheRoot = Path.Combine(fixture.Root, "cache-library");

            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--cache-dir \"" + cacheRoot + "\" --rm-cache-dir", out arguments, out error, out execution));
            Require(error.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (error.IndexOf("remove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     error.IndexOf("delete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     error.IndexOf("destructive", StringComparison.OrdinalIgnoreCase) >= 0),
                "Recursive cache removal was not rejected clearly");
            Equal(null, execution);

            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--rm-c", out arguments, out error, out execution));
            Equal(null, execution);

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--cache-dir \"" + cacheRoot + "\"", out arguments, out error, out execution));

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--cache-dir \"" + cacheRoot + "\" --rm-cache-dir", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }


    private static void DownloadHistoryRejectsTestModePartialCompletion() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] { "--test", "--tes", "--te" }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution));
                Require(error.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Protected test mode was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--check-formats", out arguments, out error, out execution));

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--test", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsFalseCompletionErrorControls() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "-i",
                "--ignore-errors",
                "--ignore-e",
                "--ignore-no-formats-error",
                "--ignore-n"
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution));
                Require(error.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("archive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0,
                    "False-completion error control was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            foreach (string custom in new[] {
                "--no-abort-on-error",
                "--abort-on-error",
                "--no-ignore-errors",
                "--no-ignore-no-formats-error"
            }) {
                Require(DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution),
                    "Fail-closed neighboring error control was rejected: " + custom + " :: " + error);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "-i --ignore-no-formats-error", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryForcesCompleteFragmentDownloads() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "--skip-unavailable-fragments",
                "--skip-u",
                "--no-abort-on-unavailable-fragments",
                "--no-abort-on-u"
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution));
                Require(error.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("archive", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Partial-fragment control was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            foreach (string custom in new[] {
                "--abort-on-unavailable-fragments",
                "--no-skip-unavailable-fragments"
            }) {
                Require(DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution),
                    "Fail-closed fragment control was rejected: " + custom + " :: " + error);
                Require(arguments.Contains("--abort-on-unavailable-fragments"),
                    "Protected suffix did not force fail-closed fragment handling");
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                null, out arguments, out error, out execution));
            Require(arguments.Contains("--abort-on-unavailable-fragments"),
                "Protected suffix does not override yt-dlp's default fragment-skipping behavior");

            bool oldSkip = (bool)fixture.Downloads.GetProperty("SkipUnavailableFragments", All).GetValue(null, null);
            try {
                Set(fixture.Downloads, null, "SkipUnavailableFragments", true);

                object standard = New("youtube_dl_gui.DownloadInfo", "https://www.youtube.com/watch?v=9qFjkwAElDs");
                Set(standard.GetType(), standard, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
                Set(standard.GetType(), standard, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
                Require((bool)Call(standard.GetType(), standard, "GenerateArguments",
                    (Action<string>)(delegate(string ignored) { })), "Standard protected fragment arguments failed to generate");
                string standardArgs = (string)Get(standard, "Arguments");
                int standardSkip = standardArgs.IndexOf("--skip-unavailable-fragments", StringComparison.Ordinal);
                int standardAbort = standardArgs.LastIndexOf("--abort-on-unavailable-fragments", StringComparison.Ordinal);
                Require(standardSkip >= 0 && standardAbort > standardSkip,
                    "Standard protected suffix did not override the app's fragment-skip setting");

                object extended = New("youtube_dl_gui.ExtendedMediaDetails", "https://www.youtube.com/watch?v=9qFjkwAElDs");
                Set(extended.GetType(), extended, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
                Set(extended.GetType(), extended, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
                Set(extended.GetType(), extended, "SkipUnavailableFragments", true);
                object format = New("youtube_dl_gui.YoutubeDlSubdata+Format");
                Set(format.GetType(), format, "Identifier", "18");
                Set(format.GetType(), format, "Extension", "mp4");
                System.Windows.Forms.ListViewItem item = new System.Windows.Forms.ListViewItem();
                item.Tag = format;
                Set(extended.GetType(), extended, "SelectedVideoItem", item);
                Require((bool)Call(extended.GetType(), extended, "GenerateArguments"),
                    "Extended protected fragment arguments failed to generate");
                string extendedArgs = (string)Get(extended, "Arguments");
                int extendedSkip = extendedArgs.IndexOf("--skip-unavailable-fragments", StringComparison.Ordinal);
                int extendedAbort = extendedArgs.LastIndexOf("--abort-on-unavailable-fragments", StringComparison.Ordinal);
                Require(extendedSkip >= 0 && extendedAbort > extendedSkip,
                    "Extended protected suffix did not override the per-download fragment-skip setting");
            }
            finally {
                Set(fixture.Downloads, null, "SkipUnavailableFragments", oldSkip);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--skip-unavailable-fragments", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryRejectsExecutablePathAndSelfUpdateOverrides() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] {
                "-U",
                "--update",
                "--update-to custom@example",
                "--ffmpeg-location \"C:\\audit\\ffmpeg.exe\"",
                "--ffmpeg \"C:\\audit\\ffmpeg.exe\"",
                "--downloader \"C:\\audit\\aria2c.exe\"",
                "--external-downloader \"..\\audit\\curl.exe\"",
                "--js-runtimes \"node:C:\\audit\\node.exe\""
            }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution));
                Require(error.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("update", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("runtime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("downloader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("ffmpeg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("process", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Executable/provider override was not rejected clearly: " + custom);
                Equal(null, execution);
            }

            foreach (string custom in new[] {
                "--no-update",
                "--downloader aria2c",
                "--external-downloader \"dash,m3u8:native\"",
                "--js-runtimes node",
                "--no-js-runtimes"
            }) {
                Require(DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution),
                    "Safe built-in provider/runtime selection was rejected: " + custom + " :: " + error);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--update-to custom@example --downloader \"C:\\audit\\aria2c.exe\"",
                out arguments, out error, out execution));
            Equal(string.Empty, arguments);
        }
    }

    private static void DownloadHistoryMatchesYtDlpPathExpansion() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            const string variable = "YTDL_GUI_HISTORY_ROOT";
            const string dollarVariable = "YTDL_GUI_DOLLAR_TARGET";
            const string percentVariable = "YTDL_GUI_PERCENT_TARGET";
            string previous = Environment.GetEnvironmentVariable(variable);
            string previousDollar = Environment.GetEnvironmentVariable(dollarVariable);
            string previousPercent = Environment.GetEnvironmentVariable(percentVariable);
            try {
                Environment.SetEnvironmentVariable(variable, fixture.Root);
                Environment.SetEnvironmentVariable(dollarVariable, "expanded-dollar");
                Environment.SetEnvironmentVariable(percentVariable, "expanded-percent");

                string expectedMediaRoot = Path.GetFullPath(Path.Combine(fixture.Root, "media"));
                Equal(expectedMediaRoot, Call(fixture.History, null, "ResolveActiveDownloadRoot", "$" + variable + "\\media"));
                Equal(expectedMediaRoot, Call(fixture.History, null, "ResolveActiveDownloadRoot", "$" + "{" + variable + "}\\media"));
                Equal(expectedMediaRoot, Call(fixture.History, null, "ResolveActiveDownloadRoot", "%" + variable + "%\\media"));

                string home = Environment.GetEnvironmentVariable("USERPROFILE");
                if (!string.IsNullOrEmpty(home)) {
                    Equal(Path.GetFullPath(Path.Combine(home, "media")),
                        Call(fixture.History, null, "ResolveActiveDownloadRoot", "~\\media"));
                }

                string expectedArchive = Path.Combine(fixture.Root, "history.txt");
                fixture.History.GetField("fArchivePath", All).SetValue(null, "$" + variable + "\\history.txt");
                Equal(Path.GetFullPath(expectedArchive), fixture.History.GetProperty("EffectiveArchivePath", All).GetValue(null, null));

                string literalPhysical = Path.Combine(fixture.Root, "$" + dollarVariable + "%" + percentVariable + "%", "history.txt");
                string escaped = (string)Call(fixture.History, null, "EscapeYtDlpLiteralPathForArgument", literalPhysical);
                Equal(literalPhysical, Call(fixture.History, null, "ResolveYtDlpDirectPath", escaped));

                string privateUseRoot = Path.Combine(fixture.Root, "\uE000literal", "media");
                Equal(Path.GetFullPath(privateUseRoot),
                    Call(fixture.History, null, "ResolveActiveDownloadRoot", privateUseRoot));

                string quotedLiteralPhysical = Path.Combine(
                    fixture.Root,
                    "'$" + dollarVariable + "%" + percentVariable + "%'",
                    "$" + dollarVariable + "%" + percentVariable + "%",
                    "history.txt");
                string quotedEscaped = (string)Call(fixture.History, null, "EscapeYtDlpLiteralPathForArgument", quotedLiteralPhysical);
                Equal(Path.GetFullPath(quotedLiteralPhysical),
                    Call(fixture.History, null, "ResolveYtDlpDirectPath", quotedEscaped));

                string dynamicRoot = Path.Combine(fixture.Root, "%(uploader)s");
                Throws<InvalidOperationException>(delegate {
                    Call(fixture.History, null, "ResolveActiveDownloadRoot", dynamicRoot);
                });

                fixture.History.GetField("fArchivePath", All).SetValue(null, string.Empty);
                Set(fixture.Downloads, null, "downloadPath", dynamicRoot);
                string arguments, error;
                object execution;
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    null, out arguments, out error, out execution));
                Require(error.IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("template", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Dynamic active download root was not rejected clearly");

                string sourceRoot = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
                string historySource = File.ReadAllText(Path.Combine(sourceRoot, "youtube-dl-gui", "Classes", "DownloadHistory.cs"));
                string dialogSource = File.ReadAllText(Path.Combine(sourceRoot, "youtube-dl-gui", "Forms", "frmDownloadHistory.cs"));
                Require(historySource.Contains("EscapeYtDlpLiteralPathForArgument(preparedArchive)"),
                    "Protected archive arguments are not escaped back to the exact locked physical path");
                Require(dialogSource.Contains("DownloadHistory.NormalizeConfiguredArchivePathForUi"),
                    "Download History dialog path normalization does not use provider-compatible archive expansion");
            }
            finally {
                Environment.SetEnvironmentVariable(variable, previous);
                Environment.SetEnvironmentVariable(dollarVariable, previousDollar);
                Environment.SetEnvironmentVariable(percentVariable, previousPercent);
            }
        }
    }

    private static void DownloadHistoryIsolationPrecedesDanglingCustomOptions() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);

            object standard = New("youtube_dl_gui.DownloadInfo", "https://www.youtube.com/watch?v=9qFjkwAElDs");
            Set(standard.GetType(), standard, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(standard.GetType(), standard, "CustomArguments", "--proxy");
            Set(standard.GetType(), standard, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
            Require((bool)Call(standard.GetType(), standard, "GenerateArguments", (Action<string>)(delegate(string ignored) { })),
                "Standard dangling-option control did not generate arguments");
            string standardArgs = (string)Get(standard, "Arguments");
            int standardCustom = standardArgs.IndexOf("--proxy", StringComparison.Ordinal);
            int standardFirstIsolation = standardArgs.IndexOf("--ignore-config", StringComparison.Ordinal);
            int standardLastIsolation = standardArgs.LastIndexOf("--ignore-config", StringComparison.Ordinal);
            Require(standardFirstIsolation >= 0 && standardFirstIsolation < standardCustom && standardLastIsolation > standardCustom,
                "Standard protected isolation does not bracket raw custom arguments");

            object mostlyCustom = New("youtube_dl_gui.DownloadInfo", "https://www.youtube.com/watch?v=9qFjkwAElDs");
            Set(mostlyCustom.GetType(), mostlyCustom, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(mostlyCustom.GetType(), mostlyCustom, "MostlyCustomArguments", true);
            Set(mostlyCustom.GetType(), mostlyCustom, "CustomArguments", "--proxy");
            Set(mostlyCustom.GetType(), mostlyCustom, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
            Require((bool)Call(mostlyCustom.GetType(), mostlyCustom, "GenerateArguments", (Action<string>)(delegate(string ignored) { })),
                "Mostly-custom dangling-option control did not generate arguments");
            string mostlyArgs = (string)Get(mostlyCustom, "Arguments");
            int mostlyCustomIndex = mostlyArgs.IndexOf("--proxy", StringComparison.Ordinal);
            Require(mostlyArgs.IndexOf("--ignore-config", StringComparison.Ordinal) < mostlyCustomIndex &&
                    mostlyArgs.LastIndexOf("--ignore-config", StringComparison.Ordinal) > mostlyCustomIndex,
                "Mostly-custom protected isolation does not survive argument-buffer replacement");

            object extended = New("youtube_dl_gui.ExtendedMediaDetails", "https://www.youtube.com/watch?v=9qFjkwAElDs");
            Set(extended.GetType(), extended, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
            Set(extended.GetType(), extended, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(extended.GetType(), extended, "CustomArguments", "--proxy");
            Require((bool)Call(extended.GetType(), extended, "GenerateArguments"),
                "Extended dangling-option control did not generate arguments");
            string extendedArgs = (string)Get(extended, "Arguments");
            int extendedCustom = extendedArgs.IndexOf("--proxy", StringComparison.Ordinal);
            Require(extendedArgs.IndexOf("--ignore-config", StringComparison.Ordinal) < extendedCustom &&
                    extendedArgs.LastIndexOf("--ignore-config", StringComparison.Ordinal) > extendedCustom,
                "Extended protected isolation does not bracket raw custom arguments");
        }
    }

    private static void DownloadHistoryRejectsNonNativeArchiveEncodings() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            MethodInfo readArchive = fixture.History.GetMethod("TryReadArchive", All);
            if (readArchive == null) throw new Exception("Missing native archive reader");

            Func<string, bool> read = delegate(string file) {
                object[] args = { file, new HashSet<string>(StringComparer.Ordinal), string.Empty };
                return (bool)readArchive.Invoke(null, args);
            };

            string plain = Path.Combine(fixture.Root, "plain-utf8.txt");
            File.WriteAllText(plain, "youtube 9qFjkwAElDs\n", new UTF8Encoding(false));
            Equal(true, read(plain));

            string bom = Path.Combine(fixture.Root, "utf8-bom.txt");
            File.WriteAllText(bom, "youtube 9qFjkwAElDs\n", new UTF8Encoding(true));
            Equal(false, read(bom));

            string utf16 = Path.Combine(fixture.Root, "utf16.txt");
            File.WriteAllText(utf16, "youtube 9qFjkwAElDs\n", Encoding.Unicode);
            Equal(false, read(utf16));

            string malformed = Path.Combine(fixture.Root, "malformed-utf8.txt");
            byte[] prefix = Encoding.ASCII.GetBytes("youtube 9qFjkwAElDs");
            File.WriteAllBytes(malformed, prefix.Concat(new byte[] { 0xC3, 0x28, 0x0A }).ToArray());
            Equal(false, read(malformed));
        }
    }

    private static void DownloadHistoryRejectsArbitraryPostprocessorHooks() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            string[] unsafeArguments = {
                "--exec echo changed",
                "--exe echo changed",
                "--exec-before-download echo changed",
                "--use-postprocessor AuditPlugin"
            };
            foreach (string custom in unsafeArguments) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("postprocessor", StringComparison.OrdinalIgnoreCase) >= 0 || error.IndexOf("exec", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Arbitrary postprocessor hook was not rejected clearly: " + custom);
            }
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--remux-video mp4 --split-chapters", out arguments, out error, out execution));
        }
    }

    private static void DownloadHistoryRejectsIdOutputOverride() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--id", out arguments, out error, out execution));
            Require(error.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0, "The hidden --id output override was not rejected clearly");
            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--id", out arguments, out error, out execution));
        }
    }

    private static void DownloadHistoryRejectsInjectedMetadataArchiveRecords() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Injected-legityoutube evil.mp4");
            string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
            File.WriteAllText(stem + ".info.json", "{\"id\":\"legit\\nyoutube evil\",\"extractor_key\":\"Youtube\"}", new UTF8Encoding(false));

            object report = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, true);
            Equal("Unsafe", DownloadHistoryStateName(report));
            Equal(1, Get(report, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Metadata ID containing a record boundary was written into the archive");
            Require(File.Exists(media), "Rejected metadata identity unexpectedly changed the media path");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Injected extractor.mp4");
            string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
            File.WriteAllText(stem + ".info.json", "{\"id\":\"evilId\",\"extractor_key\":\"Youtube legitId\\nyoutube\"}", new UTF8Encoding(false));

            object report = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, true);
            Equal("Unsafe", DownloadHistoryStateName(report));
            Equal(1, Get(report, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Metadata extractor containing a record boundary was written into the archive");
            Require(File.Exists(media), "Rejected extractor identity unexpectedly changed the media path");
        }
    }

    private static void DownloadHistoryRecoversSupportedMediaExtensions() {
        string[] extensions = { ".f4v", ".mk3d", ".divx", ".ogv", ".f4a", ".f4b", ".m4r", ".ogx", ".spx", ".vorbis", ".weba", ".nut", ".swf", ".mp2", ".tta", ".aifc" };
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string[] ids = new string[extensions.Length];
            for (int i = 0; i < extensions.Length; i++) {
                ids[i] = ((char)('A' + i)).ToString() + "1234567890";
                DownloadHistoryWriteMediaWithInfo(fixture.Root, "Media-" + ids[i] + extensions[i], "Youtube", ids[i]);
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

    private static void DownloadHistoryInventoriesCurrentDirectMediaExtensions() {
        string[] extensions = {
            ".3ga", ".adts", ".asx", ".au", ".isma", ".ismv", ".it", ".m2t", ".m4s", ".mid",
            ".mng", ".mod", ".mp1", ".mp2v", ".mp4a", ".mp4v", ".mpa", ".mpeg1", ".mpeg2", ".mpeg4",
            ".mpga", ".mxf", ".ogm", ".qt", ".ra", ".rm", ".shn", ".vid", ".vp9", ".xm", ".unknown_video"
        };
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            List<string> mediaPaths = new List<string>();
            List<byte[]> mediaBytes = new List<byte[]>();
            for (int i = 0; i < extensions.Length; i++) {
                string id = "direct" + i.ToString("D4");
                string media = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Direct-" + id + extensions[i], "Generic", id);
                mediaPaths.Add(media);
                mediaBytes.Add(File.ReadAllBytes(media));
            }

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal(extensions.Length, Get(analysis, "CompletedMedia"));
            Equal(extensions.Length, Get(analysis, "MetadataRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(extensions.Length, Get(rebuilt, "ArchiveEntries"));
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            for (int i = 0; i < extensions.Length; i++) {
                string id = "direct" + i.ToString("D4");
                Require(lines.Contains("generic " + id), "Explicit rebuild omitted current direct-media extension " + extensions[i]);
                Require(File.Exists(mediaPaths[i]) && mediaBytes[i].SequenceEqual(File.ReadAllBytes(mediaPaths[i])),
                    "Direct-media inventory modified an existing " + extensions[i] + " file");
            }
        }
    }

    private static void DownloadHistoryRebuildsDeletedArchiveFromIds() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Video-" + id + ".webm", "Youtube", id);
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Video-copy-" + id + ".mp4", "Youtube", id);
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

    private static void DownloadHistoryParsesFormattedFilenameSchemas() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Set(fixture.Downloads, null, "fileNameSchema", "%(title).200B-%(id)s-%(upload_date)s.%(ext)s");
            DownloadHistoryWriteMedia(fixture.Root, "Title-" + id + "-20260915.mp4");
            File.WriteAllText(fixture.Archive, "youtube " + id + "\r\n", new UTF8Encoding(false));
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal(1, Get(analysis, "FilenameRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
            Equal("Healthy", DownloadHistoryStateName(analysis));
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
            DownloadHistoryWriteMediaWithInfo(fixture.Root, id + "--historical-title.mp4", "Youtube", id);

            Set(fixture.Downloads, null, "fileNameSchema", "NEW-%(id)s.%(ext)s");
            File.Delete(fixture.Archive);
            File.Delete(fixture.Archive + ".bak");
            object rebuilt = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, true);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Require(DownloadHistoryArchiveLines(fixture.Archive).Contains("youtube " + id), "Archive loss could not recover a file written with an earlier protected filename schema");
        }
    }

    private static void DownloadHistoryPreservesDelimiterBearingSchemas() {
        const string id = "aB_Cd-Ef123";
        const string historical = "%(chapters&has chapters|no chapters)s-%(id)s.%(ext)s";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Set(fixture.Downloads, null, "fileNameSchema", historical);
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, historical, null, out arguments, out error, out execution));
            string persisted = (string)fixture.History.GetField("fKnownFileNameSchemas", All).GetValue(null);
            Require(persisted.StartsWith("v2:", StringComparison.Ordinal), "Protected filename schema history was not migrated to unambiguous encoding");

            DownloadHistoryWriteMedia(fixture.Root, "has chapters-" + id + ".mp4");
            File.WriteAllText(fixture.Archive, "youtube " + id + "\r\n", new UTF8Encoding(false));
            Set(fixture.Downloads, null, "fileNameSchema", "NEW-%(id)s.%(ext)s");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal(0, Get(analysis, "UnresolvedMedia"));
            Equal(1, Get(analysis, "FilenameRecovered"));
        }
    }

    private static void DownloadHistoryUnderstandsIdPlacementFromSchema() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            Set(fixture.Downloads, null, "fileNameSchema", "%(id)s--%(title)s.%(ext)s");
            DownloadHistoryWriteMedia(fixture.Root, id + "--Title.webm");
            File.WriteAllText(fixture.Archive, "youtube " + id + "\r\n", new UTF8Encoding(false));
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal(1, Get(analysis, "FilenameRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
            DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }
    }

    private static void DownloadHistoryRejectsReparsePointTraversal() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string target = Path.Combine(Environment.CurrentDirectory, "download-history-link-target-" + Guid.NewGuid().ToString("N"));
            string link = Path.Combine(fixture.Root, "linked-library");
            Directory.CreateDirectory(target);
            DownloadHistoryWriteMedia(target, "Outside-ABCDEFGHIJK.mp4");
            try {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo {
                    FileName = "cmd.exe",
                    Arguments = "/d /c mklink /J \"" + link + "\" \"" + target + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi)) {
                    Require(process.WaitForExit(5000), "Junction creation did not complete");
                    Equal(0, process.ExitCode);
                }
                object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
                Equal("Unavailable", DownloadHistoryStateName(analysis));
                Require(((string)Get(analysis, "Message")).IndexOf("reparse", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Reparse-point library traversal did not fail with an explicit safety message");
            }
            finally {
                if (Directory.Exists(link)) Directory.Delete(link);
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
        }
    }


    private static void DownloadHistoryRecoversRestrictedYtDlpIdSanitization() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            const string accentedId = "äabc";
            DownloadHistoryWriteMedia(fixture.Root, "Restricted-aabc.mp4");
            File.WriteAllText(fixture.Archive, "rokfin " + accentedId + Environment.NewLine, new UTF8Encoding(false));

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "FilenameRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            const string leadingInvalidId = "/abc";
            DownloadHistoryWriteMedia(fixture.Root, "Restricted-abc.mp4");
            File.WriteAllText(fixture.Archive, "rokfin " + leadingInvalidId + Environment.NewLine, new UTF8Encoding(false));

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "FilenameRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
        }
    }

    private static void DownloadHistoryRecoversSanitizedProviderIds() {
        const string id = "stream/31332";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string original = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Legacy Rokfin.mp4", "Rokfin", id);
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(analysis));
            Equal(0, Get(analysis, "MigrationCount"));
            DownloadHistoryReconcile(fixture, string.Empty, false);
            string expected = Path.Combine(fixture.Root, "Legacy Rokfin-stream\u29F831332.mp4");
            Require(!File.Exists(expected), "Inventory created a renamed provider media path");
            Require(File.Exists(original), "Metadata-backed provider media was renamed during inventory");
            Require(File.Exists(Path.Combine(fixture.Root, "Legacy Rokfin.info.json")), "Metadata sidecar was renamed during inventory");
            Equal("rokfin " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());

            File.Delete(fixture.Archive);
            if (File.Exists(fixture.Archive + ".bak")) File.Delete(fixture.Archive + ".bak");
            DownloadHistoryReconcile(fixture, string.Empty, true);
            Equal("rokfin " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Restricted-stream_31332.mp4");
            File.WriteAllText(fixture.Archive, "rokfin " + id + "\r\n", new UTF8Encoding(false));
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "FilenameRecovered"));
        }
    }

    private static void DownloadHistoryRecognizesSplitChapterIds() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Video - 001 Intro [" + id + "].mp4");
            File.WriteAllText(fixture.Archive, "youtube " + id + "\r\n", new UTF8Encoding(false));
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "FilenameRecovered"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
        }
    }

    private static void DownloadHistoryRebuildsDerivedMediaAfterTotalArchiveLoss() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string parent = DownloadHistoryWriteMedia(fixture.Root, "Z Parent-" + id + ".mp4");
            string info = Path.ChangeExtension(parent, ".info.json");
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"requested_formats\":[{\"format_id\":\"137\"},{\"format_id\":\"140\"}],\"format_id\":\"137+140\"}",
                new UTF8Encoding(false));
            string chapter = DownloadHistoryWriteMedia(fixture.Root, "A Chapter - 001 Intro [" + id + "].mp4");
            string keptFormat = DownloadHistoryWriteMedia(fixture.Root, "Z Parent-" + id + ".f137.webm");
            byte[] parentBefore = File.ReadAllBytes(parent);
            byte[] infoBefore = File.ReadAllBytes(info);
            byte[] chapterBefore = File.ReadAllBytes(chapter);
            byte[] keptBefore = File.ReadAllBytes(keptFormat);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(true, DownloadHistoryCanReconcile(rebuilt));
            Equal(3, Get(rebuilt, "CompletedMedia"));
            Equal(3, Get(rebuilt, "IdentifiedMedia"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(parentBefore.SequenceEqual(File.ReadAllBytes(parent)), "Derived-media rebuild rewrote canonical media");
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Derived-media rebuild rewrote canonical metadata");
            Require(chapterBefore.SequenceEqual(File.ReadAllBytes(chapter)), "Derived-media rebuild rewrote split chapter output");
            Require(keptBefore.SequenceEqual(File.ReadAllBytes(keptFormat)), "Derived-media rebuild rewrote retained format output");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "Components-" + id);
            string info = ownerStem + ".info.json";
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"format_id\":\"137+140\",\"formats\":[{\"format_id\":\"137\"},{\"format_id\":\"140\"},{\"format_id\":\"999\"}]}",
                new UTF8Encoding(false));
            string videoComponent = DownloadHistoryWriteMedia(fixture.Root, "Components-" + id + ".f137.webm");
            string audioComponent = DownloadHistoryWriteMedia(fixture.Root, "Components-" + id + ".f140.m4a");
            byte[] infoBefore = File.ReadAllBytes(info);
            byte[] videoBefore = File.ReadAllBytes(videoComponent);
            byte[] audioBefore = File.ReadAllBytes(audioComponent);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(true, DownloadHistoryCanReconcile(rebuilt));
            Equal(2, Get(rebuilt, "CompletedMedia"));
            Equal(2, Get(rebuilt, "IdentifiedMedia"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Component-only rebuild rewrote owner metadata");
            Require(videoBefore.SequenceEqual(File.ReadAllBytes(videoComponent)), "Component-only rebuild rewrote retained video format");
            Require(audioBefore.SequenceEqual(File.ReadAllBytes(audioComponent)), "Component-only rebuild rewrote retained audio format");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "Unexpected-" + id);
            File.WriteAllText(ownerStem + ".info.json",
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"format_id\":\"137+140\",\"formats\":[{\"format_id\":\"137\"},{\"format_id\":\"140\"},{\"format_id\":\"999\"}]}",
                new UTF8Encoding(false));
            string unexpected = DownloadHistoryWriteMedia(fixture.Root, "Unexpected-" + id + ".f999.webm");
            byte[] unexpectedBefore = File.ReadAllBytes(unexpected);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Require(DownloadHistoryStateName(rebuilt) == "Partial" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Unselected format component did not keep rebuild fail-closed");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Unselected format component caused an archive rewrite");
            Require(unexpectedBefore.SequenceEqual(File.ReadAllBytes(unexpected)), "Unselected format component was modified");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Parent-" + id + ".mp4", "Youtube", id);
            string unrelated = DownloadHistoryWriteMedia(fixture.Root, "Unrelated-UNKNOWN_MEDIA.mp4");
            byte[] unrelatedBefore = File.ReadAllBytes(unrelated);
            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Require(DownloadHistoryStateName(rebuilt) == "Partial" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Unrelated unresolved media did not keep rebuild fail-closed");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Require(!File.Exists(fixture.Archive), "Fail-closed derived-media rebuild wrote an archive despite unrelated unresolved media");
            Require(unrelatedBefore.SequenceEqual(File.ReadAllBytes(unrelated)), "Fail-closed derived-media rebuild modified unrelated media");
        }
    }

    private static void DownloadHistoryValidatesRetainedFormatComponents() {
        const string id = "9qFjkwAElDs";

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "Punctuation-" + id);
            string info = ownerStem + ".info.json";
            const string formatId = "audio.main (English)";
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"requested_formats\":[{\"format_id\":\"" + formatId + "\"}]}",
                new UTF8Encoding(false));
            string component = DownloadHistoryWriteMedia(fixture.Root,
                "Punctuation-" + id + ".f" + formatId + ".m4a");
            byte[] infoBefore = File.ReadAllBytes(info);
            byte[] componentBefore = File.ReadAllBytes(component);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(true, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "IdentifiedMedia"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Punctuation format recovery rewrote owner metadata");
            Require(componentBefore.SequenceEqual(File.ReadAllBytes(component)), "Punctuation format recovery rewrote retained component");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string parent = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Parent-" + id + ".mp4", "Youtube", id);
            string info = Path.ChangeExtension(parent, ".info.json");
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"requested_formats\":[{\"format_id\":\"137\"}],\"format_id\":\"137\"}",
                new UTF8Encoding(false));
            string unexpected = DownloadHistoryWriteMedia(fixture.Root, "Parent-" + id + ".f999.webm");
            byte[] parentBefore = File.ReadAllBytes(parent);
            byte[] infoBefore = File.ReadAllBytes(info);
            byte[] unexpectedBefore = File.ReadAllBytes(unexpected);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Require(DownloadHistoryStateName(rebuilt) == "Partial" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Canonical parent identity incorrectly promoted an unselected retained component");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Unselected retained component caused an archive rewrite");
            Require(parentBefore.SequenceEqual(File.ReadAllBytes(parent)), "Fail-closed retained-component check rewrote canonical media");
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Fail-closed retained-component check rewrote canonical metadata");
            Require(unexpectedBefore.SequenceEqual(File.ReadAllBytes(unexpected)), "Fail-closed retained-component check rewrote unexpected media");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "Mismatch-" + id);
            File.WriteAllText(ownerStem + ".info.json",
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"requested_formats\":[{\"format_id\":\"137\"}],\"format_id\":\"137+999\"}",
                new UTF8Encoding(false));
            string unexpected = DownloadHistoryWriteMedia(fixture.Root, "Mismatch-" + id + ".f999.webm");

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Require(DownloadHistoryStateName(rebuilt) == "Partial" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Combined format_id fallback overrode authoritative requested_formats");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Inconsistent retained-format metadata caused an archive rewrite");
            Require(File.Exists(unexpected), "Inconsistent retained-format metadata modified the candidate");
        }
    }

    private static void DownloadHistoryDisambiguatesCleanMergedFormatIds() {
        const string id = "9qFjkwAElDs";

        // Clean yt-dlp info JSON removes requested_formats. A selected format ID may itself
        // contain '+', so the merged top-level format_id must be decomposed against the
        // preserved formats catalogue instead of blindly splitting on every plus sign.
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "Plus-" + id);
            string info = ownerStem + ".info.json";
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"format_id\":\"video+audio+main\",\"formats\":[{\"format_id\":\"video\"},{\"format_id\":\"audio+main\"},{\"format_id\":\"main\"}]}",
                new UTF8Encoding(false));
            string component = DownloadHistoryWriteMedia(fixture.Root, "Plus-" + id + ".faudio+main.m4a");
            byte[] infoBefore = File.ReadAllBytes(info);
            byte[] componentBefore = File.ReadAllBytes(component);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(true, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "IdentifiedMedia"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Plus-bearing format proof rewrote clean metadata");
            Require(componentBefore.SequenceEqual(File.ReadAllBytes(component)), "Plus-bearing format proof rewrote retained media");
        }

        // If the same combined string has multiple valid decompositions, a component is
        // recoverable only when every decomposition proves that exact format was selected.
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "Ambiguous-" + id);
            File.WriteAllText(ownerStem + ".info.json",
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"format_id\":\"video+audio+main\",\"formats\":[{\"format_id\":\"video\"},{\"format_id\":\"audio+main\"},{\"format_id\":\"audio\"},{\"format_id\":\"main\"}]}",
                new UTF8Encoding(false));
            string ambiguous = DownloadHistoryWriteMedia(fixture.Root, "Ambiguous-" + id + ".fmain.m4a");
            byte[] ambiguousBefore = File.ReadAllBytes(ambiguous);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Require(DownloadHistoryStateName(rebuilt) == "Partial" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Ambiguous clean format_id decomposition promoted an unproven component");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Ambiguous clean format proof caused an archive rewrite");
            Require(ambiguousBefore.SequenceEqual(File.ReadAllBytes(ambiguous)), "Ambiguous clean format proof modified retained media");
        }

        // A .f<format> component needs persisted catalogue evidence after clean-info stripping.
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ownerStem = Path.Combine(fixture.Root, "NoCatalogue-" + id);
            File.WriteAllText(ownerStem + ".info.json",
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"format_id\":\"137+140\"}",
                new UTF8Encoding(false));
            string component = DownloadHistoryWriteMedia(fixture.Root, "NoCatalogue-" + id + ".f137.webm");

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Require(DownloadHistoryStateName(rebuilt) == "Partial" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Retained component without a persisted formats catalogue was treated as authoritatively selected");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Equal(1, Get(rebuilt, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive), "Unproven clean retained component caused an archive rewrite");
            Require(File.Exists(component), "Unproven clean retained component was modified");
        }
    }


    private static void DownloadHistoryRejectsLocalExtractorPageSubstitution() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            foreach (string custom in new[] { "--load-pages", "--load-p" }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution));
                Require(error.IndexOf("page", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("extract", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Local extractor-page substitution was rejected without an actionable explanation: " + custom);
                Equal(null, execution);
            }

            foreach (string custom in new[] { "--write-pages", "--dump-pages" }) {
                Require(DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                    custom, out arguments, out error, out execution),
                    "Read-only extractor page debugging was unnecessarily blocked: " + custom + " :: " + error);
                Require(execution != null, "Safe page-debug option lost the protected execution context: " + custom);
            }

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s",
                "--load-pages", out arguments, out error, out execution));
            Equal(string.Empty, arguments);
            Equal(null, execution);
        }
    }


    private static void DownloadHistoryDoesNotInferYoutubeFromIdShape() {
        const string ambiguousId = "ABCDEFGHIJK";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Generic-" + ambiguousId + ".mp4");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Unsafe", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "UnresolvedMedia"));
            Equal(false, DownloadHistoryCanReconcile(analysis));
            Require(!File.Exists(fixture.Archive), "Ambiguous 11-character filename ID was incorrectly promoted into a YouTube archive entry");
        }
    }


    private static void DownloadHistoryRejectsDisplayExtractorAsNativeIdentity() {
        const string id = "UgytZKpehg-hEMBSn3F4AaABCQ";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Clip metadata.mp4");
            string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
            File.WriteAllText(stem + ".info.json",
                "{\"id\":\"" + id + "\",\"extractor\":\"youtube:clip\"}", new UTF8Encoding(false));

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Unsafe", DownloadHistoryStateName(analysis));
            Equal(0, Get(analysis, "MetadataRecovered"));
            Equal(1, Get(analysis, "UnresolvedMedia"));
            Require(!File.Exists(fixture.Archive),
                "Display-style extractor metadata was promoted into a native archive identity");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Clip native key.mp4");
            string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
            File.WriteAllText(stem + ".info.json",
                "{\"id\":\"" + id + "\",\"extractor\":\"youtube:clip\",\"ie_key\":\"YoutubeClip\"}", new UTF8Encoding(false));

            object reconciled = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, false);
            Equal("Healthy", DownloadHistoryStateName(reconciled));
            Equal("youtubeclip " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }
    }

    private static void DownloadHistoryUsesTopLevelInfoJsonIdentity() {
        const string correctId = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Nested Metadata.mp4");
            string stem = Path.Combine(Path.GetDirectoryName(media), Path.GetFileNameWithoutExtension(media));
            File.WriteAllText(stem + ".info.json", "{\"formats\":[{\"id\":\"WRONG_ID_01\",\"extractor_key\":\"WrongExtractor\"}],\"id\":\"" + correctId + "\",\"extractor_key\":\"Youtube\"}", new UTF8Encoding(false));

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "MetadataRecovered"));
            DownloadHistoryReconcile(fixture, string.Empty, false);
            Equal("youtube " + correctId, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(File.Exists(media), "Metadata recovery renamed the original media");
            Require(File.Exists(stem + ".info.json"), "Metadata recovery renamed the original info sidecar");
            Require(!File.Exists(Path.Combine(fixture.Root, "Nested Metadata-" + correctId + ".mp4")), "Metadata recovery created a renamed media path");
            Require(!File.Exists(Path.Combine(fixture.Root, "Nested Metadata-WRONG_ID_01.mp4")), "Nested metadata identity was incorrectly trusted as the media identity");
        }
    }

    private static void DownloadHistoryMigratesLegacyMetadata() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string original = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Legacy Title.mp4", "Youtube", id);
            string metadata = Path.Combine(fixture.Root, "Legacy Title.info.json");
            byte[] mediaBefore = File.ReadAllBytes(original);
            byte[] metadataBefore = File.ReadAllBytes(metadata);
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Missing", DownloadHistoryStateName(analysis));
            Equal(0, Get(analysis, "MigrationCount"));
            Equal(1, Get(analysis, "MetadataRecovered"));

            object reconciled = Call(fixture.History, null, "ReconcileLibrary", string.Empty, true, false);
            Equal("Healthy", DownloadHistoryStateName(reconciled));
            Require(File.Exists(original) && File.Exists(metadata), "Legacy inventory changed existing paths");
            Require(mediaBefore.SequenceEqual(File.ReadAllBytes(original)), "Legacy inventory rewrote media contents");
            Require(metadataBefore.SequenceEqual(File.ReadAllBytes(metadata)), "Legacy inventory rewrote metadata contents");
            Require(!File.Exists(Path.Combine(fixture.Root, "Legacy Title-" + id + ".mp4")), "Legacy inventory created a renamed media path");
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }
    }

    private static void DownloadHistoryMigrationFailureRollsBackMedia() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string directory = Path.Combine(fixture.Root, "youtube.com", "Video", "Casual Geographic");
            Directory.CreateDirectory(directory);
            string media = DownloadHistoryWriteMediaWithInfo(directory, "Animal Facts That I Got WRONG-" + id + ".webm", "Youtube", id);
            string stem = Path.Combine(directory, "Animal Facts That I Got WRONG-" + id);
            File.WriteAllText(stem + ".description", "description", new UTF8Encoding(false));
            File.WriteAllText(stem + ".webp", "thumbnail", new UTF8Encoding(false));
            File.WriteAllText(stem + ".vtt", "subtitle", new UTF8Encoding(false));
            File.WriteAllText(stem + ".live_chat.json", "{\"chat\":true}", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "Casual Geographic - Videos-channel.info.json"), "{\"id\":\"channel\"}", new UTF8Encoding(false));
            string[] paths = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            Dictionary<string, byte[]> before = paths.ToDictionary(path => path, path => File.ReadAllBytes(path), StringComparer.OrdinalIgnoreCase);

            DownloadHistoryReconcile(fixture, string.Empty, false);
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Equal(before.Count, Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly).Length);
            foreach (KeyValuePair<string, byte[]> item in before) {
                Require(File.Exists(item.Key), "Inventory moved or renamed existing library file: " + item.Key);
                Require(item.Value.SequenceEqual(File.ReadAllBytes(item.Key)), "Inventory rewrote existing library file: " + item.Key);
            }
            Require(File.Exists(media), "Inventory moved the representative media file");
        }
    }

    private static void DownloadHistoryBlocksPartialAndUnsafeLibraries() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Known-" + id + ".mp4", "Youtube", id);
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
            File.WriteAllText(fixture.Archive, "youtube 9qFjkwAElDs\r\nyoutube aB_Cd-Ef123\r\n", new UTF8Encoding(false));
            Call(fixture.History, null, "RefreshBackupAfterRun");
            string backupBefore = File.ReadAllText(fixture.Archive + ".bak", new UTF8Encoding(false));
            Require(backupBefore.Contains("youtube aB_Cd-Ef123"), "Successful native archive append was not copied to backup");

            File.WriteAllText(fixture.Archive, "youtube poisoned123\r\ninvalid-line-without-space\r\n", new UTF8Encoding(false));
            Call(fixture.History, null, "RefreshBackupAfterRun");
            Equal(backupBefore, File.ReadAllText(fixture.Archive + ".bak", new UTF8Encoding(false)));

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            string repaired = File.ReadAllText(fixture.Archive, new UTF8Encoding(false));
            Require(repaired.Contains("youtube 9qFjkwAElDs") && repaired.Contains("youtube aB_Cd-Ef123"), "Valid backup was not restored after primary corruption");
            Require(repaired.IndexOf("poisoned123", StringComparison.Ordinal) < 0, "Corrupt primary content survived backup recovery");
        }
    }

    private static void DownloadHistoryValidArchiveTruncationPreservesBackup() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            File.WriteAllText(fixture.Archive, "youtube oldEntry001\r\n", new UTF8Encoding(false));
            Call(fixture.History, null, "RefreshBackupAfterRun");
            Require(DownloadHistoryArchiveLines(fixture.Archive + ".bak").Contains("youtube oldEntry001"), "Known-good archive entry was not backed up");

            File.WriteAllText(fixture.Archive, "youtube newEntry002\r\n", new UTF8Encoding(false));
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
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Recovered-" + id + ".mp4", "Youtube", id);
            DownloadHistoryEnable(fixture, string.Empty);
            Require(File.Exists(fixture.Archive + ".bak"), "Established default archive did not create its expected backup");
            File.WriteAllText(fixture.Archive, "youtube poisoned123\r\ninvalid-line-without-space\r\n", new UTF8Encoding(false));

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Invalid", DownloadHistoryStateName(analysis));
            Equal(true, DownloadHistoryCanReconcile(analysis));
            DownloadHistoryReconcile(fixture, string.Empty, true);
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            Equal(1, lines.Length);
            Equal("youtube " + id, lines[0]);
            Require(lines.All(x => x.IndexOf("poisoned123", StringComparison.Ordinal) < 0), "Parser trusted entries preceding a corrupt established archive line");
        }
    }

    private static void DownloadHistoryRefusesUninitializedDefaultArchiveCollision() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Existing-" + id + ".mp4", "Youtube", id);
            byte[] collision = new UTF8Encoding(false).GetBytes("THIS DEFAULT-NAMED FILE WAS NOT CREATED BY DOWNLOAD HISTORY\r\n");
            File.WriteAllBytes(fixture.Archive, collision);

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Invalid", DownloadHistoryStateName(analysis));
            Equal(false, DownloadHistoryCanReconcile(analysis));

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Invalid", DownloadHistoryStateName(rebuilt));
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Require(collision.SequenceEqual(File.ReadAllBytes(fixture.Archive)),
                "First-use reconciliation overwrote an uninitialized default archive-path collision");
            Require(!File.Exists(fixture.Archive + ".bak"),
                "First-use default collision created a backup and thereby claimed ownership of an unrelated file");
        }
    }

    private static void DownloadHistoryDisableReenableReconcilesChanges() {
        const string first = "9qFjkwAElDs";
        const string second = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "First-" + first + ".mp4", "Youtube", first);
            DownloadHistoryEnable(fixture, string.Empty);
            string beforeDisable = File.ReadAllText(fixture.Archive, new UTF8Encoding(false));
            Require(File.Exists(fixture.Archive + ".bak"), "Backup was not preserved before disable test");

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Equal(string.Empty, arguments);
            Equal(beforeDisable, File.ReadAllText(fixture.Archive, new UTF8Encoding(false)));
            Require(File.Exists(fixture.Archive + ".bak"), "Disabling history deleted the backup");
            Equal("Dormant", fixture.History.GetProperty("LastReport", All).GetValue(null, null).GetType().GetProperty("State", All).GetValue(fixture.History.GetProperty("LastReport", All).GetValue(null, null), null).ToString());

            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Second-" + second + ".webm", "Youtube", second);
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
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Trusted-" + trusted + ".mp4", "Youtube", trusted);
            DownloadHistoryEnable(fixture, string.Empty);
            DownloadHistoryWriteMedia(fixture.Root, "Failed-looking-" + failed + ".mp4");

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false);
            Equal("Partial", DownloadHistoryStateName(rebuilt));
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            Require(lines.Contains("youtube " + trusted), "Trusted archive entry disappeared");
            Require(!lines.Contains("youtube " + failed), "Ambiguous final-looking file was promoted during explicit rebuild");
        }
    }

    private static void DownloadHistoryExplicitRebuildRecoversAuthoritativeMissingEntry() {
        const string trusted = "9qFjkwAElDs";
        const string recovered = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Trusted-" + trusted + ".mp4", "Youtube", trusted);
            DownloadHistoryEnable(fixture, string.Empty);
            string existing = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Existing legacy media.mp4", "Youtube", recovered);

            object normal = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(normal));
            Require(!DownloadHistoryArchiveLines(fixture.Archive).Contains("youtube " + recovered), "Ordinary validation promoted an unarchived physical file");

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            string[] lines = DownloadHistoryArchiveLines(fixture.Archive);
            Require(lines.Contains("youtube " + trusted) && lines.Contains("youtube " + recovered), "Explicit rebuild did not union authoritative physical identity with existing archive history");
            Require(File.Exists(existing), "Explicit rebuild changed the authoritative media path");
            Require(File.Exists(Path.Combine(fixture.Root, "Existing legacy media.info.json")), "Explicit rebuild changed the authoritative metadata path");
        }
    }


    private static void DownloadHistoryInventoriesGifMediaConservatively() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Animated-" + id + ".mp4");
            string gif = DownloadHistoryWriteMedia(fixture.Root, "Animated-" + id + ".gif");
            string info = Path.ChangeExtension(media, ".info.json");
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"ext\":\"mp4\",\"thumbnails\":[{\"ext\":\"gif\",\"url\":\"https://example.invalid/thumb.gif\"}]}", new UTF8Encoding(false));
            byte[] mediaBefore = File.ReadAllBytes(media);
            byte[] gifBefore = File.ReadAllBytes(gif);
            byte[] infoBefore = File.ReadAllBytes(info);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "MetadataRecovered"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(mediaBefore.SequenceEqual(File.ReadAllBytes(media)), "Thumbnail classification modified the real media file");
            Require(gifBefore.SequenceEqual(File.ReadAllBytes(gif)), "Thumbnail classification modified the thumbnail file");
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Thumbnail classification modified adjacent metadata");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string gif = DownloadHistoryWriteMedia(fixture.Root, "Failed-" + id + ".gif");
            File.WriteAllText(Path.ChangeExtension(gif, ".info.json"),
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"ext\":\"gif\",\"thumbnails\":[{\"ext\":\"gif\",\"url\":\"https://example.invalid/thumb.gif\"}]}", new UTF8Encoding(false));

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Unsafe", DownloadHistoryStateName(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "UnresolvedMedia"));
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Require(!DownloadHistoryArchiveLines(fixture.Archive).Contains("youtube " + id),
                "Possible pre-download thumbnail residue invented a native history identity");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string gif = DownloadHistoryWriteMedia(fixture.Root, "Direct-" + id + ".gif");
            File.WriteAllText(Path.ChangeExtension(gif, ".info.json"),
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"ext\":\"gif\",\"thumbnails\":[{\"ext\":\"jpg\",\"url\":\"https://example.invalid/thumb.jpg\"}]}", new UTF8Encoding(false));

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "MetadataRecovered"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string gif = DownloadHistoryWriteMedia(fixture.Root, "Known-" + id + ".gif");
            File.WriteAllText(Path.ChangeExtension(gif, ".info.json"),
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"ext\":\"gif\",\"thumbnails\":[{\"url\":\"https://example.invalid/thumb.gif?size=large\"}]}", new UTF8Encoding(false));
            File.WriteAllText(fixture.Archive, "youtube " + id + Environment.NewLine, new UTF8Encoding(false));

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "CompletedMedia"));
            Equal(0, Get(analysis, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMedia(fixture.Root, "Unknown.gif");
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Unsafe", DownloadHistoryStateName(analysis));
            Equal(1, Get(analysis, "CompletedMedia"));
            Equal(1, Get(analysis, "UnresolvedMedia"));
            Equal(false, DownloadHistoryCanReconcile(analysis));
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            foreach (string custom in new[] {
                "--recode-video gif",
                "--recode-video mp4>gif",
                "--remux-video gif",
                "--remux-video=mp4>gif"
            }) {
                Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(arguments.Contains("--download-archive"), "Compatible GIF postprocessing lost protected archive arguments: " + custom);
                Require(execution != null, "Compatible GIF postprocessing did not retain protected execution context: " + custom);
            }
        }
    }

    private static void DownloadHistoryIgnoresIndexedThumbnailSidecars() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMedia(fixture.Root, "Indexed-" + id + ".mp4");
            string info = Path.ChangeExtension(media, ".info.json");
            string thumbnail = DownloadHistoryWriteMedia(fixture.Root, "Indexed-" + id + ".0.gif");
            File.WriteAllText(info,
                "{\"id\":\"" + id + "\",\"extractor_key\":\"Youtube\",\"ext\":\"mp4\",\"thumbnails\":[{\"id\":0,\"ext\":\"gif\",\"url\":\"https://example.invalid/zero.gif\"},{\"id\":\"1\",\"ext\":\"jpg\",\"url\":\"https://example.invalid/one.jpg\"}]}", new UTF8Encoding(false));
            byte[] mediaBefore = File.ReadAllBytes(media);
            byte[] infoBefore = File.ReadAllBytes(info);
            byte[] thumbnailBefore = File.ReadAllBytes(thumbnail);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", string.Empty, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(1, Get(rebuilt, "CompletedMedia"));
            Equal(1, Get(rebuilt, "MetadataRecovered"));
            Equal(0, Get(rebuilt, "UnresolvedMedia"));
            Equal("youtube " + id, DownloadHistoryArchiveLines(fixture.Archive).Single());
            Require(mediaBefore.SequenceEqual(File.ReadAllBytes(media)), "Indexed-thumbnail scan modified real media");
            Require(infoBefore.SequenceEqual(File.ReadAllBytes(info)), "Indexed-thumbnail scan modified metadata");
            Require(thumbnailBefore.SequenceEqual(File.ReadAllBytes(thumbnail)), "Indexed-thumbnail scan modified sidecar");
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            const string mismatchId = "aB_Cd-Ef123";
            string media = DownloadHistoryWriteMedia(fixture.Root, "Mismatch-" + mismatchId + ".mp4");
            File.WriteAllText(Path.ChangeExtension(media, ".info.json"),
                "{\"id\":\"" + mismatchId + "\",\"extractor_key\":\"Youtube\",\"ext\":\"mp4\",\"thumbnails\":[{\"id\":\"0\",\"ext\":\"gif\",\"url\":\"https://example.invalid/zero.gif\"},{\"id\":\"2\",\"ext\":\"jpg\",\"url\":\"https://example.invalid/two.jpg\"}]}", new UTF8Encoding(false));
            DownloadHistoryWriteMedia(fixture.Root, "Mismatch-" + mismatchId + ".1.gif");

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Partial", DownloadHistoryStateName(analysis));
            Equal(2, Get(analysis, "CompletedMedia"));
            Equal(1, Get(analysis, "UnresolvedMedia"));
            Equal(false, DownloadHistoryCanReconcile(analysis));
        }
    }

    private static void DownloadHistoryIgnoresFailedAndSidecarFiles() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            File.WriteAllText(Path.Combine(fixture.Root, "Video-9qFjkwAElDs.mp4.part"), "partial");
            File.WriteAllText(Path.Combine(fixture.Root, "Video-9qFjkwAElDs.mp4.ytdl"), "partial");
            File.WriteAllText(Path.Combine(fixture.Root, "Video-9qFjkwAElDs.info.json"), "{}", new UTF8Encoding(false));
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
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Trusted-" + trusted + ".mp4", "Youtube", trusted);
            DownloadHistoryEnable(fixture, string.Empty);
            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            Set(fixture.Downloads, null, "fileNameSchema", "%(title)s.%(ext)s");
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Recoverable.mp4", "Youtube", recoverable);
            DownloadHistoryWriteMedia(fixture.Root, "Unresolved.mp4");

            object analysis = Call(fixture.History, null, "AnalyzeLibrary", string.Empty);
            Equal("Partial", DownloadHistoryStateName(analysis));
            Equal(false, DownloadHistoryCanReconcile(analysis));
            Equal(0, Get(analysis, "MigrationCount"));
            Require(File.Exists(Path.Combine(fixture.Root, "Recoverable.mp4")), "Partial inventory mutated recoverable media despite unresolved files");
            Require(!File.Exists(Path.Combine(fixture.Root, "Recoverable-" + recoverable + ".mp4")), "Partial inventory created a renamed media path");
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
            File.WriteAllText(fixture.Archive, "youtube oldEntry001\r\n", new UTF8Encoding(false));
            Call(fixture.History, null, "RefreshBackupAfterRun");

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            File.WriteAllText(fixture.Archive, "youtube newEntry002\r\n", new UTF8Encoding(false));
            Throws<InvalidOperationException>(() => Call(execution.GetType(), execution, "AcquireValidatedLease"));

            object repairedExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out repairedExecution));
            string[] repaired = DownloadHistoryArchiveLines(fixture.Archive);
            Require(repaired.Contains("youtube oldEntry001") && repaired.Contains("youtube newEntry002"), "Regenerating a rejected command did not reconcile the truncated primary archive with its last-good backup");
            using (IDisposable lease = (IDisposable)Call(repairedExecution.GetType(), repairedExecution, "AcquireValidatedLease")) { }
        }
    }



    private static void DownloadHistoryExecutionLeaseRejectsBackupFreePreparedTruncation() {
        const string first = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Prepared-" + first + ".mp4", "Youtube", first);
            object prepared = Call(fixture.History, null, "ReconcileLibrary", string.Empty, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, string.Empty, false, prepared, string.Empty);
            Require(!File.Exists(fixture.Archive + ".bak"), "Backup-free prepared truncation fixture unexpectedly created a backup");

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            File.WriteAllText(fixture.Archive, string.Empty, new UTF8Encoding(false));

            Throws<InvalidOperationException>(() => Call(execution.GetType(), execution, "AcquireValidatedLease"));
            Equal(true, fixture.History.GetProperty("NeedsReconciliation", All).GetValue(null, null));
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Prepared-" + first + ".mp4", "Youtube", first);
            object prepared = Call(fixture.History, null, "ReconcileLibrary", string.Empty, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, string.Empty, false, prepared, string.Empty);

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            File.AppendAllText(fixture.Archive, "youtube aB_Cd-Ef123" + Environment.NewLine, new UTF8Encoding(false));

            using (IDisposable lease = (IDisposable)Call(execution.GetType(), execution, "AcquireValidatedLease")) { }
            Require(DownloadHistoryArchiveLines(fixture.Archive).Contains("youtube " + first),
                "A post-preparation superset append displaced an identity from the prepared ledger snapshot");
        }
    }


    private static void DownloadHistoryPreservesSameSessionLedgerFloorWithoutBackup() {
        const string first = "9qFjkwAElDs";
        const string second = "aB_Cd-Ef123";

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "First-" + first + ".mp4", "Youtube", first);
            object prepared = Call(fixture.History, null, "ReconcileLibrary", string.Empty, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, string.Empty, false, prepared, string.Empty);
            Require(!File.Exists(fixture.Archive + ".bak"), "Same-session floor fixture unexpectedly created a backup");

            File.AppendAllText(fixture.Archive, "youtube " + second + Environment.NewLine, new UTF8Encoding(false));
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));

            File.WriteAllText(fixture.Archive, "youtube " + first + Environment.NewLine, new UTF8Encoding(false));
            object laterExecution;
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out laterExecution));
            Require(error.IndexOf("identit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("rebuild", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("ledger", StringComparison.OrdinalIgnoreCase) >= 0,
                "Later preparation did not explain the same-session ledger shrink");
            Equal(null, laterExecution);
        }

        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "First-" + first + ".mp4", "Youtube", first);
            object prepared = Call(fixture.History, null, "ReconcileLibrary", string.Empty, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, string.Empty, false, prepared, string.Empty);

            string arguments, error;
            object oldExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out oldExecution));

            using (IDisposable lease = (IDisposable)Call(oldExecution.GetType(), oldExecution, "AcquireValidatedLease")) {
                File.AppendAllText(fixture.Archive, "youtube " + second + Environment.NewLine, new UTF8Encoding(false));
                Call(oldExecution.GetType(), oldExecution, "RefreshBackupAfterRun");
            }
            Require(!File.Exists(fixture.Archive + ".bak"), "No-backup provider completion unexpectedly created a backup");

            File.WriteAllText(fixture.Archive, "youtube " + first + Environment.NewLine, new UTF8Encoding(false));
            Throws<InvalidOperationException>(() => Call(oldExecution.GetType(), oldExecution, "AcquireValidatedLease"));
        }
    }

    private static void DownloadHistoryRetentionOffDoesNotRestoreFromStaleBackupAlone() {
        const string first = "9qFjkwAElDs";
        const string newer = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "First-" + first + ".mp4", "Youtube", first);
            DownloadHistoryEnable(fixture, string.Empty);
            Require(File.Exists(fixture.Archive + ".bak"), "Retention-off stale-backup test did not establish a valid backup");

            fixture.History.GetField("fKeepBackup", All).SetValue(null, false);
            File.AppendAllText(fixture.Archive, "youtube " + newer + Environment.NewLine, new UTF8Encoding(false));
            string[] beforeLoss = DownloadHistoryArchiveLines(fixture.Archive);
            Require(beforeLoss.Contains("youtube " + first) && beforeLoss.Contains("youtube " + newer),
                "Retention-off stale-backup test did not establish a newer primary-only identity");

            File.Delete(fixture.Archive);
            fixture.History.GetField("PreparedKey", All).SetValue(null, null);

            string arguments, error;
            object execution;
            Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(error.IndexOf("rebuild", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    error.IndexOf("backup", StringComparison.OrdinalIgnoreCase) >= 0,
                "Retention-off primary loss did not fail with an actionable rebuild/backup error");
            Require(!File.Exists(fixture.Archive),
                "Normal protected preparation recreated a missing primary from a backup that may be stale because retention is off");
            Equal(null, execution);
        }
    }

    private static void DownloadHistoryArchiveRelocationRejectsStaleBackupOnlySource() {
        const string first = "9qFjkwAElDs";
        const string newer = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "First-" + first + ".mp4", "Youtube", first);
            DownloadHistoryEnable(fixture, string.Empty);
            string oldArchive = fixture.Archive;
            Require(File.Exists(oldArchive + ".bak"), "Archive-relocation stale-backup test did not establish a valid backup");

            fixture.History.GetField("fKeepBackup", All).SetValue(null, false);
            File.AppendAllText(oldArchive, "youtube " + newer + Environment.NewLine, new UTF8Encoding(false));
            File.Delete(oldArchive);

            string replacement = Path.Combine(fixture.Root, "replacement-history.txt");
            object prepared = Call(fixture.History, null, "RebuildLibrary", replacement, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Require(DownloadHistoryArchiveLines(replacement).Contains("youtube " + first),
                "Replacement candidate did not recover current physical media");

            Throws<InvalidOperationException>(() =>
                Call(fixture.History, null, "CommitSettings", true, replacement, false, prepared, string.Empty));
            Equal(Path.GetFullPath(oldArchive), fixture.History.GetProperty("BoundArchivePath", All).GetValue(null, null));
            Require(!DownloadHistoryArchiveLines(replacement).Contains("youtube " + newer),
                "Stale retained backup unexpectedly supplied a newer identity that it never contained");
        }
    }

    private static void DownloadHistoryExecutionLeaseUsesExistingBackupWhenRetentionOff() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            File.WriteAllText(fixture.Archive, "youtube oldEntry001\r\n", new UTF8Encoding(false));
            Call(fixture.History, null, "RefreshBackupAfterRun");
            Require(File.Exists(fixture.Archive + ".bak"), "Last-good backup was not established for retention-off execution test");

            fixture.History.GetField("fKeepBackup", All).SetValue(null, false);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));

            File.WriteAllText(fixture.Archive, "youtube newEntry002\r\n", new UTF8Encoding(false));
            Throws<InvalidOperationException>(() => Call(execution.GetType(), execution, "AcquireValidatedLease"));

            object repairedExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out repairedExecution));
            string[] repaired = DownloadHistoryArchiveLines(fixture.Archive);
            Require(repaired.Contains("youtube oldEntry001") && repaired.Contains("youtube newEntry002"),
                "Regenerating after retention-off truncation did not union the valid existing backup into the primary");
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


    private static void DownloadHistoryFirstUseRaceAcquiresFileLockAfterDirectoryAppears() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(false)) {
            IDisposable lease = null;
            try {
                lease = (IDisposable)Call(fixture.History, null, "AcquireArchiveLease");
                Require(!Directory.Exists(fixture.Root), "First-use race fixture unexpectedly created the library while acquiring the lease");

                Directory.CreateDirectory(fixture.Root); // competing session creates the default archive parent
                object[] initialize = { fixture.Root, fixture.Archive, lease, null };
                Equal(true, Call(fixture.History, null, "TryInitializeNewDefaultLibrary", initialize));

                bool blocked = false;
                try {
                    using (FileStream competing = new FileStream(fixture.Archive + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
                }
                catch (IOException) { blocked = true; }
                Require(blocked, "First-use initialization observed an existing directory but failed to upgrade the lease to the cross-session file lock");
            }
            finally {
                if (lease != null) lease.Dispose();
            }

            using (FileStream afterRelease = new FileStream(fixture.Archive + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
        }
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

    private static void DownloadHistoryLeaseWaitHonorsCancellation() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            IDisposable first = (IDisposable)Call(execution.GetType(), execution, "AcquireValidatedLease");
            ManualResetEvent started = new ManualResetEvent(false);
            ManualResetEvent cancel = new ManualResetEvent(false);
            ManualResetEvent finished = new ManualResetEvent(false);
            Exception workerError = null;
            Thread worker = new Thread(delegate() {
                started.Set();
                try {
                    Func<bool> cancelled = delegate() { return cancel.WaitOne(0); };
                    using (IDisposable ignored = (IDisposable)Call(execution.GetType(), execution, "AcquireValidatedLease", cancelled)) { }
                    workerError = new Exception("Cancelled waiter unexpectedly acquired the protected archive lease");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { workerError = ex; }
                finally { finished.Set(); }
            });
            worker.IsBackground = true;
            try {
                worker.Start();
                Require(started.WaitOne(2000), "Protected lease waiter did not start");
                Thread.Sleep(200);
                Require(!finished.WaitOne(0), "Protected lease waiter did not actually block behind the active worker");
                cancel.Set();
                Require(finished.WaitOne(2000), "Cancellation did not interrupt the protected archive lease wait");
                Require(worker.Join(2000), "Cancelled protected lease waiter did not finish");
                if (workerError != null) throw workerError;
            }
            finally {
                first.Dispose();
                started.Dispose();
                cancel.Dispose();
                finished.Dispose();
            }
        }
    }

    private static void DownloadHistoryCancellationIsRecheckedBeforeProcessStart() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string standard = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloader.cs"));
        string extended = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmExtendedDownloader.cs"));

        int standardLease = standard.IndexOf("using DownloadHistoryLease? DownloadLease = HistoryExecution?.AcquireValidatedLease(() =>", StringComparison.Ordinal);
        int standardGuard = standard.IndexOf("CancellationRequested || CurrentDownload.Status == DownloadStatus.Aborted || CurrentDownload.Status == DownloadStatus.AbortForClose", standardLease, StringComparison.Ordinal);
        int standardStart = standard.IndexOf("DownloadProcess.Start();", standardLease, StringComparison.Ordinal);
        Require(standardLease >= 0 && standardGuard > standardLease && standardStart > standardGuard, "Standard worker can start after cancellation while waiting for the archive lease");

        int normalLease = extended.IndexOf("using DownloadHistoryLease? DownloadLease = HistoryExecution?.AcquireValidatedLease(() =>", StringComparison.Ordinal);
        int normalGuard = extended.IndexOf("CancellationRequested || Status == DownloadStatus.Aborted || Status == DownloadStatus.AbortForClose", normalLease, StringComparison.Ordinal);
        int normalStart = extended.IndexOf("DownloadProcess.Start();", normalLease, StringComparison.Ordinal);
        Require(normalLease >= 0 && normalGuard > normalLease && normalStart > normalGuard, "Extended worker can start after cancellation while waiting for the archive lease");

        int batchLease = extended.IndexOf("using DownloadHistoryLease? DownloadLease = BatchHistoryExecution?.AcquireValidatedLease(() =>", StringComparison.Ordinal);
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
            Require(settingsSource.IndexOf("Disable Download History before changing libraries", StringComparison.Ordinal) < 0, "Main Settings still blocks path-agnostic download-folder changes while history is enabled");
            Require(settingsSource.Contains("unsavedSchema") && settingsSource.Contains("Unsaved download settings"), "Opening Download History does not warn about an unsaved filename-format change");
            Require(settingsSource.Contains("history.ShowDialog(this) != DialogResult.OK"), "Cancelling Download History can overwrite unsaved filename-format UI state");
            Require(settingsSource.Contains("Download History requires yt-dlp or yt-dlp nightly") && settingsSource.Contains("selectedProvider is not"), "Main Settings does not prevent an incompatible provider switch while history is enabled");
            string historyDialogSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloadHistory.cs"));
            Require(historyDialogSource.Contains("Reset History operates only on the currently saved archive"), "Reset History does not guard against an unsaved archive-path change");
        }
    }

    private static void DownloadHistoryLibraryBindingPreventsCrossLibraryReuse() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ledgerDirectory = Path.Combine(fixture.Root, "ledger");
            Directory.CreateDirectory(ledgerDirectory);
            string custom = Path.Combine(ledgerDirectory, "history.txt");
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Original-" + id + ".mp4", "Youtube", id);
            DownloadHistoryEnable(fixture, custom);

            string secondLibrary = Path.Combine(fixture.Root, "second-library");
            Directory.CreateDirectory(secondLibrary);
            Set(fixture.Downloads, null, "downloadPath", secondLibrary);
            object analysis = Call(fixture.History, null, "AnalyzeLibrary", custom);
            Equal("Healthy", DownloadHistoryStateName(analysis));
            Equal(true, DownloadHistoryCanReconcile(analysis));
            Require(DownloadHistoryArchiveLines(custom).Contains("youtube " + id), "Changing physical media roots invalidated an existing native archive identity");
        }
    }


    private static void DownloadHistoryActiveRootChangeTriggersInventoryRecovery() {
        const string first = "9qFjkwAElDs";
        const string second = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string initial = Path.Combine(fixture.Root, "initial-downloads");
            string next = Path.Combine(fixture.Root, "next-downloads");
            string ledger = Path.Combine(fixture.Root, "ledger");
            Directory.CreateDirectory(initial);
            Directory.CreateDirectory(next);
            Directory.CreateDirectory(ledger);
            Set(fixture.Downloads, null, "downloadPath", initial);

            DownloadHistoryWriteMediaWithInfo(initial, "Initial-" + first + ".mp4", "Youtube", first);
            string archive = Path.Combine(ledger, "history.txt");
            object prepared = Call(fixture.History, null, "RebuildLibrary", archive, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, archive, true, prepared, string.Empty);
            Require(DownloadHistoryArchiveLines(archive).Contains("youtube " + first), "Initial active-root identity was not archived");

            Set(fixture.Downloads, null, "downloadPath", next);
            DownloadHistoryWriteMediaWithInfo(next, "AlreadyHere-" + second + ".webm", "Youtube", second);

            object reconciled = Call(fixture.History, null, "ReconcileLibrary", archive, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(reconciled));
            string[] entries = DownloadHistoryArchiveLines(archive);
            Require(entries.Contains("youtube " + first), "Changing the active inventory root pruned prior history");
            Require(entries.Contains("youtube " + second), "Ordinary management reconciliation ignored authoritative media in the newly selected active root");
        }
    }

    private static void DownloadHistoryRefusesUnrecognizedArchiveOverwrite() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string collision = DownloadHistoryWriteMediaWithInfo(fixture.Root, "DoNotReplace-" + id + ".mp4", "Youtube", id);
            byte[] before = File.ReadAllBytes(collision);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", collision, true, false, string.Empty);

            Require(before.SequenceEqual(File.ReadAllBytes(collision)),
                "Rebuild overwrote an existing unbound user file that was selected as the archive path");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Require(DownloadHistoryStateName(rebuilt) == "Invalid" || DownloadHistoryStateName(rebuilt) == "Unsafe",
                "Unrecognized invalid archive collision was not rejected fail-closed");
        }
    }


    private static void DownloadHistoryCompanionLockAndTempFilesAreNonDestructive() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string custom = Path.Combine(fixture.Root, "companion-history.txt");
            string lockPath = custom + ".lock";
            byte[] lockBytes = new UTF8Encoding(false).GetBytes("user-lock-sentinel");
            File.WriteAllBytes(lockPath, lockBytes);
            fixture.History.GetField("fArchivePath", All).SetValue(null, custom);

            using (IDisposable lease = (IDisposable)Call(fixture.History, null, "AcquireArchiveLease")) { }
            Require(File.Exists(lockPath), "Archive lease disposal deleted a pre-existing lock-path file");
            Require(lockBytes.SequenceEqual(File.ReadAllBytes(lockPath)), "Archive lease modified a pre-existing lock-path file");

            string tempPath = custom + ".tmp";
            byte[] tempBytes = new UTF8Encoding(false).GetBytes("user-temp-sentinel");
            File.WriteAllBytes(tempPath, tempBytes);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", custom, false, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Require(File.Exists(tempPath), "Atomic archive creation consumed a pre-existing fixed temp-path file");
            Require(tempBytes.SequenceEqual(File.ReadAllBytes(tempPath)), "Atomic archive creation overwrote a pre-existing fixed temp-path file");
        }
    }

    private static void DownloadHistoryResetPreservesUnownedTempCompanions() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string custom = Path.Combine(fixture.Root, "reset-history.txt");
            File.WriteAllText(custom, "youtube 9qFjkwAElDs" + Environment.NewLine, new UTF8Encoding(false));
            string temp = custom + ".tmp";
            string backupTemp = custom + ".bak.tmp";
            byte[] tempBytes = new UTF8Encoding(false).GetBytes("user-primary-temp");
            byte[] backupTempBytes = new UTF8Encoding(false).GetBytes("user-backup-temp");
            File.WriteAllBytes(temp, tempBytes);
            File.WriteAllBytes(backupTemp, backupTempBytes);

            fixture.History.GetField("fArchivePath", All).SetValue(null, custom);
            fixture.History.GetField("fEnabled", All).SetValue(null, false);
            Call(fixture.History, null, "ResetHistory");

            Require(File.Exists(temp) && tempBytes.SequenceEqual(File.ReadAllBytes(temp)),
                "Reset History deleted or modified an unowned fixed primary temp-path collision");
            Require(File.Exists(backupTemp) && backupTempBytes.SequenceEqual(File.ReadAllBytes(backupTemp)),
                "Reset History deleted or modified an unowned fixed backup temp-path collision");
        }
    }

    private static void DownloadHistoryRefusesInvalidBackupCollisionBeforePrimaryMutation() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string custom = Path.Combine(fixture.Root, "backup-collision-history.txt");
            string backup = custom + ".bak";
            byte[] backupBytes = new UTF8Encoding(false).GetBytes("THIS IS USER DATA, NOT A NATIVE ARCHIVE" + Environment.NewLine);
            File.WriteAllBytes(backup, backupBytes);
            DownloadHistoryWriteMediaWithInfo(fixture.Root, "Existing-" + id + ".mp4", "Youtube", id);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", custom, true, false, string.Empty);

            Require(!File.Exists(custom), "Rebuild mutated the primary archive before rejecting an invalid backup collision");
            Require(File.Exists(backup) && backupBytes.SequenceEqual(File.ReadAllBytes(backup)),
                "Rebuild overwrote an invalid pre-existing backup-path collision");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Require(DownloadHistoryStateName(rebuilt) == "Invalid" || DownloadHistoryStateName(rebuilt) == "Unavailable",
                "Invalid backup collision was not rejected fail-closed");
        }
    }

    private static void DownloadHistoryBoundCustomArchiveCorruptionRequiresExplicitReset() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string ledgerDirectory = Path.Combine(fixture.Root, "ledger");
            Directory.CreateDirectory(ledgerDirectory);
            string custom = Path.Combine(ledgerDirectory, "bound-history.txt");
            string media = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Bound-" + id + ".mp4", "Youtube", id);

            object prepared = Call(fixture.History, null, "RebuildLibrary", custom, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, custom, true, prepared, string.Empty);
            Require(File.Exists(custom), "Bound custom archive was not created");
            Require(File.Exists(custom + ".bak"), "Bound custom archive backup was not created");

            File.Delete(custom + ".bak");
            byte[] corrupt = new UTF8Encoding(false).GetBytes("THIS IS NOT A NATIVE ARCHIVE\r\n");
            File.WriteAllBytes(custom, corrupt);
            byte[] mediaBefore = File.ReadAllBytes(media);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", custom, true, false, string.Empty);

            Require(corrupt.SequenceEqual(File.ReadAllBytes(custom)),
                "Rebuild overwrote a corrupt bound custom archive without a valid backup");
            Require(mediaBefore.SequenceEqual(File.ReadAllBytes(media)),
                "Refused custom-archive recovery modified existing media");
            Equal(false, DownloadHistoryCanReconcile(rebuilt));
            Equal("Invalid", DownloadHistoryStateName(rebuilt));
        }
    }

    private static void DownloadHistoryArchiveFileIsNotInventoryMedia() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string archive = Path.Combine(fixture.Root, "application-history.mp4");
            object prepared = Call(fixture.History, null, "RebuildLibrary", archive, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, archive, true, prepared, string.Empty);

            object rebuilt = Call(fixture.History, null, "RebuildLibrary", archive, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(0, Get(rebuilt, "CompletedMedia"));
            Require(File.Exists(archive), "Explicit rebuild removed the application-owned archive");
        }
    }


    private static void DownloadHistoryArchiveRelocationPreservesLedgerOnlyIdentities() {
        const string physical = "9qFjkwAElDs";
        const string ledgerOnly = "ledgerOnlyIdentity001";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string initial = Path.Combine(fixture.Root, "initial-downloads");
            string next = Path.Combine(fixture.Root, "next-downloads");
            string offline = Path.Combine(fixture.Root, "offline-media");
            Directory.CreateDirectory(initial);
            Directory.CreateDirectory(next);
            Directory.CreateDirectory(offline);
            Set(fixture.Downloads, null, "downloadPath", initial);

            string media = DownloadHistoryWriteMediaWithInfo(initial, "Original-" + physical + ".mp4", "Youtube", physical);
            string metadata = Path.Combine(initial, "Original-" + physical + ".info.json");
            DownloadHistoryEnable(fixture, string.Empty);
            string oldArchive = (string)fixture.History.GetProperty("EffectiveArchivePath", All).GetValue(null, null);
            File.AppendAllText(oldArchive, "youtube " + ledgerOnly + Environment.NewLine, new UTF8Encoding(false));
            Call(fixture.History, null, "RefreshBackupAfterRun");
            Require(DownloadHistoryArchiveLines(oldArchive).Contains("youtube " + ledgerOnly),
                "Archive-only historical identity was not established before relocation");

            File.Move(media, Path.Combine(offline, Path.GetFileName(media)));
            File.Move(metadata, Path.Combine(offline, Path.GetFileName(metadata)));
            Set(fixture.Downloads, null, "downloadPath", next);

            string newArchive = Path.Combine(next, "relocated-history.txt");
            object prepared = Call(fixture.History, null, "ReconcileLibrary", newArchive, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Call(fixture.History, null, "CommitSettings", true, newArchive, true, prepared, string.Empty);

            string[] migrated = DownloadHistoryArchiveLines(newArchive);
            Require(migrated.Contains("youtube " + physical),
                "Archive relocation lost an identity whose physical media was no longer in a current scan root");
            Require(migrated.Contains("youtube " + ledgerOnly),
                "Archive relocation lost a ledger-only historical identity");
            Equal(Path.GetFullPath(newArchive), fixture.History.GetProperty("EffectiveArchivePath", All).GetValue(null, null));
            Require(File.Exists(oldArchive), "Archive relocation destructively removed the previous ledger");
        }
    }

    private static void DownloadHistoryArchiveRelocationRequiresReadablePreviousLedger() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string initial = Path.Combine(fixture.Root, "initial-downloads");
            string next = Path.Combine(fixture.Root, "next-downloads");
            Directory.CreateDirectory(initial);
            Directory.CreateDirectory(next);
            Set(fixture.Downloads, null, "downloadPath", initial);

            string media = DownloadHistoryWriteMediaWithInfo(initial, "Move-" + id + ".mp4", "Youtube", id);
            string metadata = Path.Combine(initial, "Move-" + id + ".info.json");
            DownloadHistoryEnable(fixture, string.Empty);
            string oldArchive = (string)fixture.History.GetProperty("EffectiveArchivePath", All).GetValue(null, null);
            if (File.Exists(oldArchive)) File.Delete(oldArchive);
            if (File.Exists(oldArchive + ".bak")) File.Delete(oldArchive + ".bak");

            File.Move(media, Path.Combine(next, Path.GetFileName(media)));
            File.Move(metadata, Path.Combine(next, Path.GetFileName(metadata)));
            Set(fixture.Downloads, null, "downloadPath", next);

            string newArchive = Path.Combine(next, "replacement-history.txt");
            object prepared = Call(fixture.History, null, "ReconcileLibrary", newArchive, true, false, string.Empty);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            Throws<InvalidOperationException>(() =>
                Call(fixture.History, null, "CommitSettings", true, newArchive, true, prepared, string.Empty));
            Equal(Path.GetFullPath(oldArchive), fixture.History.GetProperty("BoundArchivePath", All).GetValue(null, null));
        }
    }

    private static void DownloadHistoryDialogCanSelectNewDefaultArchiveAfterRootChange() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string initial = Path.Combine(fixture.Root, "initial-downloads");
            string next = Path.Combine(fixture.Root, "next-downloads");
            Directory.CreateDirectory(initial);
            Directory.CreateDirectory(next);
            Set(fixture.Downloads, null, "downloadPath", initial);
            DownloadHistoryEnable(fixture, string.Empty);

            string oldBound = (string)fixture.History.GetProperty("BoundArchivePath", All).GetValue(null, null);
            Set(fixture.Downloads, null, "downloadPath", next);
            string newDefault = (string)fixture.History.GetProperty("DefaultArchivePath", All).GetValue(null, null);
            Type dialog = T("youtube_dl_gui.frmDownloadHistory");

            string normalizedNew = (string)Call(dialog, null, "NormalizeConfiguredPath", newDefault);
            Require(!string.IsNullOrWhiteSpace(normalizedNew),
                "Selecting the new active root's default archive path collapsed back to the old bound archive");
            Equal(Path.GetFullPath(newDefault), Path.GetFullPath(normalizedNew));

            string normalizedBound = (string)Call(dialog, null, "NormalizeConfiguredPath", oldBound);
            Equal(string.Empty, normalizedBound);
        }
    }


    private static void DownloadHistoryResetUsesEffectiveArchiveForImplicitBoundPath() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string dialogSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloadHistory.cs"));

        int resetStart = dialogSource.IndexOf("private void ResetHistory(object? sender, EventArgs e)", StringComparison.Ordinal);
        int saveStart = dialogSource.IndexOf("private async void SaveAndClose", resetStart, StringComparison.Ordinal);
        Require(resetStart >= 0 && saveStart > resetStart, "Could not inspect Download History Reset flow");
        string resetSource = dialogSource.Substring(resetStart, saveStart - resetStart);

        Require(resetSource.Contains("? DownloadHistory.EffectiveArchivePath"),
            "Reset History does not resolve an implicit/bound archive to the saved effective archive");
        Require(resetSource.IndexOf("? DownloadHistory.DefaultArchivePath", StringComparison.Ordinal) < 0,
            "Reset History still substitutes the current active root's default for an implicit bound archive");
        Require(resetSource.Contains("Reset History operates only on the currently saved archive"),
            "Reset History lost its protection against a genuinely unsaved archive-path edit");
    }

    private static void DownloadHistoryPathAgnosticHistorySurvivesMediaMoves() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string media = DownloadHistoryWriteMediaWithInfo(fixture.Root, "Movable-" + id + ".mp4", "Youtube", id);
            string metadata = Path.Combine(fixture.Root, "Movable-" + id + ".info.json");
            DownloadHistoryEnable(fixture, string.Empty);
            string archive = (string)fixture.History.GetProperty("EffectiveArchivePath", All).GetValue(null, null);

            string arguments, error;
            object preparedExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out preparedExecution));

            string relocated = Path.Combine(fixture.Root, "relocated");
            Directory.CreateDirectory(relocated);
            File.Move(media, Path.Combine(relocated, Path.GetFileName(media)));
            File.Move(metadata, Path.Combine(relocated, Path.GetFileName(metadata)));
            string futureDownloads = Path.Combine(fixture.Root, "future-downloads");
            Directory.CreateDirectory(futureDownloads);
            Set(fixture.Downloads, null, "downloadPath", futureDownloads);

            Equal(archive, fixture.History.GetProperty("EffectiveArchivePath", All).GetValue(null, null));
            using (IDisposable lease = (IDisposable)Call(preparedExecution.GetType(), preparedExecution, "AcquireValidatedLease")) { }
            fixture.History.GetField("PreparedKey", All).SetValue(null, null);
            object nextExecution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out nextExecution));
            Require(arguments.Contains("--download-archive \"" + archive + "\""), "Changing media paths silently rebound the native archive");
            Require(DownloadHistoryArchiveLines(archive).Contains("youtube " + id), "Moving media removed its durable native archive identity");
        }
    }

    private static void DownloadHistoryMultipleInventoryRootsShareOneArchive() {
        const string first = "9qFjkwAElDs";
        const string second = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string active = Path.Combine(fixture.Root, "downloads");
            string existing = Path.Combine(fixture.Root, "existing-library");
            string nested = Path.Combine(existing, "youtube.com", "Video", "Creator");
            Directory.CreateDirectory(active);
            Directory.CreateDirectory(nested);
            Set(fixture.Downloads, null, "downloadPath", active);

            DownloadHistoryWriteMediaWithInfo(active, "Active-" + first + ".mp4", "Youtube", first);
            DownloadHistoryWriteMediaWithInfo(existing, "Duplicate-" + first + ".webm", "Youtube", first);
            string nestedMedia = DownloadHistoryWriteMediaWithInfo(nested, "Existing library item.webm", "Youtube", second);
            string nestedStem = Path.Combine(nested, "Existing library item");
            File.WriteAllText(nestedStem + ".description", "description", new UTF8Encoding(false));
            File.WriteAllText(nestedStem + ".webp", "thumbnail", new UTF8Encoding(false));
            Dictionary<string, byte[]> before = Directory.GetFiles(existing, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, path => File.ReadAllBytes(path), StringComparer.OrdinalIgnoreCase);

            string customArchive = Path.Combine(fixture.Root, "history.txt");
            string configuredRoots = existing + "|" + nested;
            object rebuilt = Call(fixture.History, null, "RebuildLibrary", customArchive, true, false, configuredRoots);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Equal(3, Get(rebuilt, "CompletedMedia"));
            Equal(2, Get(rebuilt, "ArchiveEntries"));
            string[] archiveLines = DownloadHistoryArchiveLines(customArchive);
            Require(archiveLines.Contains("youtube " + first) && archiveLines.Contains("youtube " + second), "Multiple inventory roots were not unioned into one native archive");
            Require(archiveLines.Length == 2, "Duplicate identities across inventory roots produced duplicate archive records");
            foreach (KeyValuePair<string, byte[]> item in before) {
                Require(File.Exists(item.Key), "Scan-only inventory moved or renamed a library file: " + item.Key);
                Require(item.Value.SequenceEqual(File.ReadAllBytes(item.Key)), "Scan-only inventory rewrote a library file: " + item.Key);
            }
            Require(File.Exists(nestedMedia), "Scan-only inventory moved the nested media file");

            Call(fixture.History, null, "CommitSettings", true, customArchive, true, rebuilt, configuredRoots);
            string persisted = (string)fixture.History.GetProperty("InventoryRoots", All).GetValue(null, null);
            Equal(Path.GetFullPath(existing), persisted);
        }
    }


    private static void DownloadHistoryNormalProtectionIgnoresOfflineInventoryRoots() {
        const string id = "9qFjkwAElDs";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string active = Path.Combine(fixture.Root, "downloads");
            string existing = Path.Combine(fixture.Root, "existing-library");
            Directory.CreateDirectory(active);
            Directory.CreateDirectory(existing);
            Set(fixture.Downloads, null, "downloadPath", active);
            DownloadHistoryWriteMediaWithInfo(existing, "Existing-" + id + ".mp4", "Youtube", id);

            string customArchive = Path.Combine(fixture.Root, "history.txt");
            object rebuilt = Call(fixture.History, null, "RebuildLibrary", customArchive, true, false, existing);
            Equal("Healthy", DownloadHistoryStateName(rebuilt));
            Call(fixture.History, null, "CommitSettings", true, customArchive, true, rebuilt, existing);

            string offline = existing + "-offline";
            Directory.Move(existing, offline);
            fixture.History.GetField("PreparedKey", All).SetValue(null, null); // simulate application restart/cache loss

            string arguments, error;
            object execution;
            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(arguments.Contains("--download-archive \"" + customArchive + "\""), "Healthy path-agnostic archive was not used after its scan-only root went offline");
            Require(!Directory.Exists(existing), "Normal protected command generation recreated or required the offline scan-only root");
            Require(DownloadHistoryArchiveLines(customArchive).Contains("youtube " + id), "Offline inventory root invalidated an already-recorded native identity");
        }
    }

    private static void DownloadHistoryPreparedCommitDoesNotRescanInventory() {
        const string id = "aB_Cd-Ef123";
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            string active = Path.Combine(fixture.Root, "downloads");
            string existing = Path.Combine(fixture.Root, "existing-library");
            Directory.CreateDirectory(active);
            Directory.CreateDirectory(existing);
            Set(fixture.Downloads, null, "downloadPath", active);
            DownloadHistoryWriteMediaWithInfo(existing, "Prepared-" + id + ".webm", "Youtube", id);

            string customArchive = Path.Combine(fixture.Root, "history.txt");
            object prepared = Call(fixture.History, null, "ReconcileLibrary", customArchive, true, false, existing);
            Equal("Healthy", DownloadHistoryStateName(prepared));
            string offline = existing + "-offline";
            Directory.Move(existing, offline);

            Call(fixture.History, null, "CommitSettings", true, customArchive, true, prepared, existing);
            Equal(true, fixture.History.GetProperty("Enabled", All).GetValue(null, null));
            Require(DownloadHistoryArchiveLines(customArchive).Contains("youtube " + id), "Prepared archive identity was lost when the scan-only root moved before settings commit");
            Require(!Directory.Exists(existing), "CommitSettings rescanned or recreated a scan-only inventory root after a healthy prepared reconciliation");
        }
    }

    private static void DownloadHistoryLargeLibraryManagementAvoidsRepeatedScans() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string historySource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Classes", "DownloadHistory.cs"));
        string dialogSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloadHistory.cs"));

        Require(historySource.IndexOf("List<string> mediaFiles = []", StringComparison.Ordinal) < 0,
            "Large-library inventory still materializes the complete media-file list before processing");
        Require(historySource.Contains("FileNameIdentityMatcher"),
            "Filename-only recovery still lacks a reusable archive identity matcher");
        Require(historySource.IndexOf("TryRecoverFromFilename(string mediaPath, HashSet<string> archiveEntries)", StringComparison.Ordinal) < 0,
            "Filename-only recovery still scans the complete archive separately for each media file");

        int commitStart = historySource.IndexOf("public static void CommitSettings(bool enabled, string configuredArchivePath, bool keepBackup, DownloadHistoryReport? preparedReport, string configuredInventoryRoots)", StringComparison.Ordinal);
        int validateStart = historySource.IndexOf("public static DownloadHistoryReport ValidateAndReconcile", commitStart, StringComparison.Ordinal);
        Require(commitStart >= 0 && validateStart > commitStart, "Could not inspect Download History settings commit flow");
        string commitSource = historySource.Substring(commitStart, validateStart - commitStart);
        Require(commitSource.IndexOf("AnalyzeCore(", StringComparison.Ordinal) < 0,
            "CommitSettings still performs a redundant full physical-library scan after a healthy prepared reconciliation");

        int rebuildStart = dialogSource.IndexOf("private async void RebuildArchive()", StringComparison.Ordinal);
        int refreshStart = dialogSource.IndexOf("private void RefreshStatus", rebuildStart, StringComparison.Ordinal);
        Require(rebuildStart >= 0 && refreshStart > rebuildStart, "Rebuild Archive is not an asynchronous management operation");
        string rebuildSource = dialogSource.Substring(rebuildStart, refreshStart - rebuildStart);
        Require(rebuildSource.IndexOf("AnalyzeLibrary(", StringComparison.Ordinal) < 0,
            "Rebuild Archive still performs a redundant analysis scan before the explicit rebuild scan");

        int saveStart = dialogSource.IndexOf("private async void SaveAndClose", StringComparison.Ordinal);
        Require(saveStart >= 0, "Save/enable does not run long inventory work asynchronously");
        string saveSource = dialogSource.Substring(saveStart);
        Require(saveSource.IndexOf("AnalyzeLibrary(", StringComparison.Ordinal) < 0,
            "Save/enable still performs a redundant analysis scan before reconciliation");
        Require(dialogSource.Contains("Task.Run") && dialogSource.Contains("managementOperationInProgress") && dialogSource.Contains("FormClosing"),
            "Long Download History management operations are not kept off the UI thread with re-entry/close protection");
    }


    private static void DownloadHistorySettingsPersistenceFailsClosed() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string historySource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Classes", "DownloadHistory.cs"));

        int helperStart = historySource.IndexOf("private static void PersistSettingsFailClosed(", StringComparison.Ordinal);
        Require(helperStart >= 0, "Download History settings persistence does not use a dedicated fail-closed write helper");
        int helperEnd = historySource.IndexOf("\n    private static ", helperStart + 1, StringComparison.Ordinal);
        Require(helperEnd > helperStart, "Could not inspect the fail-closed Download History settings persistence helper");
        string helper = historySource.Substring(helperStart, helperEnd - helperStart);

        string disableWrite = "IniProvider.Write(false, ConfigName, nameof(Enabled));";
        string finalWrite = "IniProvider.Write(enabled, ConfigName, nameof(Enabled));";
        int guard = helper.IndexOf(disableWrite, StringComparison.Ordinal);
        int final = helper.LastIndexOf(finalWrite, StringComparison.Ordinal);
        Require(guard >= 0 && final > guard, "Settings persistence does not guard the durable state as disabled before dependent writes");
        Require(helper.IndexOf(disableWrite, guard + disableWrite.Length, StringComparison.Ordinal) < 0,
            "Fail-closed settings helper contains an unexpected second disabled guard write");
        Require(helper.IndexOf(finalWrite, 0, StringComparison.Ordinal) == final,
            "The intended Enabled state is written more than once by the fail-closed settings helper");

        foreach (string dependent in new[] {
            "nameof(ArchivePath)", "nameof(KeepBackup)", "nameof(FailIfUnavailable)", "nameof(EverEnabled)",
            "nameof(NeedsReconciliation)", "nameof(BoundLibraryRoot)", "nameof(BoundArchivePath)", "nameof(InventoryRoots)"
        }) {
            int write = helper.IndexOf(dependent, StringComparison.Ordinal);
            Require(write > guard && write < final, "Dependent Download History setting is not durably written between the disabled guard and final enable: " + dependent);
        }

        int commitStart = historySource.IndexOf("public static void CommitSettings(bool enabled, string configuredArchivePath, bool keepBackup, DownloadHistoryReport? preparedReport, string configuredInventoryRoots)", StringComparison.Ordinal);
        int validateStart = historySource.IndexOf("public static DownloadHistoryReport ValidateAndReconcile", commitStart, StringComparison.Ordinal);
        Require(commitStart >= 0 && validateStart > commitStart, "Could not inspect Download History settings commit flow");
        string commit = historySource.Substring(commitStart, validateStart - commitStart);
        Require(commit.Contains("PersistSettingsFailClosed(enabled,"),
            "Target Download History settings are not persisted through the fail-closed helper");
        Require(commit.Contains("PersistSettingsFailClosed(oldEnabled,"),
            "Rollback does not restore prior Download History settings through the same fail-closed helper");
        Require(commit.IndexOf("IniProvider.Write(enabled, ConfigName, nameof(Enabled));", StringComparison.Ordinal) < 0,
            "CommitSettings still writes Enabled directly instead of using fail-closed persistence ordering");
    }


    private static void DownloadHistorySettingsProviderRollbackCannotStrandProtection() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string settingsSource = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmSettings.cs"));

        int methodStart = settingsSource.IndexOf("private void AddDownloadHistorySettingsButton()", StringComparison.Ordinal);
        int methodEnd = settingsSource.IndexOf("\n    private void frmSettings_Load", methodStart, StringComparison.Ordinal);
        Require(methodStart >= 0 && methodEnd > methodStart, "Could not inspect the Download History Settings button integration");
        string buttonSource = settingsSource.Substring(methodStart, methodEnd - methodStart);

        int transientProviderGuard = buttonSource.IndexOf("Downloads.YtdlType != YtdlType_Last", StringComparison.Ordinal);
        int childDialog = buttonSource.IndexOf("using frmDownloadHistory history = new()", StringComparison.Ordinal);
        Require(transientProviderGuard >= 0 && childDialog > transientProviderGuard,
            "Download History can still open against a transient provider selection that parent Settings Cancel may roll back");
        Require(buttonSource.IndexOf("save or cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                buttonSource.IndexOf("save or revert", StringComparison.OrdinalIgnoreCase) >= 0,
            "Transient provider rollback guard does not give an actionable save/revert instruction");
    }

    private static void DownloadHistoryWorkersUsePreparedContext() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string standard = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmDownloader.cs"));
        string extended = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Forms", "frmExtendedDownloader.cs"));
        Require(standard.Contains("HistoryExecution?.AcquireValidatedLease(() =>"), "Standard downloader does not launch through its cancellation-aware prepared archive context");
        Require(standard.Contains("HistoryExecution?.RefreshBackupAfterRun()"), "Standard downloader does not refresh archive backup after provider execution");
        Require(standard.IndexOf("DownloadHistory.Enabled ? DownloadHistory.AcquireArchiveLease()", StringComparison.Ordinal) < 0, "Standard downloader still binds locking to mutable live settings");
        Require(extended.Contains("HistoryExecution?.AcquireValidatedLease(() =>") && extended.Contains("BatchHistoryExecution?.AcquireValidatedLease(() =>"), "Extended downloader paths do not launch through cancellation-aware prepared archive contexts");
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

    private static void DownloadHistoryDisablesAmbientYtDlpPlugins() {
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string arguments, error;
            object execution;

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", null, out arguments, out error, out execution));
            Require(arguments.Contains("--no-plugin-dirs"), "Protected arguments do not disable ambient/default yt-dlp plugins");
            Require(arguments.IndexOf("--ignore-config", StringComparison.Ordinal) < arguments.IndexOf("--no-plugin-dirs", StringComparison.Ordinal),
                "Plugin isolation is not part of the app-owned protected argument prefix");
            Require(arguments.IndexOf("--no-plugin-dirs", StringComparison.Ordinal) < arguments.IndexOf("--download-archive", StringComparison.Ordinal),
                "Plugin isolation is not applied before the app-owned native archive option");

            foreach (string custom in new[] { "--plugin-dirs C:\\audit-plugins", "--plugin-dir C:\\audit-plugins" }) {
                Equal(false, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", custom, out arguments, out error, out execution));
                Require(error.IndexOf("plugin", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Custom plugin-directory option was not rejected clearly: " + custom);
            }

            Equal(true, DownloadHistoryArguments(fixture.History, "%(title)s-%(id)s.%(ext)s", "--no-plugin-dirs", out arguments, out error, out execution));
            Require(arguments.Contains("--no-plugin-dirs"), "A compatible user-supplied plugin-disable option removed app-owned plugin isolation");
        }
    }


    private static void RunDownloadHistoryTests() {
        Test("DOWNLOAD_HISTORY.NeverEnabledIsNoOp", DownloadHistoryNeverEnabledIsNoOp);
        Test("DOWNLOAD_HISTORY.TemplateAndArguments", DownloadHistoryTemplateAndArguments);
        Test("DOWNLOAD_HISTORY.RequiresAuthoritativeMetadataForRebuild", DownloadHistoryRequiresAuthoritativeMetadataForRebuild);
        Test("DOWNLOAD_HISTORY.HonorsEscapedIdTemplateSemantics", DownloadHistoryHonorsEscapedIdTemplateSemantics);
        Test("DOWNLOAD_HISTORY.RejectsParentTraversalFilenameSchemas", DownloadHistoryRejectsParentTraversalFilenameSchemas);
        Test("DOWNLOAD_HISTORY.RejectsUnsafeProtectedFilenameSchemas", DownloadHistoryRejectsUnsafeProtectedFilenameSchemas);
        Test("DOWNLOAD_HISTORY.DisablesPlaylistConcatenationWhileProtected", DownloadHistoryDisablesPlaylistConcatenationWhileProtected);
        Test("DOWNLOAD_HISTORY.RejectsCustomArgumentsThatBreakProtection", DownloadHistoryRejectsCustomArgumentsThatBreakProtection);
        Test("DOWNLOAD_HISTORY.RejectsUnbalancedCustomArgumentQuotes", DownloadHistoryRejectsUnbalancedCustomArgumentQuotes);
        Test("DOWNLOAD_HISTORY.RejectsEmbeddedNullCustomArguments", DownloadHistoryRejectsEmbeddedNullCustomArguments);
        Test("DOWNLOAD_HISTORY.RejectsPartialSectionDownloads", DownloadHistoryRejectsPartialSectionDownloads);
        Test("DOWNLOAD_HISTORY.RejectsClusteredShortOutputOverrides", DownloadHistoryRejectsClusteredShortOutputOverrides);
        Test("DOWNLOAD_HISTORY.RejectsSourceAndExtractorIdentityOverrides", DownloadHistoryRejectsSourceAndExtractorIdentityOverrides);
        Test("DOWNLOAD_HISTORY.RejectsMetadataIdentityRewrites", DownloadHistoryRejectsMetadataIdentityRewrites);
        Test("DOWNLOAD_HISTORY.RejectsUnsafeExtensionCompatibility", DownloadHistoryRejectsUnsafeExtensionCompatibility);
        Test("DOWNLOAD_HISTORY.RejectsArbitraryStateMutationHooks", DownloadHistoryRejectsArbitraryStateMutationHooks);
        Test("DOWNLOAD_HISTORY.RejectsCookieWritebackCollisions", DownloadHistoryRejectsCookieWritebackCollisions);
        Test("DOWNLOAD_HISTORY.ExpandsDollarCookiePathsLikeYtDlp", DownloadHistoryExpandsDollarCookiePathsLikeYtDlp);
        Test("DOWNLOAD_HISTORY.RejectsRawChildProcessArguments", DownloadHistoryRejectsRawChildProcessArguments);
        Test("DOWNLOAD_HISTORY.RejectsDestructiveCacheRemoval", DownloadHistoryRejectsDestructiveCacheRemoval);
        Test("DOWNLOAD_HISTORY.RejectsTestModePartialCompletion", DownloadHistoryRejectsTestModePartialCompletion);
        Test("DOWNLOAD_HISTORY.RejectsFalseCompletionErrorControls", DownloadHistoryRejectsFalseCompletionErrorControls);
        Test("DOWNLOAD_HISTORY.ForcesCompleteFragmentDownloads", DownloadHistoryForcesCompleteFragmentDownloads);
        Test("DOWNLOAD_HISTORY.RejectsExecutablePathAndSelfUpdateOverrides", DownloadHistoryRejectsExecutablePathAndSelfUpdateOverrides);
        Test("DOWNLOAD_HISTORY.MatchesYtDlpPathExpansion", DownloadHistoryMatchesYtDlpPathExpansion);
        Test("DOWNLOAD_HISTORY.IsolationPrecedesDanglingCustomOptions", DownloadHistoryIsolationPrecedesDanglingCustomOptions);
        Test("DOWNLOAD_HISTORY.RejectsNonNativeArchiveEncodings", DownloadHistoryRejectsNonNativeArchiveEncodings);
        Test("DOWNLOAD_HISTORY.RejectsArbitraryPostprocessorHooks", DownloadHistoryRejectsArbitraryPostprocessorHooks);
        Test("DOWNLOAD_HISTORY.RejectsIdOutputOverride", DownloadHistoryRejectsIdOutputOverride);
        Test("DOWNLOAD_HISTORY.RejectsInjectedMetadataArchiveRecords", DownloadHistoryRejectsInjectedMetadataArchiveRecords);
        Test("DOWNLOAD_HISTORY.IgnoresAmbientYtDlpConfig", DownloadHistoryIgnoresAmbientYtDlpConfig);
        Test("DOWNLOAD_HISTORY.DisablesAmbientYtDlpPlugins", DownloadHistoryDisablesAmbientYtDlpPlugins);
        Test("DOWNLOAD_HISTORY.RecoversSupportedMediaExtensions", DownloadHistoryRecoversSupportedMediaExtensions);
        Test("DOWNLOAD_HISTORY.InventoriesCurrentDirectMediaExtensions", DownloadHistoryInventoriesCurrentDirectMediaExtensions);
        Test("DOWNLOAD_HISTORY.RebuildsDeletedArchiveFromIds", DownloadHistoryRebuildsDeletedArchiveFromIds);
        Test("DOWNLOAD_HISTORY.RecoversHistoricalProtectedSchemas", DownloadHistoryRecoversHistoricalProtectedSchemas);
        Test("DOWNLOAD_HISTORY.ParsesFormattedFilenameSchemas", DownloadHistoryParsesFormattedFilenameSchemas);
        Test("DOWNLOAD_HISTORY.PreservesDelimiterBearingSchemas", DownloadHistoryPreservesDelimiterBearingSchemas);
        Test("DOWNLOAD_HISTORY.UnderstandsIdPlacementFromSchema", DownloadHistoryUnderstandsIdPlacementFromSchema);
        Test("DOWNLOAD_HISTORY.RejectsReparsePointTraversal", DownloadHistoryRejectsReparsePointTraversal);
        Test("DOWNLOAD_HISTORY.RecoversRestrictedYtDlpIdSanitization", DownloadHistoryRecoversRestrictedYtDlpIdSanitization);
        Test("DOWNLOAD_HISTORY.RecoversSanitizedProviderIds", DownloadHistoryRecoversSanitizedProviderIds);
        Test("DOWNLOAD_HISTORY.RecognizesSplitChapterIds", DownloadHistoryRecognizesSplitChapterIds);
        Test("DOWNLOAD_HISTORY.RebuildsDerivedMediaAfterTotalArchiveLoss", DownloadHistoryRebuildsDerivedMediaAfterTotalArchiveLoss);
        Test("DOWNLOAD_HISTORY.ValidatesRetainedFormatComponents", DownloadHistoryValidatesRetainedFormatComponents);
        Test("DOWNLOAD_HISTORY.DisambiguatesCleanMergedFormatIds", DownloadHistoryDisambiguatesCleanMergedFormatIds);
        Test("DOWNLOAD_HISTORY.RejectsLocalExtractorPageSubstitution", DownloadHistoryRejectsLocalExtractorPageSubstitution);
        Test("DOWNLOAD_HISTORY.DoesNotInferYoutubeFromIdShape", DownloadHistoryDoesNotInferYoutubeFromIdShape);
        Test("DOWNLOAD_HISTORY.RejectsDisplayExtractorAsNativeIdentity", DownloadHistoryRejectsDisplayExtractorAsNativeIdentity);
        Test("DOWNLOAD_HISTORY.UsesTopLevelInfoJsonIdentity", DownloadHistoryUsesTopLevelInfoJsonIdentity);
        Test("DOWNLOAD_HISTORY.MigratesLegacyMetadata", DownloadHistoryMigratesLegacyMetadata);
        Test("DOWNLOAD_HISTORY.MigrationFailureRollsBackMedia", DownloadHistoryMigrationFailureRollsBackMedia);
        Test("DOWNLOAD_HISTORY.InventoryNeverMutatesMediaFamilies", DownloadHistoryMigrationFailureRollsBackMedia);
        Test("DOWNLOAD_HISTORY.BlocksPartialAndUnsafeLibraries", DownloadHistoryBlocksPartialAndUnsafeLibraries);
        Test("DOWNLOAD_HISTORY.UnexpectedLossRequiresExplicitReset", DownloadHistoryUnexpectedLossRequiresExplicitReset);
        Test("DOWNLOAD_HISTORY.BackupRefreshRejectsCorruption", DownloadHistoryBackupRefreshRejectsCorruption);
        Test("DOWNLOAD_HISTORY.ValidArchiveTruncationPreservesBackup", DownloadHistoryValidArchiveTruncationPreservesBackup);
        Test("DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines", DownloadHistoryCorruptArchiveDoesNotTrustPartialLines);
        Test("DOWNLOAD_HISTORY.RefusesUninitializedDefaultArchiveCollision", DownloadHistoryRefusesUninitializedDefaultArchiveCollision);
        Test("DOWNLOAD_HISTORY.DisableReenableReconcilesChanges", DownloadHistoryDisableReenableReconcilesChanges);
        Test("DOWNLOAD_HISTORY.ValidArchiveDoesNotPromoteUnarchivedFile", DownloadHistoryValidArchiveDoesNotPromoteUnarchivedFile);
        Test("DOWNLOAD_HISTORY.ExplicitRebuildRecoversAuthoritativeMissingEntry", DownloadHistoryExplicitRebuildRecoversAuthoritativeMissingEntry);
        Test("DOWNLOAD_HISTORY.InventoriesGifMediaConservatively", DownloadHistoryInventoriesGifMediaConservatively);
        Test("DOWNLOAD_HISTORY.IgnoresIndexedThumbnailSidecars", DownloadHistoryIgnoresIndexedThumbnailSidecars);
        Test("DOWNLOAD_HISTORY.IgnoresFailedAndSidecarFiles", DownloadHistoryIgnoresFailedAndSidecarFiles);
        Test("DOWNLOAD_HISTORY.DisabledIntervalWithoutIdsFailsSafe", DownloadHistoryDisabledIntervalWithoutIdsFailsSafe);
        Test("DOWNLOAD_HISTORY.MissingParentHardStopsWithoutPersistence", DownloadHistoryMissingParentHardStopsWithoutPersistence);
        Test("DOWNLOAD_HISTORY.ManagementFailsFastWhenBusy", DownloadHistoryManagementFailsFastWhenBusy);
        Test("DOWNLOAD_HISTORY.ExecutionContextSurvivesDisableButNotReset", DownloadHistoryExecutionContextSurvivesDisableButNotReset);
        Test("DOWNLOAD_HISTORY.ExecutionContextRejectsSettingChanges", DownloadHistoryExecutionContextRejectsSettingChanges);
        Test("DOWNLOAD_HISTORY.ExecutionLeaseRejectsArchiveTruncation", DownloadHistoryExecutionLeaseRejectsArchiveTruncation);
        Test("DOWNLOAD_HISTORY.ExecutionLeaseRejectsBackupFreePreparedTruncation", DownloadHistoryExecutionLeaseRejectsBackupFreePreparedTruncation);
        Test("DOWNLOAD_HISTORY.PreservesSameSessionLedgerFloorWithoutBackup", DownloadHistoryPreservesSameSessionLedgerFloorWithoutBackup);
        Test("DOWNLOAD_HISTORY.RetentionOffDoesNotRestoreFromStaleBackupAlone", DownloadHistoryRetentionOffDoesNotRestoreFromStaleBackupAlone);
        Test("DOWNLOAD_HISTORY.ArchiveRelocationRejectsStaleBackupOnlySource", DownloadHistoryArchiveRelocationRejectsStaleBackupOnlySource);
        Test("DOWNLOAD_HISTORY.ExecutionLeaseUsesExistingBackupWhenRetentionOff", DownloadHistoryExecutionLeaseUsesExistingBackupWhenRetentionOff);
        Test("DOWNLOAD_HISTORY.LeaseSerializesWorkers", DownloadHistoryLeaseSerializesWorkers);
        Test("DOWNLOAD_HISTORY.FirstUseRaceAcquiresFileLockAfterDirectoryAppears", DownloadHistoryFirstUseRaceAcquiresFileLockAfterDirectoryAppears);
        Test("DOWNLOAD_HISTORY.LeaseWaitHonorsCancellation", DownloadHistoryLeaseWaitHonorsCancellation);
        Test("DOWNLOAD_HISTORY.CancellationIsRecheckedBeforeProcessStart", DownloadHistoryCancellationIsRecheckedBeforeProcessStart);
        Test("DOWNLOAD_HISTORY.SettingsRejectIdRemovalWhileEnabled", DownloadHistorySettingsRejectIdRemovalWhileEnabled);
        Test("DOWNLOAD_HISTORY.LibraryBindingPreventsCrossLibraryReuse", DownloadHistoryLibraryBindingPreventsCrossLibraryReuse);
        Test("DOWNLOAD_HISTORY.ActiveRootChangeTriggersInventoryRecovery", DownloadHistoryActiveRootChangeTriggersInventoryRecovery);
        Test("DOWNLOAD_HISTORY.RefusesUnrecognizedArchiveOverwrite", DownloadHistoryRefusesUnrecognizedArchiveOverwrite);
        Test("DOWNLOAD_HISTORY.CompanionLockAndTempFilesAreNonDestructive", DownloadHistoryCompanionLockAndTempFilesAreNonDestructive);
        Test("DOWNLOAD_HISTORY.ResetPreservesUnownedTempCompanions", DownloadHistoryResetPreservesUnownedTempCompanions);
        Test("DOWNLOAD_HISTORY.RefusesInvalidBackupCollisionBeforePrimaryMutation", DownloadHistoryRefusesInvalidBackupCollisionBeforePrimaryMutation);
        Test("DOWNLOAD_HISTORY.BoundCustomArchiveCorruptionRequiresExplicitReset", DownloadHistoryBoundCustomArchiveCorruptionRequiresExplicitReset);
        Test("DOWNLOAD_HISTORY.ArchiveFileIsNotInventoryMedia", DownloadHistoryArchiveFileIsNotInventoryMedia);
        Test("DOWNLOAD_HISTORY.ArchiveRelocationPreservesLedgerOnlyIdentities", DownloadHistoryArchiveRelocationPreservesLedgerOnlyIdentities);
        Test("DOWNLOAD_HISTORY.ArchiveRelocationRequiresReadablePreviousLedger", DownloadHistoryArchiveRelocationRequiresReadablePreviousLedger);
        Test("DOWNLOAD_HISTORY.DialogCanSelectNewDefaultArchiveAfterRootChange", DownloadHistoryDialogCanSelectNewDefaultArchiveAfterRootChange);
        Test("DOWNLOAD_HISTORY.ResetUsesEffectiveArchiveForImplicitBoundPath", DownloadHistoryResetUsesEffectiveArchiveForImplicitBoundPath);
        Test("DOWNLOAD_HISTORY.PathAgnosticHistorySurvivesMediaMoves", DownloadHistoryPathAgnosticHistorySurvivesMediaMoves);
        Test("DOWNLOAD_HISTORY.MultipleInventoryRootsShareOneArchive", DownloadHistoryMultipleInventoryRootsShareOneArchive);
        Test("DOWNLOAD_HISTORY.NormalProtectionIgnoresOfflineInventoryRoots", DownloadHistoryNormalProtectionIgnoresOfflineInventoryRoots);
        Test("DOWNLOAD_HISTORY.PreparedCommitDoesNotRescanInventory", DownloadHistoryPreparedCommitDoesNotRescanInventory);
        Test("DOWNLOAD_HISTORY.LargeLibraryManagementAvoidsRepeatedScans", DownloadHistoryLargeLibraryManagementAvoidsRepeatedScans);
        Test("DOWNLOAD_HISTORY.SettingsPersistenceFailsClosed", DownloadHistorySettingsPersistenceFailsClosed);
        Test("DOWNLOAD_HISTORY.SettingsProviderRollbackCannotStrandProtection", DownloadHistorySettingsProviderRollbackCannotStrandProtection);
        Test("DOWNLOAD_HISTORY.WorkersUsePreparedContext", DownloadHistoryWorkersUsePreparedContext);
        Test("DOWNLOAD_HISTORY.WiresStandardAndExtendedArguments", DownloadHistoryWiresStandardAndExtendedArguments);
    }
}

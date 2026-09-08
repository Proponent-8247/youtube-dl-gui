using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using youtube_dl_gui.History;

internal static partial class Program {
    private const string Id = "9qFjkwAElDs";
    private const string SecondId = "ABCDEFGHIJK";
    private static int passed;
    private static int failed;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws(Action action, string message) {
        bool threw = false;
        try { action(); } catch (IOException) { threw = true; }
        Assert(threw, message);
    }
    private static void Test(string name, Action action) {
        try { action(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex); failed++; }
    }
    private sealed class Library : IDisposable {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "ytdlg-history-tests-" + Guid.NewGuid().ToString("N"));
        public string Archive { get { return Path.Combine(Root, "yt-dlp-archive.txt"); } }
        public HistoryOptions Options { get; private set; }
        public Library() {
            Directory.CreateDirectory(Root);
            Options = new HistoryOptions { LibraryPath = Root, Template = "%(title)s-%(id)s.%(ext)s" };
        }
        public string Media(string name) { string p = Path.Combine(Root, name); File.WriteAllBytes(p, new byte[] { 1, 2, 3 }); return p; }
        public void Info(string stem, string id, string key = "Youtube") {
            File.WriteAllText(Path.Combine(Root, stem + ".info.json"), "{\"extractor_key\":\"" + key + "\",\"id\":\"" + id + "\"}", new UTF8Encoding(false));
        }
        public HistoryStore Open() { return HistoryStore.Open(Options); }
        public void Dispose() { Directory.Delete(Root, true); }
    }
    public static int Main(string[] args) {
        if (args.Length == 1 && args[0] == "--child-wait") { System.Threading.Thread.Sleep(60000); return 0; }
        Test("identity preserves source-ID case and normalizes only the extractor", () => {
            Assert(HistoryIdentity.Parse("YouTube " + Id).Entry == "youtube " + Id, "Identity case changed.");
            Throws(() => HistoryIdentity.Parse("youtube"), "Missing ID accepted.");
            Throws(() => HistoryIdentity.Parse("youtube A\nB"), "Multiline identity accepted.");
        });
        Test("mandatory unescaped filename ID", () => {
            HistoryTemplate.Validate("%(uploader)s\\%(title)s-%(id)s.%(ext)s");
            HistoryTemplate.Validate("%(id)s-%(title)s.%(ext)s");
            Throws(() => HistoryTemplate.Validate("%(title)s.%(ext)s"), "Missing ID accepted.");
            Throws(() => HistoryTemplate.Validate("%%(id)s.%(ext)s"), "Escaped ID accepted.");
            Throws(() => HistoryTemplate.Validate("%(id)s\\%(title)s.%(ext)s"), "Folder-only ID accepted.");
            Throws(() => HistoryTemplate.Validate("..\\%(id)s.%(ext)s"), "Traversal accepted.");
            Assert(HistoryTemplate.Suggest("%(title)s.%(ext)s") == "%(title)s-%(id)s.%(ext)s", "Bad suggested template.");
        });
        Test("empty library initializes without fabricated identities", () => {
            using (var l = new Library()) using (var s = l.Open()) {
                var r = s.Reconcile(); Assert(r.Entries.Count == 0 && File.Exists(l.Archive), "Initialization failed.");
                Assert(File.Exists(l.Archive + ".bak"), "No backup.");
            }
        });
        Test("missing storage is a hard stop", () => {
            using (var l = new Library()) {
                l.Options.ArchivePath = Path.Combine(l.Root, "missing-storage", "archive.txt");
                Throws(() => { using (l.Open()) { } }, "Missing storage accepted.");
                Assert(!Directory.Exists(Path.Combine(l.Root, "missing-storage")), "Missing storage recreated.");
            }
        });
        Test("container changes share one identity", () => {
            using (var l = new Library()) {
                l.Media("Title-" + Id + ".webm"); l.Info("Title-" + Id, Id);
                using (var s = l.Open()) s.Reconcile();
                l.Media("Renamed [Youtube " + Id + "].mp4");
                using (var s = l.Open()) { var r = s.Reconcile(); Assert(r.Entries.Count == 1 && r.EmbeddedIds == 2, "Container changed identity."); }
            }
        });
        Test("a bare filename ID does not invent an extractor", () => {
            using (var l = new Library()) {
                l.Media("Title-" + Id + ".webm");
                using (var s = l.Open()) { Assert(s.Inspect().Unresolved.Count == 1, "Guessed a namespace."); Throws(() => s.Reconcile(), "Unsafe library accepted."); }
                Assert(!File.Exists(l.Archive), "Empty ledger silently created.");
                l.Options.LegacyExtractor = "youtube";
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Contains("youtube " + Id), "Explicit declaration not honored.");
            }
        });
        Test("existing archive authoritatively resolves a filename namespace", () => {
            using (var l = new Library()) {
                l.Media("Title-" + Id + ".mp4"); File.WriteAllText(l.Archive, "youtube " + Id + "\n");
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Count == 1, "Archive identity was not used.");
            }
        });
        Test("ambiguous namespaces never merge", () => {
            using (var l = new Library()) {
                l.Media("Title-12345.mp4"); File.WriteAllText(l.Archive, "vimeo 12345\nother 12345\n");
                using (var s = l.Open()) Assert(s.Inspect().Unresolved.Count == 1, "Ambiguous ID assigned a website.");
            }
        });
        Test("lost archive and backup rebuild from the last validated checkpoint", () => {
            using (var l = new Library()) {
                l.Media("Title-" + Id + ".webm"); l.Info("Title-" + Id, Id);
                using (var s = l.Open()) s.Reconcile();
                File.Delete(l.Archive); File.Delete(l.Archive + ".bak"); File.Delete(Path.Combine(l.Root, "Title-" + Id + ".info.json"));
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Contains("youtube " + Id), "Checkpoint recovery lost an identity.");
            }
        });
        Test("valid backup recovers malformed primary without destroying evidence", () => {
            using (var l = new Library()) {
                File.WriteAllText(l.Archive, "broken"); File.WriteAllText(l.Archive + ".bak", "youtube " + Id + "\n");
                using (var s = l.Open()) { var r = s.Reconcile(); Assert(r.BackupUsed && r.Entries.Count == 1, "Backup recovery failed."); }
                Assert(Directory.GetFiles(l.Root, "*.corrupt-*").Length == 1, "Corrupt evidence was discarded.");
            }
        });
        Test("stop-on-missing policy distinguishes existing media from first use", () => {
            using (var l = new Library()) {
                l.Options.StopWhenMissing = true; l.Media("Title-" + Id + ".mp4"); l.Info("Title-" + Id, Id);
                using (var s = l.Open()) { Throws(() => s.Reconcile(), "Stop policy ignored."); Assert(s.Reconcile(true).Entries.Count == 1, "Explicit rebuild failed."); }
            }
        });
        Test("metadata-only and temporary/fragment artifacts never populate history", () => {
            using (var l = new Library()) {
                l.Info("Title-" + Id, Id); l.Media("Title-" + Id + ".mp4.part"); l.Media("Title-" + Id + ".f137.mp4"); l.Media("Title-" + Id + ".jpg");
                using (var s = l.Open()) { var r = s.Reconcile(); Assert(r.Entries.Count == 0 && r.Incomplete.Count == 2, "Sidecar/fragment was trusted."); }
            }
        });
        Test("metadata-backed legacy migration renames media and sidecars", () => {
            using (var l = new Library()) {
                l.Media("Legacy title.mp4"); l.Info("Legacy title", Id); l.Media("Legacy title.en.vtt");
                using (var s = l.Open()) {
                    Assert(s.Inspect().Migrations.Count == 1, "Migration not identified.");
                    Throws(() => s.Reconcile(), "Unmigrated file trusted.");
                    Assert(s.Migrate().Entries.Contains("youtube " + Id), "Migration archive missing.");
                }
                Assert(File.Exists(Path.Combine(l.Root, "Legacy title-" + Id + ".mp4")), "Media not renamed.");
                Assert(File.Exists(Path.Combine(l.Root, "Legacy title-" + Id + ".info.json")), "Metadata not renamed.");
                Assert(File.Exists(Path.Combine(l.Root, "Legacy title-" + Id + ".en.vtt")), "Subtitle not renamed.");
            }
        });
        Test("migration checks all collisions before making changes", () => {
            using (var l = new Library()) {
                l.Options.LegacyExtractor = "youtube"; l.Media("Legacy.mp4"); l.Info("Legacy", Id); l.Media("Legacy-" + Id + ".mp4");
                using (var s = l.Open()) Throws(() => s.Migrate(), "Collision overwritten.");
                Assert(File.Exists(Path.Combine(l.Root, "Legacy.mp4")) && File.Exists(Path.Combine(l.Root, "Legacy.info.json")), "Collision caused a partial migration.");
            }
        });
        Test("multiple legacy containers share one migrated metadata sidecar", () => {
            using (var l = new Library()) {
                l.Media("Legacy.mp4"); l.Media("Legacy.webm"); l.Info("Legacy", Id);
                using (var s = l.Open()) Assert(s.Migrate().Entries.Count == 1, "Shared metadata migration failed.");
            }
        });
        Test("partial migration is not reported healthy", () => {
            using (var l = new Library()) {
                l.Media("Known [Youtube " + Id + "].mp4"); l.Media("Unknown.mp4");
                using (var s = l.Open()) { var r = s.Inspect(); Assert(r.State == "Partial", "Partial library called healthy."); Throws(() => s.Reconcile(), "Partial library enabled."); }
                Assert(!File.Exists(l.Archive), "Unsafe archive created.");
            }
        });
        Test("large unidentified library fails closed", () => {
            using (var l = new Library()) {
                for (int i = 0; i < 10000; i++) l.Media("Unidentified-" + i + ".mp4");
                using (var s = l.Open()) { var r = s.Inspect(); Assert(r.State == "Unsafe" && r.Unresolved.Count == 10000, "Files silently skipped."); Throws(() => s.Reconcile(), "Unsafe large library enabled."); }
                Assert(!File.Exists(l.Archive), "Empty ledger created over legacy library.");
            }
        });
        Test("failed postprocessing leaves final-looking media retryable", () => {
            using (var l = new Library()) {
                using (var s = l.Open()) {
                    s.BeginRun(); l.Media("Failed-" + Id + ".mp4"); l.Info("Failed-" + Id, Id);
                    var r = s.FinishRun(); Assert(r.Entries.Count == 0 && r.Incomplete.Count == 1, "Failed media archived.");
                }
                using (var s = l.Open()) {
                    Assert(s.Reconcile().Entries.Count == 0, "Failed output imported on the next run.");
                    s.BeginRun(); File.AppendAllText(l.Archive, "youtube " + Id + "\n");
                    Assert(s.FinishRun().Entries.Contains("youtube " + Id), "Successful retry not trusted.");
                }
            }
        });
        Test("successful entries survive a partially failed batch", () => {
            using (var l = new Library()) using (var s = l.Open()) {
                s.BeginRun(); l.Media("Success-" + Id + ".mp4"); l.Info("Success-" + Id, Id);
                l.Media("Failure-" + SecondId + ".mp4"); l.Info("Failure-" + SecondId, SecondId);
                File.AppendAllText(l.Archive, "youtube " + Id + "\n");
                var r = s.FinishRun(); Assert(r.Entries.SetEquals(new[] { "youtube " + Id }) && r.Incomplete.Count == 1, "Batch success/failure identities mixed.");
            }
        });
        Test("re-enabling reconciles downloads made during the disabled interval", () => {
            using (var l = new Library()) {
                l.Media("Old [Youtube " + Id + "].mp4");
                using (var s = l.Open()) s.Reconcile();
                string dormant = File.ReadAllText(l.Archive);
                l.Media("New [Youtube " + SecondId + "].webm");
                Assert(File.ReadAllText(l.Archive) == dormant, "Disabled interval modified archive.");
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Count == 2, "Re-enabled archive stale.");
            }
        });
        Test("re-enable with unidentified files is blocked without erasing history", () => {
            using (var l = new Library()) {
                l.Media("Old [Youtube " + Id + "].mp4"); using (var s = l.Open()) s.Reconcile();
                byte[] before = File.ReadAllBytes(l.Archive); l.Media("Unidentified.mp4");
                using (var s = l.Open()) Throws(() => s.Reconcile(), "Unidentified interval accepted.");
                Assert(before.SequenceEqual(File.ReadAllBytes(l.Archive)), "Existing history changed on failure.");
            }
        });
        Test("exclusive leases prevent concurrent downloads and reconstruction", () => {
            using (var l = new Library()) using (var first = l.Open()) {
                Throws(() => { using (l.Open()) { } }, "Concurrent lease acquired.");
                l.Options.ArchivePath = Path.Combine(l.Root, "other.txt");
                Throws(() => { using (l.Open()) { } }, "Another archive bypassed the library lock.");
            }
        });
        Test("a live child from an interrupted GUI blocks reconstruction", () => {
            using (var l = new Library()) {
                using (var process = Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location, "--child-wait") { UseShellExecute = false, CreateNoWindow = true })) {
                    try {
                        using (var s = l.Open()) { s.BeginRun(); s.TrackProcess(process); }
                        using (var s = l.Open()) Throws(() => s.Reconcile(), "Live orphan was ignored.");
                    }
                    finally { process.Kill(); process.WaitForExit(); }
                }
                using (var s = l.Open()) s.Reconcile();
            }
        });
        Test("archive filename prefixes cannot hide media", () => {
            using (var l = new Library()) {
                l.Media("yt-dlp-archive.txt.mp4");
                using (var s = l.Open()) Assert(s.Inspect().Unresolved.Count == 1, "Media hidden by archive prefix.");
            }
        });
        Test("invalid metadata cannot manufacture an identity", () => {
            using (var l = new Library()) {
                l.Media("Video-" + Id + ".mp4"); File.WriteAllText(Path.Combine(l.Root, "Video-" + Id + ".info.json"), "{\"id\":\"" + Id + "\"}");
                using (var s = l.Open()) Assert(s.Inspect().Unresolved.Count == 1, "Unidentified metadata trusted.");
            }
        });
        Test("metadata-backed ID-first templates remain supported", () => {
            using (var l = new Library()) {
                l.Options.Template = "%(id)s-%(title)s.%(ext)s";
                l.Media(Id + "-Title.mp4"); l.Info(Id + "-Title", Id);
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Count == 1, "ID-first template rejected.");
            }
        });
        Test("changing archive paths cannot bypass an interrupted library run", () => {
            using (var l = new Library()) {
                using (var s = l.Open()) s.BeginRun();
                l.Options.ArchivePath = Path.Combine(l.Root, "another-archive.txt");
                using (var s = l.Open()) Throws(() => s.Reconcile(), "Library-wide interrupted marker was bypassed.");
                l.Options.ArchivePath = null;
                using (var s = l.Open()) s.RecoverInterruptedLaunch();
                Assert(!File.Exists(Path.Combine(l.Root, ".ytdlg-history.pending.json")), "Library pending marker leaked.");
            }
        });
        TestRuntime();
        Console.WriteLine("RESULT " + passed + " passed; " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}

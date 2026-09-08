using System;
using System.Diagnostics;
using System.IO;
using youtube_dl_gui.History;

internal static partial class Program {
    private static void TestRuntime() {
        Test("ordinary title components are not mistaken for intermediate media", () => {
            using (var l = new Library()) {
                foreach (string title in new[] { "A.temp.title", "A.f137.title", "A.part.title" }) {
                    string stem = title + "-" + Id;
                    l.Media(stem + ".mp4"); l.Info(stem, Id);
                }
                l.Media("Unfinished-" + SecondId + ".f137.mp4");
                using (var s = l.Open()) {
                    var report = s.Reconcile();
                    Assert(report.EmbeddedIds == 3 && report.Incomplete.Count == 1, "Completed media silently skipped.");
                    Assert(report.Entries.SetEquals(new[] { "youtube " + Id }), "Wrong completed identity set.");
                }
            }
        });
        Test("archive options precede the audit branch URL separator", () => {
            using (var l = new Library()) {
                string command = "-o " + HistoryCommandPolicy.Quote(Path.Combine(l.Root, l.Options.Template))
                    + " -- https://example.com/media";
                var tokens = HistoryCommandPolicy.Tokenize(HistoryCommandPolicy.Build(command, l.Root, l.Archive));
                Assert(tokens.IndexOf("--download-archive") < tokens.IndexOf("--"), "Archive option became URL text.");
                Assert(tokens[tokens.Count - 1] == "https://example.com/media", "Media URL changed.");
                Throws(() => HistoryCommandPolicy.Build(command + " --download-archive other.txt", l.Root, l.Archive), "Option-looking URL accepted.");
                Throws(() => HistoryCommandPolicy.Build(command + " --", l.Root, l.Archive), "Second delimiter accepted.");
            }
        });
        Test("cancelled preflight does not create an archive", () => {
            using (var l = new Library()) {
                l.Options.Cancelled = () => true;
                bool cancelled = false;
                try { using (var s = l.Open()) s.Reconcile(); } catch (OperationCanceledException) { cancelled = true; }
                Assert(cancelled && !File.Exists(l.Archive), "Cancelled preflight modified history.");
            }
        });
        Test("changing custom archives cannot promote failed media to completed", () => {
            using (var l = new Library()) using (var other = new Library()) {
                using (var s = l.Open()) {
                    s.BeginRun(); l.Media("Failed-" + Id + ".mp4"); l.Info("Failed-" + Id, Id); s.FinishRun();
                }
                other.Options.ArchivePath = l.Archive;
                using (var s = other.Open()) s.Reconcile();
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Count == 0, "Shared archive lost failed-media evidence.");
                l.Options.ArchivePath = Path.Combine(l.Root, "new-history.txt");
                using (var s = l.Open()) Assert(s.Reconcile().Entries.Count == 0, "New archive trusted failed output.");
            }
        });
        Test("archive evidence prefixes cannot hide completed media", () => {
            using (var l = new Library()) {
                l.Media("yt-dlp-archive.txt.reset-arbitrary.mp4");
                using (var s = l.Open()) Assert(s.Inspect().Unresolved.Count == 1, "Prefix hid unknown media.");
            }
        });
        Test("Windows argument quoting round-trips protected paths and credentials", () => {
            foreach (string value in new[] { "", @"C:\Media library\", "a\"b", "a\\\\\"b", "--password", "a b" }) {
                var tokens = HistoryCommandPolicy.Tokenize(HistoryCommandPolicy.Quote(value));
                Assert(tokens.Count == 1 && tokens[0] == value, "Quoted argument changed.");
            }
            Throws(() => HistoryCommandPolicy.Tokenize("--password \"unfinished"), "Unbalanced quotes accepted.");
        });
        Test("native archive options preserve formats and complete playlist traversal", () => {
            using (var l = new Library()) {
                string command = "https://www.youtube.com/ninjasexparty/videos -o " + HistoryCommandPolicy.Quote(Path.Combine(l.Root, l.Options.Template)) + " -f bestvideo+bestaudio/best --no-abort-on-error";
                string result = HistoryCommandPolicy.Build(command, l.Root, l.Archive);
                Assert(result.StartsWith(command, StringComparison.Ordinal), "Existing format/input arguments changed.");
                Assert(result.Contains("--no-break-on-existing") && result.Contains("--download-archive"), "Safe archive flags missing.");
                Assert(!result.Contains("--recode-video"), "History forced transcoding.");
                foreach (string unsafeOption in new[] { "--download-archive=other.txt", "--no-download-archive", "--download-arc x", "--break-on-existing", "--break-per-input", "--force-write-archive", "--ignore-errors", "--simulate", "--skip-download", "--exec echo", "--alias x=y", "--config-locations x", "--download-sections *0-10", "-i", "--", "-f all", "-f -" }) {
                    Throws(() => HistoryCommandPolicy.Build(command + " " + unsafeOption, l.Root, l.Archive), "Unsafe option accepted: " + unsafeOption);
                }
            }
        });
        Test("custom arguments cannot move media outside the protected library", () => {
            using (var l = new Library()) {
                string command = "https://example.com/video -o " + HistoryCommandPolicy.Quote(Path.Combine(l.Root, l.Options.Template));
                Throws(() => HistoryCommandPolicy.Build(command + " -o other.mp4", l.Root, l.Archive), "Duplicate output accepted.");
                Throws(() => HistoryCommandPolicy.Build("https://example.com/video -o " + HistoryCommandPolicy.Quote(Path.Combine(l.Root, "..", "outside-%(id)s.%(ext)s")), l.Root, l.Archive), "Escaping output accepted.");
                Throws(() => HistoryCommandPolicy.Build(command + " --cookies " + HistoryCommandPolicy.Quote(l.Archive), l.Root, l.Archive), "Cookies could overwrite archive.");
                HistoryCommandPolicy.Build(command + " --cookies-from-browser firefox --sleep-interval 2 --js-runtimes deno", l.Root, l.Archive);
            }
        });
        Test("process guard reaps its child and releases the archive only after postflight", () => {
            using (var l = new Library()) using (var process = new Process()) {
                process.StartInfo = new ProcessStartInfo(typeof(Program).Assembly.Location, "--child-wait") { UseShellExecute = false, CreateNoWindow = true };
                var guard = HistoryProcessGuard.Start(process, l.Options, () => false, _ => { });
                Assert(HistoryProcessGuard.ActiveCount == 1, "Guard was not registered.");
                Throws(() => { using (l.Open()) { } }, "Active child lost its library lease.");
                process.Kill(); process.WaitForExit();
                Assert(guard.Completion.Wait(15000), "Guard did not finish.");
                Assert(HistoryProcessGuard.ActiveCount == 0, "Guard count leaked.");
                using (l.Open()) { }
            }
        });
        Test("a failed process start does not strand an archive lease", () => {
            using (var l = new Library()) using (var process = new Process()) {
                process.StartInfo = new ProcessStartInfo(Path.Combine(l.Root, "does-not-exist.exe")) { UseShellExecute = false };
                bool rejected = false;
                try { HistoryProcessGuard.Start(process, l.Options, () => false, _ => { }); }
                catch (System.ComponentModel.Win32Exception) { rejected = true; }
                Assert(rejected && HistoryProcessGuard.ActiveCount == 0, "Failed start leaked protection state.");
                using (var store = l.Open()) Assert(store.Reconcile().Entries.Count == 0, "Failed start fabricated history.");
            }
        });
    }
}

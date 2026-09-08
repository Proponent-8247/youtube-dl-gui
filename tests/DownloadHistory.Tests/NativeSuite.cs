using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using youtube_dl_gui.History;

internal static class NativeSuite {
    private static Assembly app;
    private static Type settings;
    private static Type runtime;
    private static string workspace;
    private static string library;
    private static string requests;
    private static string config;
    private static string schema = "%(title)s-%(id)s.%(ext)s";
    private static int successes;
    private static int failures;
    private const string A = "AAAAAAAAAAA", B = "BBBBBBBBBBB", C = "CCCCCCCCCCC", D = "DDDDDDDDDDD";
    private const string BaseUrl = "https://history.invalid/";
    private static string Archive { get { return Path.Combine(library, "yt-dlp-archive.txt"); } }
    private static object Call(Type type, string name, params object[] args) {
        try { return type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action action) {
        try { action(); successes++; Console.WriteLine("PASS NATIVE " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL NATIVE " + name + ": " + ex); }
    }
    private static object Preferences() { return settings.GetProperty("Current").GetValue(null); }
    private static void SetField(object value, string name, object field) { value.GetType().GetField(name).SetValue(value, field); }
    private static void Save(bool enabled) {
        object preferences = Preferences();
        SetField(preferences, "Enabled", enabled); SetField(preferences, "CustomArchive", false);
        SetField(preferences, "ArchivePath", "");
        Call(settings, "Save", preferences, schema);
    }
    private static void Reconcile(bool migrate = false) {
        object options = Call(settings, "Options", Preferences(), schema);
        Type storeType = app.GetType("youtube_dl_gui.History.HistoryStore", true);
        using (var store = (IDisposable)Call(storeType, "Open", options)) {
            try { storeType.GetMethod(migrate ? "Migrate" : "Reconcile").Invoke(store, migrate ? null : new object[] { true }); }
            catch (TargetInvocationException ex) { throw ex.InnerException; }
        }
    }
    private static void Initialize(string name) {
        Save(false);
        library = Path.Combine(workspace, name); Directory.CreateDirectory(library);
        app.GetType("youtube_dl_gui.Downloads", true).GetProperty("downloadPath").SetValue(null, library);
        requests = Path.Combine(workspace, name + "-requests.txt");
        Configure();
    }
    private static void Configure(string ext = "webm", string channel = "AAAAAAAAAAA BBBBBBBBBBB", string fail = "") {
        string json = "{\"ext\":\"" + ext + "\",\"title\":\"" + ext + " title\",\"requests\":\"" + requests.Replace("\\", "\\\\")
            + "\",\"postprocess_failure\":\"" + fail + "\",\"channel\":[" + string.Join(",", channel.Split(' ').Select(x => "\"" + x + "\""))
            + "],\"playlists\":{\"one\":[\"" + A + "\",\"" + C + "\",\"" + D + "\"],\"two\":[\"" + B + "\",\"" + D + "\"]}}";
        File.WriteAllText(config, json, new UTF8Encoding(false));
    }
    private static int Transfers { get { return File.Exists(requests) ? File.ReadAllLines(requests).Length : 0; } }
    private static string Url(string id) { return BaseUrl + "video/" + id; }
    private static void Run(string urls, string template = null, string extra = "", bool fails = false) {
        using (var process = new Process()) {
            process.StartInfo = new ProcessStartInfo(typeof(NativeSuite).Assembly.Location,
                urls + " -o " + HistoryCommandPolicy.Quote(Path.Combine(library, template ?? schema)) + " --retries 0 --no-warnings " + extra) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            var output = new StringBuilder();
            DataReceivedEventHandler receive = (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.OutputDataReceived += receive; process.ErrorDataReceived += receive;
            Call(runtime, "Start", process, new Func<bool>(() => false), new Action<string>(text => Console.WriteLine("HISTORY " + text)));
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            if (!process.WaitForExit(60000)) { process.Kill(); throw new Exception("Native fixture timed out."); }
            process.WaitForExit();
            bool healthy = (bool)Call(runtime, "Complete", process, new Action<string>(Console.WriteLine));
            Check(healthy, "Postflight failed: " + output);
            Check(fails ? process.ExitCode != 0 : process.ExitCode == 0, "Unexpected provider result: " + output);
        }
    }
    public static int Proxy(string[] args) {
        string python = Environment.GetEnvironmentVariable("YTDLG_TEST_PYTHON");
        string script = Environment.GetEnvironmentVariable("YTDLG_TEST_SCRIPT");
        using (var child = new Process()) {
            child.StartInfo = new ProcessStartInfo(python,
                HistoryCommandPolicy.Quote(script) + " " + string.Join(" ", args.Select(HistoryCommandPolicy.Quote))) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            child.OutputDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            child.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
            child.Start(); child.BeginOutputReadLine(); child.BeginErrorReadLine();
            child.WaitForExit(); return child.ExitCode;
        }
    }
    public static int RunSuite(string[] args) {
        if (args.Length != 4) throw new ArgumentException("Expected application, Python, fixture script and evidence directory.");
        string appPath = Path.GetFullPath(args[0]); string evidence = Path.GetFullPath(args[3]);
        Directory.CreateDirectory(evidence);
        workspace = Path.Combine(Path.GetTempPath(), "ytdlg-native-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(workspace);
        string oldDirectory = Environment.CurrentDirectory;
        config = Path.Combine(workspace, "fixture.json");
        Environment.SetEnvironmentVariable("YTDLG_TEST_PYTHON", Path.GetFullPath(args[1]));
        Environment.SetEnvironmentVariable("YTDLG_TEST_SCRIPT", Path.GetFullPath(args[2]));
        Environment.SetEnvironmentVariable("YTDLG_TEST_CONFIG", config);
        Environment.SetEnvironmentVariable("PYTHONUTF8", "1");
        Environment.CurrentDirectory = workspace;
        try {
            app = Assembly.LoadFrom(appPath);
            settings = app.GetType("youtube_dl_gui.DownloadHistorySettings", true);
            runtime = app.GetType("youtube_dl_gui.DownloadHistoryRuntime", true);
            Test("never-enabled mode has no archive or mandatory filename IDs", () => {
                Check(!(bool)settings.GetProperty("Enabled").GetValue(null), "Feature defaulted to enabled.");
                Initialize("never-enabled"); Run(Url(A), "%(title)s.%(ext)s");
                Check(Transfers == 1 && !File.Exists(Archive), "Disabled mode used a ledger.");
                Check(Directory.GetFiles(library, "*.json").Length == 0, "Disabled mode forced metadata.");
            });
            Test("container change, channel refresh and mixed-source duplicates", () => {
                Initialize("collections"); Reconcile(); Save(true); Run(Url(A)); Check(Transfers == 1, "Initial media missing.");
                Configure("mp4"); Run(Url(A)); Check(Transfers == 1, "Container/title change duplicated the source.");
                Run(BaseUrl + "channel/videos"); Run(BaseUrl + "channel/videos"); Check(Transfers == 2, "Channel re-run duplicated media.");
                Configure("mp4", A + " " + B + " " + C); Run(BaseUrl + "channel/videos"); Check(Transfers == 3, "New channel entry was not discovered.");
                string mixed = BaseUrl + "playlist/one " + BaseUrl + "playlist/two " + Url(D) + " " + BaseUrl + "channel/videos";
                Run(mixed); Run(mixed); Check(Transfers == 4, "Mixed inputs duplicated media or stopped early.");
                Check(File.ReadAllLines(Archive).Count(x => !string.IsNullOrWhiteSpace(x)) == 4, "Wrong source identity count.");
            });
            Test("lost ledger rebuilds from media metadata without redownloading", () => {
                Initialize("lost-ledger"); Reconcile(); Save(true); Run(Url(A));
                File.Delete(Archive); File.Delete(Archive + ".bak"); File.Delete(Archive + ".state.json");
                Configure("mp4"); Run(Url(A)); Check(Transfers == 1, "Ledger recovery caused a duplicate.");
            });
            Test("disable preserves history and re-enable discovers the disabled interval", () => {
                Initialize("lifecycle"); Reconcile(); Save(true); Run(Url(A)); Save(false);
                byte[] ledger = File.ReadAllBytes(Archive), backup = File.ReadAllBytes(Archive + ".bak");
                Run(Url(B), "%(title)s [%(extractor_key)s %(id)s].%(ext)s");
                Check(ledger.SequenceEqual(File.ReadAllBytes(Archive)) && backup.SequenceEqual(File.ReadAllBytes(Archive + ".bak")), "Disabled job changed history.");
                Reconcile(); Save(true); Run(Url(A) + " " + Url(B)); Check(Transfers == 2, "Re-enabled history missed disabled media.");
            });
            Test("metadata-backed legacy filenames migrate before protected use", () => {
                Initialize("migration"); Run(Url(A), "%(title)s.%(ext)s", "--write-info-json");
                // The fixture title itself includes the ID; rename both files to a genuinely ID-less name.
                string media = Directory.GetFiles(library, "*.webm").Single(); string metadata = Directory.GetFiles(library, "*.info.json").Single();
                File.Move(media, Path.Combine(library, "Legacy.webm")); File.Move(metadata, Path.Combine(library, "Legacy.info.json"));
                Reconcile(true); Save(true); Run(Url(A));
                Check(Transfers == 1 && File.Exists(Path.Combine(library, "Legacy-" + A + ".webm")), "Legacy migration failed.");
            });
            Test("failed native post-processing remains retryable", () => {
                Initialize("failed-download"); Reconcile(); Save(true); Configure("webm", A, A);
                Run(Url(A), fails: true); Check(!File.ReadAllText(Archive).Contains(A), "Failed item entered the archive.");
                Configure(); Run(Url(A)); Check(File.ReadAllText(Archive).Contains(A), "Successful retry never entered history.");
            });
            Test("simultaneous jobs share a lease and complete one media transfer", () => {
                Initialize("concurrent"); Reconcile(); Save(true);
                Task.WaitAll(Task.Run(() => Run(Url(A))), Task.Run(() => Run(Url(A))));
                Check(Transfers == 1, "Concurrent workers downloaded duplicate media.");
            });
            Test("native custom archive overrides fail before starting a provider", () => {
                Initialize("custom-override"); Reconcile(); Save(true); bool rejected = false;
                try { Run(Url(A), extra: "--download-archive other.txt"); } catch (IOException) { rejected = true; }
                Check(rejected && Transfers == 0, "Conflicting archive reached the provider.");
            });
            Test("built Download History dialog renders and disable preserves files", () => {
                Initialize("ui"); Reconcile(); Save(true);
                byte[] ledger = File.ReadAllBytes(Archive), backup = File.ReadAllBytes(Archive + ".bak");
                using (var form = (Form)Activator.CreateInstance(app.GetType("youtube_dl_gui.frmDownloadHistory", true), true)) {
                    form.Show(); Application.DoEvents();
                    using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(evidence, "download-history-settings.png")); }
                    var enabled = (CheckBox)form.GetType().GetField("enabled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    var apply = (Button)form.GetType().GetField("apply", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    enabled.Checked = false; apply.PerformClick(); Application.DoEvents();
                    Check(!(bool)settings.GetProperty("Enabled").GetValue(null), "Disable setting was not saved.");
                    Check(ledger.SequenceEqual(File.ReadAllBytes(Archive)) && backup.SequenceEqual(File.ReadAllBytes(Archive + ".bak")), "GUI disable destroyed history.");
                    form.Close();
                }
            });
            Console.WriteLine("NATIVE RESULT " + successes + " passed; " + failures + " failed; yt-dlp fixture uses the real CLI parser/downloader/archive implementation.");
            return failures == 0 ? 0 : 1;
        }
        finally {
            Environment.CurrentDirectory = oldDirectory;
            Environment.SetEnvironmentVariable("YTDLG_TEST_PYTHON", null);
            Environment.SetEnvironmentVariable("YTDLG_TEST_SCRIPT", null);
            Environment.SetEnvironmentVariable("YTDLG_TEST_CONFIG", null);
            Directory.Delete(workspace, true);
        }
    }
}

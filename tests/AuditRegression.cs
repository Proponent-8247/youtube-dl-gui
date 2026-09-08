using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Linq;

// Runs against the compiled application, not a transcription of its algorithms.
// The child modes perform no network access and never touch a media library.
internal static class AuditRegression {
    private static Assembly App;
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly List<XElement> Results = new List<XElement>();
    private static string Self { get { return Assembly.GetExecutingAssembly().Location; } }
    private static Type T(string name) { return App.GetType(name, true); }
    private static object Call(Type type, object target, string name, params object[] args) {
        MethodInfo method = type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    private static void Set(Type type, object target, string name, object value) {
        type.GetProperty(name, All).SetValue(target, value, null);
    }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name, All).GetValue(target, null); }
    private static object New(string name, params object[] args) {
        try { return Activator.CreateInstance(T(name), All, null, args, CultureInfo.InvariantCulture); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    private static void Equal(object expected, object actual) {
        if (!object.Equals(expected, actual)) throw new Exception("Expected [" + expected + "]; actual [" + actual + "]");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Throws<TException>(Action action) where TException : Exception {
        try { action(); } catch (TException) { return; }
        throw new Exception("Expected " + typeof(TException).Name);
    }
    private static void Test(string name, Action action) {
        var watch = Stopwatch.StartNew();
        var result = new XElement("testcase", new XAttribute("name", name), new XAttribute("classname", "AuditRegression"));
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { result.Add(new XElement("failure", new XAttribute("message", e.Message), e.ToString())); Console.WriteLine("FAIL " + name + ": " + e.Message); }
        result.Add(new XAttribute("time", watch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture)));
        Results.Add(result);
    }
    private static string Escape(string value) { return (string)Call(T("youtube_dl_gui.ArgumentList"), null, "EscapeArgument", value); }
    private static object Time(int seconds, int milliseconds) { return New("murrty.controls.Time", 0, 0, seconds, milliseconds); }
    private static string Join(string[] values, string separator, int limit) { return (string)Call(T("youtube_dl_gui.Extensions"), null, "JoinUntilLimit", values, separator, limit); }
    private static string LanguageValue(string line) {
        object[] args = { line, null, null };
        Call(T("youtube_dl_gui.Language"), null, "GetControlInfo", args);
        return (string)args[2];
    }
    private static object Download(string source) {
        object info = New("youtube_dl_gui.DownloadInfo", source);
        Set(info.GetType(), info, "Type", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
        Set(info.GetType(), info, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
        return info;
    }
    private static string Generate(object info) {
        Require((bool)Call(info.GetType(), info, "GenerateArguments", (Action<string>)(s => { })), "Argument generation failed");
        return (string)Get(info, "Arguments");
    }
    private static void TestArguments() {
        Test("D002.WindowsArgumentRoundTrip", () => {
            string[] values = { "", "plain", "two  spaces", "tab\there", "quote\"here", "trailing slash \\", "\\\\server\\share\\", "--help", "https://example.invalid/?x=1&y=2", "a\\\\\"b", "unicode-\u03bb" };
            using (Process p = Process.Start(new ProcessStartInfo(Self, "--echo " + string.Join(" ", values.Select(Escape))) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })) {
                string data = p.StandardOutput.ReadToEnd();
                Require(p.WaitForExit(10000), "Recording child hung");
                Equal(0, p.ExitCode);
                string[] actual = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => Encoding.UTF8.GetString(Convert.FromBase64String(x.Substring(1)))).ToArray();
                Equal(values.Length, actual.Length);
                for (int i = 0; i < values.Length; i++) Equal(values[i], actual[i]);
            }
        });
        Test("D002.QuickSourceIsOperand", () => Require(Generate(Download("--help")).EndsWith("-- --help", StringComparison.Ordinal), "Source was not placed after --"));
        Test("D032.QuotedWhitespacePreserved", () => {
            object info = Download("https://example.invalid/video");
            string custom = "--add-header \"X-Audit: two  spaces\"";
            Set(info.GetType(), info, "CustomArguments", custom);
            Require(Generate(info).Contains(custom), "Quoted custom value was rewritten");
        });
        Test("D017.ExplicitPlaylistDoesNotDisablePlaylist", () => {
            object info = Download("https://example.invalid/watch?v=1&list=2");
            Set(info.GetType(), info, "PlaylistSelection", Enum.Parse(T("youtube_dl_gui.PlaylistSelectionType"), "PlaylistItems"));
            Set(info.GetType(), info, "PlaylistSelectionArg", "2,5");
            string args = Generate(info);
            Require(args.Contains("--playlist-items 2,5"), "Selection not forwarded");
            Require(!args.Contains("--no-playlist"), "Explicit playlist disabled");
        });
        Test("D017.SingleVideoDefaultPreserved", () => Require(Generate(Download("https://example.invalid/video")).Contains("--no-playlist"), "Single-video default changed"));
    }
    [STAThread]
    private static int Main(string[] args) {
        if (args.Length > 0 && args[0] == "--echo") {
            foreach (string value in args.Skip(1)) Console.WriteLine("=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
            return 0;
        }
        if (args.Length >= 2 && args[0] == "--fixture") {
            if (args.Length >= 3) File.WriteAllText(args[2], Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
            if (args[1] == "hang") { Thread.Sleep(Timeout.Infinite); return 0; }
            if (args[1] == "large") { Console.Write(new string('x', 1048576)); return 0; }
            if (args[1] == "stdin") { Console.In.ReadToEnd(); Console.Write("EOF"); return 0; }
            return 2;
        }
        if (args.Length != 2) { Console.Error.WriteLine("Usage: AuditRegression.exe application.exe results.xml"); return 2; }
        string assemblyPath = Path.GetFullPath(args[0]);
        string resultPath = Path.GetFullPath(args[1]);
        string originalDirectory = Environment.CurrentDirectory;
        string scratch = Path.Combine(Path.GetTempPath(), "youtube-dl-gui-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try {
            Environment.CurrentDirectory = scratch;
            // Configuration and file operations are isolated from the user's installation.
            File.WriteAllText(Path.Combine(scratch, "youtube-dl-gui.ini"), "[youtube-dl-gui]\r\nfirstTime=False\r\nCheckForUpdatesOnLaunch=False\r\nAutoUpdateYoutubeDl=False\r\n");
            App = Assembly.LoadFrom(assemblyPath);
            Call(T("youtube_dl_gui.Language"), null, "LoadInternalEnglish");
            Set(T("youtube_dl_gui.Verification"), null, "YoutubeDlPath", Self);
            Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", Self);
            Test("H001.UncSourcePreserved", () => {
                const string source = "\\\\server\\share\\video.mkv";
                object media = New("youtube_dl_gui.ExtendedMediaDetails", source);
                Equal(source, Get(media, "URL"));
            });
            Test("H004.FractionalSeconds", () => {
                foreach (int ms in new[] { 1, 5, 50, 500, 999 }) {
                    Equal("0." + ms.ToString("D3", CultureInfo.InvariantCulture), Time(0, ms).ToString());
                    Equal("1." + ms.ToString("D3", CultureInfo.InvariantCulture), Time(1, ms).ToString());
                }
            });
            Test("H005.JoinSeparatorAndLimit", () => {
                Equal("a,b", Join(new[] { "a", "b" }, ",", 3));
                Equal("a", Join(new[] { "a", "b" }, ",", 2));
                Equal("", Join(new[] { "long" }, ",", 2));
                Equal("a::b", Join(new[] { "a", "b", "c" }, "::", 4));
                Equal("a b", Join(new[] { "a", "b" }, " ", 3));
            });
            Test("H006.ForwardRangeAndEquality", () => {
                object first = New("murrty.controls.TimeOffset", Time(1, 0), Time(2, 0));
                object second = New("murrty.controls.TimeOffset", Time(1, 0), Time(2, 0));
                MethodInfo equals = first.GetType().GetMethod("Equals", new[] { first.GetType() });
                Require((bool)equals.Invoke(first, new[] { second }), "Typed equality failed");
                Require(first.Equals(second), "Object equality failed");
                Equal(first.GetHashCode(), second.GetHashCode());
                Throws<ArgumentOutOfRangeException>(() => New("murrty.controls.TimeOffset", Time(2, 0), Time(1, 0)));
            });
            Test("D034.LanguageUrlAndEquals", () => Equal("https://example.invalid/help?x=1&y=2", LanguageValue("key = https://example.invalid/help?x=1&y=2")));
            Test("D034.LanguageInlineComment", () => Equal("value", LanguageValue("key = value // comment")));
            Test("D034.LanguageQuotedSlashes", () => Equal("\"literal // content\"", LanguageValue("key = \"literal // content\" // comment")));
            TestArguments();
        }
        catch (Exception e) { Test("Harness.Initialization", () => { throw e; }); }
        finally {
            Environment.CurrentDirectory = originalDirectory;
            try { Directory.Delete(scratch, true); } catch (IOException) { }
            int failures = Results.Count(x => x.Element("failure") != null);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            new XDocument(new XElement("testsuite", new XAttribute("name", "Compiled application audit"), new XAttribute("tests", Results.Count), new XAttribute("failures", failures), Results)).Save(resultPath);
        }
        return Results.Any(x => x.Element("failure") != null) ? 1 : 0;
    }
}

using System;
using System.IO;
using System.Text;

internal static partial class AuditRegression {
    private static void ProviderDiscoveryUsesFullyInitializedPersistedType() {
        string app = App.Location;
        string fixture = Path.Combine(Path.GetTempPath(), "audit-provider-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try {
            string probeSource = Path.Combine(fixture, "ProviderInitProbe.cs");
            string probeExe = Path.Combine(fixture, "ProviderInitProbe.exe");
            const string source = @"
using System;
using System.IO;
using System.Reflection;
class ProviderInitProbe {
  static int Main(string[] args) {
    try {
      Assembly app = Assembly.LoadFrom(args[0]);
      Type verification = app.GetType(""youtube_dl_gui.Verification"", true);
      Type downloads = app.GetType(""youtube_dl_gui.Downloads"", true);
      BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
      // Verification MUST initialize before our first explicit Downloads access.
      // This is the original reentrancy trigger under test.
      string discovered = (string)verification.GetProperty(""YoutubeDlPath"", flags).GetValue(null, null);
      int type = (int)downloads.GetProperty(""YtdlType"", flags).GetValue(null, null);
      int expected = int.Parse(args[1]);
      string[] names = { ""yt-dlp.exe"", ""yt-dlp-nightly.exe"", ""youtube-dl.exe"", ""youtube-dl-nightly.exe"" };
      if (type != expected || discovered == null || !Path.GetFileName(discovered).Equals(names[expected], StringComparison.OrdinalIgnoreCase)) {
        Console.Error.WriteLine(""STALE_PROVIDER: requested="" + expected + "", actual="" + type + "", path="" + discovered);
        return 2;
      }
      // Switching providers must invalidate the old cached executable before
      // another execution/update sees it.
      downloads.GetProperty(""YtdlType"", flags).SetValue(null, 1, null);
      string changed = (string)verification.GetProperty(""YoutubeDlPath"", flags).GetValue(null, null);
      if (changed == null || !Path.GetFileName(changed).Equals(""yt-dlp-nightly.exe"", StringComparison.OrdinalIgnoreCase)) {
        Console.Error.WriteLine(""STALE_SELECTION: "" + changed);
        return 3;
      }
      Console.WriteLine(""PROVIDER_DISCOVERY_OK"");
      return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex); return 5; }
  }
}
";
            File.WriteAllText(probeSource, source, new UTF8Encoding(false));
            string windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
            string csc = Path.Combine(windir, "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
            if (!File.Exists(csc)) csc = Path.Combine(windir, "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe");
            Require(File.Exists(csc), "Windows C# compiler unavailable for fresh-process provider test");
            int compiled;
            string result = GuardRunCli(csc, "/nologo /target:exe /out:\"" + probeExe + "\" \"" + probeSource + "\"", fixture, out compiled);
            Require(compiled == 0, "Fresh provider probe compilation failed: " + result);
            string[] names = { "yt-dlp.exe", "yt-dlp-nightly.exe", "youtube-dl.exe", "youtube-dl-nightly.exe" };
            foreach (string name in names) File.WriteAllText(Path.Combine(fixture, name), "provider fixture " + name);
            foreach (int configured in new[] { 0, 1, 2, 3, 999 }) {
                // The config parser reads youtube-dl-gui.ini from the child process
                // current directory. Each case is a wholly fresh CLR process.
                File.WriteAllText(Path.Combine(fixture, "youtube-dl-gui.ini"),
                    "[Downloads]\r\nYtdlType=" + configured + "\r\n[General]\r\nUseStaticYtdl=False\r\n",
                    new UTF8Encoding(false));
                int expected = configured >= 0 && configured <= 3 ? configured : 0;
                int exit;
                string output = GuardRunCli(probeExe, "\"" + app + "\" " + expected, fixture, out exit);
                Require(exit == 0 && output.Contains("PROVIDER_DISCOVERY_OK"),
                    "Fresh-process provider discovery was stale for " + configured + ": " + output);
            }
        }
        finally { try { Directory.Delete(fixture, true); } catch { } }
    }
}

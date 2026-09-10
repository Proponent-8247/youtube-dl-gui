using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static string Sha256(Stream stream) {
        using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string FileSha256(string file) { using (Stream stream = File.OpenRead(file)) return Sha256(stream); }

    private delegate bool O010EnumWindow(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "EnumThreadWindows")] private static extern bool O010EnumThreadWindows(uint thread, O010EnumWindow callback, IntPtr data);
    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")] private static extern uint O010GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)] private static extern int O010GetClassName(IntPtr hwnd, StringBuilder text, int capacity);
    [DllImport("user32.dll", EntryPoint = "GetDlgItem")] private static extern IntPtr O010GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendO010Button(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);

    private static void VerifyUpdaterMismatchCannotBeIgnored() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        Assembly updaterAssembly = Assembly.LoadFrom(Path.Combine(root, "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe"));
        Type language = updaterAssembly.GetType("youtube_dl_gui_updater.Language", true);
        Type updateDataType = updaterAssembly.GetType("youtube_dl_gui_updater.UpdateData", false) ?? updaterAssembly.GetTypes().Single(t => t.Name == "UpdateData");
        Type updaterType = updaterAssembly.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Call(language, null, "LoadInternalEnglish");

        string file = Path.Combine(Environment.CurrentDirectory, "o010-mismatch.part");
        File.WriteAllText(file, "mismatched payload");
        bool sawDialog = false;
        bool ignoreExposed = false;
        Exception unexpected = null;
        uint thread = O010GetCurrentThreadId();
        using (Form form = (Form)Activator.CreateInstance(updaterType, true))
        using (Timer timer = new Timer { Interval = 50 }) {
            object data = Activator.CreateInstance(updateDataType);
            Set(updateDataType, data, "UpdateHash", new string('0', 64));
            Field(form, "UpdateData", data);
            IntPtr handle = form.Handle;
            timer.Tick += (sender, args) => O010EnumThreadWindows(thread, (window, state) => {
                StringBuilder name = new StringBuilder(256);
                O010GetClassName(window, name, name.Capacity);
                if (name.ToString() != "#32770") return true;
                try {
                    sawDialog = true;
                    ignoreExposed |= O010GetDlgItem(window, (int)DialogResult.Ignore) != IntPtr.Zero;
                    IntPtr abort = O010GetDlgItem(window, (int)DialogResult.Abort);
                    if (abort != IntPtr.Zero) SendO010Button(abort, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception ex) { unexpected = ex; }
                return false;
            }, IntPtr.Zero);
            try {
                timer.Start();
                Task task = (Task)Call(updaterType, form, "VerifyHash", "https://example.invalid/update.exe", file);
                PumpUntil(() => task.IsCompleted, 10000, "Updater hash-mismatch dialog did not complete");
                bool rejected = false;
                try { task.GetAwaiter().GetResult(); }
                catch (CryptographicException) { rejected = true; }
                if (unexpected != null) throw unexpected;
                Require(sawDialog, "Updater hash mismatch did not present its recovery dialog");
                Require(rejected, "Updater hash mismatch was not rejected after Abort");
                Require(!ignoreExposed, "Updater hash mismatch still offers an Ignore override");
            }
            finally { timer.Stop(); }
        }
        if (File.Exists(file)) File.Delete(file);
    }

    static partial void RunPackagingTests() {
        Test("N005.BuildDateGeneratedWithoutExternalHelper", () => {
            string text = (string)T("youtube_dl_gui.GeneratedBuildDate").GetField("Value", All).GetValue(null);
            DateTime value;
            Require(DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value), "Generated build date is invalid");
            Require(value.Date <= DateTime.UtcNow.Date && value.Date >= DateTime.UtcNow.AddDays(-1).Date, "Generated build date is stale");
        });
        Test("Packaging.EmbeddedUpdaterHashMatchesActualResource", () => {
            string expected = (string)T("youtube_dl_gui.GeneratedUpdaterHash").GetField("Value", All).GetValue(null);
            byte[] updater = (byte[])T("youtube_dl_gui.Properties.Resources").GetProperty("youtube_dl_gui_updater", All).GetValue(null, null);
            using (MemoryStream memory = new MemoryStream(updater)) Equal(expected.ToLowerInvariant(), Sha256(memory));
            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
            Equal(FileSha256(Path.Combine(root, "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe")), expected.ToLowerInvariant());
        });
        Test("O009.UpdaterIntegrityIsFailClosed", () => {
            string invalid = Path.Combine(Environment.CurrentDirectory, "o009-invalid-updater.exe");
            string valid = Path.Combine(Environment.CurrentDirectory, "o009-valid-updater.exe");
            try {
                File.WriteAllText(invalid, "tampered updater bytes");
                Require(!(bool)Call(T("youtube_dl_gui.Updater"), null, "UpdaterFileMatchesKnownHash", invalid), "A mismatched updater was accepted");

                byte[] updater = (byte[])T("youtube_dl_gui.Properties.Resources").GetProperty("youtube_dl_gui_updater", All).GetValue(null, null);
                File.WriteAllBytes(valid, updater);
                Require((bool)Call(T("youtube_dl_gui.Updater"), null, "UpdaterFileMatchesKnownHash", valid), "The build-pinned embedded updater was rejected");
            }
            finally {
                if (File.Exists(invalid)) File.Delete(invalid);
                if (File.Exists(valid)) File.Delete(valid);
            }
        });
        Test("O010.UpdaterHashMismatchHasNoIgnoreOverride", VerifyUpdaterMismatchCannotBeIgnored);
        Test("N005.ReleaseArchiveAndChecksumsMatchBuiltFiles", () => {
            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
            string release = Path.Combine(root, "Release");
            string exe = Path.Combine(release, "youtube-dl-gui.exe");
            string zip = Path.Combine(release, "youtube-dl-gui.zip");
            string manifest = Path.Combine(release, "Release-hashes.md");
            Require(File.Exists(exe) && File.Exists(zip) && File.Exists(manifest), "Release packaging outputs are missing");
            Equal(FileSha256(App.Location), FileSha256(exe));
            string hashes = File.ReadAllText(manifest);
            object data = New("murrty.updater.GithubData");
            Set(data.GetType(), data, "VersionDescription", hashes);
            Equal(FileSha256(exe), ((string)Call(data.GetType(), data, "FindHash")).ToLowerInvariant());
            Require(hashes.ToLowerInvariant().Contains("zip sha256: " + FileSha256(zip)), "ZIP checksum is wrong");
            using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                string[] names = archive.Entries.Where(e => !e.FullName.EndsWith("/")).Select(e => e.FullName.Replace('\\', '/')).OrderBy(n => n).ToArray();
                string[] expected = Directory.GetFiles(Path.Combine(root, "Languages"), "*.ini").Select(p => "lang/" + Path.GetFileName(p)).Concat(new[] { "youtube-dl-gui.exe" }).OrderBy(n => n).ToArray();
                Equal(string.Join("\n", expected), string.Join("\n", names));
                using (Stream packaged = archive.GetEntry("youtube-dl-gui.exe").Open()) Equal(FileSha256(exe), Sha256(packaged));
                foreach (string languageFile in Directory.GetFiles(Path.Combine(root, "Languages"), "*.ini")) {
                    using (Stream packaged = archive.GetEntry("lang/" + Path.GetFileName(languageFile)).Open()) Equal(FileSha256(languageFile), Sha256(packaged));
                }
            }
        });
    }
}

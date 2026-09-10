using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

internal static partial class AuditRegression {
    private static string Sha256(Stream stream) {
        using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string FileSha256(string file) { using (Stream stream = File.OpenRead(file)) return Sha256(stream); }
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
                foreach (string language in Directory.GetFiles(Path.Combine(root, "Languages"), "*.ini")) {
                    using (Stream packaged = archive.GetEntry("lang/" + Path.GetFileName(language)).Open()) Equal(FileSha256(language), Sha256(packaged));
                }
            }
        });
    }
}

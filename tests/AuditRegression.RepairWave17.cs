using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object Wave17Asset(string name, string digest, string url, long length) {
        Type type = T("murrty.updater.GithubAsset");
        object asset = Activator.CreateInstance(type);
        PropertyInfo nameProperty = type.GetProperty("Name", All);
        PropertyInfo digestProperty = type.GetProperty("Digest", All);
        PropertyInfo urlProperty = type.GetProperty("DownloadUrl", All);
        Require(nameProperty != null && digestProperty != null && urlProperty != null,
            "GithubAsset does not expose release name/digest/download metadata");
        nameProperty.SetValue(asset, name, null);
        digestProperty.SetValue(asset, digest, null);
        urlProperty.SetValue(asset, url, null);
        type.GetProperty("Length", All).SetValue(asset, length, null);
        return asset;
    }

    private static object Wave17Release(params object[] assets) {
        Type assetType = T("murrty.updater.GithubAsset");
        Array array = Array.CreateInstance(assetType, assets.Length);
        for (int i = 0; i < assets.Length; i++) array.SetValue(assets[i], i);
        object release = New("murrty.updater.GithubData");
        Set(release.GetType(), release, "Files", array);
        return release;
    }

    private static void ProviderReleaseRequiresAuthoritativeDigest() {
        Type updater = T("youtube_dl_gui.Updater");
        string hash = new string('a', 64);
        object modern = Wave17Release(Wave17Asset("yt-dlp.exe", "sha256:" + hash,
            "https://example.invalid/yt-dlp.exe", 1234));
        object[] modernArgs = { modern, "yt-dlp.exe", null, null, null, 0L };
        Equal(true, Call(updater, null, "TryGetProviderAsset", modernArgs));
        Equal("https://example.invalid/yt-dlp.exe", modernArgs[2]);
        Equal(hash, modernArgs[3]);
        Equal(null, modernArgs[4]);
        Equal(1234L, modernArgs[5]);

        object legacy = Wave17Release(
            Wave17Asset("youtube-dl.exe", null, "https://example.invalid/youtube-dl.exe", 2222),
            Wave17Asset("SHA2-256SUMS", null, "https://example.invalid/SHA2-256SUMS", 2048));
        object[] legacyArgs = { legacy, "youtube-dl.exe", null, null, null, 0L };
        Equal(true, Call(updater, null, "TryGetProviderAsset", legacyArgs));
        Equal("https://example.invalid/youtube-dl.exe", legacyArgs[2]);
        Equal(null, legacyArgs[3]);
        Equal("https://example.invalid/SHA2-256SUMS", legacyArgs[4]);
        Equal(2222L, legacyArgs[5]);

        object unverifiable = Wave17Release(Wave17Asset("yt-dlp.exe", null,
            "https://example.invalid/yt-dlp.exe", 3333));
        object[] badArgs = { unverifiable, "yt-dlp.exe", null, null, null, 0L };
        Equal(false, Call(updater, null, "TryGetProviderAsset", badArgs));

        string destination = Path.Combine(Environment.CurrentDirectory, "provider-current-" + Guid.NewGuid().ToString("N") + ".exe");
        string staged = destination + ".staged";
        try {
            File.WriteAllText(destination, "old-provider", Encoding.UTF8);
            File.WriteAllText(staged, "new-provider", Encoding.UTF8);
            Equal(false, Call(updater, null, "CommitVerifiedExecutable", staged, destination, new string('0', 64)));
            Equal("old-provider", File.ReadAllText(destination, Encoding.UTF8));
            File.WriteAllText(staged, "new-provider", Encoding.UTF8);
            Equal(true, Call(updater, null, "CommitVerifiedExecutable", staged, destination, Wave16Sha256(staged)));
            Equal("new-provider", File.ReadAllText(destination, Encoding.UTF8));
        }
        finally { try { File.Delete(staged); } catch { } try { File.Delete(destination); } catch { } }
    }

    private static void FfmpegArchiveRequiresVerifiedDigest() {
        string archive = Path.Combine(Environment.CurrentDirectory, "ffmpeg-archive-" + Guid.NewGuid().ToString("N") + ".zip");
        try {
            File.WriteAllText(archive, "ffmpeg archive fixture", Encoding.UTF8);
            Equal(true, Call(T("youtube_dl_gui.Updater"), null, "VerifyFfmpegArchive", archive, Wave16Sha256(archive)));
            Equal(false, Call(T("youtube_dl_gui.Updater"), null, "VerifyFfmpegArchive", archive, new string('0', 64)));
        }
        finally { try { File.Delete(archive); } catch { } }
    }

    private static string Wave17GitBlobSha1(byte[] bytes) {
        byte[] header = Encoding.UTF8.GetBytes("blob " + bytes.Length + "\0");
        byte[] combined = new byte[header.Length + bytes.Length];
        Buffer.BlockCopy(header, 0, combined, 0, header.Length);
        Buffer.BlockCopy(bytes, 0, combined, header.Length, bytes.Length);
        using (SHA1 sha = SHA1.Create()) return BitConverter.ToString(sha.ComputeHash(combined)).Replace("-", "").ToLowerInvariant();
    }

    private static void LanguageBlobRequiresGitObjectIdentity() {
        Type updater = T("youtube_dl_gui.Updater");
        byte[] bytes = Encoding.UTF8.GetBytes("[Audit]\r\nkey=value\r\n");
        string staged = Path.Combine(Environment.CurrentDirectory, "language-staged-" + Guid.NewGuid().ToString("N") + ".ini");
        string destination = Path.Combine(Environment.CurrentDirectory, "language-current-" + Guid.NewGuid().ToString("N") + ".ini");
        try {
            File.WriteAllBytes(staged, bytes);
            string objectId = Wave17GitBlobSha1(bytes);
            Equal(true, Call(updater, null, "GithubBlobMatches", staged, objectId));
            Equal(false, Call(updater, null, "GithubBlobMatches", staged, new string('0', 40)));
            File.WriteAllText(destination, "old-language", Encoding.UTF8);
            Equal(false, Call(updater, null, "CommitVerifiedGithubBlob", staged, destination, new string('0', 40)));
            Equal("old-language", File.ReadAllText(destination, Encoding.UTF8));
            File.WriteAllBytes(staged, bytes);
            Equal(true, Call(updater, null, "CommitVerifiedGithubBlob", staged, destination, objectId));
            Require(File.ReadAllBytes(destination).Length == bytes.Length, "Verified language payload was not committed");
        }
        finally { try { File.Delete(staged); } catch { } try { File.Delete(destination); } catch { } }
    }

    private static void FileDownloadsEnforceByteAndDurationBounds() {
        Type clientType = T("murrty.controls.ManagedHttpClient");
        string output = Path.Combine(Environment.CurrentDirectory, "bounded-http-" + Guid.NewGuid().ToString("N") + ".bin");
        using (IDisposable client = (IDisposable)HttpClient()) {
            using (LoopbackResponse server = new LoopbackResponse(200, new byte[4096], null, false, "/bounded.bin")) {
                bool rejected = false;
                try {
                    Task task = (Task)Call(clientType, client, "DownloadFileTaskAsync", server.Uri, output, 1024L, TimeSpan.FromSeconds(5), CancellationToken.None);
                    task.GetAwaiter().GetResult();
                }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "File download exceeded its byte limit without rejection");
                Require(!File.Exists(output) || new FileInfo(output).Length <= 1024, "Oversize file download left an unbounded partial output");
                try { File.Delete(output); } catch { }
            }
            using (LoopbackResponse server = new LoopbackResponse(200, new byte[] { 1 }, null, true, "/deadline.bin")) {
                Stopwatch watch = Stopwatch.StartNew();
                Task task = (Task)Call(clientType, client, "DownloadFileTaskAsync", server.Uri, output, 8192L, TimeSpan.FromMilliseconds(250), CancellationToken.None);
                PumpUntil(() => task.IsCompleted, 3000, "File download ignored its overall duration bound");
                Require(task.IsCanceled || task.IsFaulted, "Duration-bounded download unexpectedly succeeded while response was stalled");
                Require(watch.Elapsed < TimeSpan.FromSeconds(3), "Duration-bounded download completed too late");
                try { File.Delete(output); } catch { }
            }
        }
    }

    private static void SavedWindowLocationRequiresCurrentScreen() {
        Type pointType = T("murrty.structs.Point");
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        object visible = Activator.CreateInstance(pointType, new object[] { work.Left, work.Top });
        object vanished = Activator.CreateInstance(pointType, new object[] { 1000000, 1000000 });
        Equal(true, Get(visible, "Valid"));
        Equal(false, Get(vanished, "Valid"));
    }

    private static void FfmpegZipExtractionIsBounded() {
        Type updater = T("youtube_dl_gui.Updater");
        string zipPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg-bound-" + Guid.NewGuid().ToString("N") + ".zip");
        string output = zipPath + ".exe";
        try {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                ZipArchiveEntry entry = archive.CreateEntry("ffmpeg.exe", CompressionLevel.NoCompression);
                using (Stream stream = entry.Open()) {
                    byte[] payload = new byte[4096];
                    stream.Write(payload, 0, payload.Length);
                }
            }
            using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                bool rejected = false;
                try { Call(updater, null, "ExtractZipEntryBounded", archive.Entries[0], output, 1024L); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Oversize FFmpeg ZIP entry was extracted past its bound");
                try { File.Delete(output); } catch { }
                Call(updater, null, "ExtractZipEntryBounded", archive.Entries[0], output, 8192L);
                Equal(4096L, new FileInfo(output).Length);
            }
        }
        finally { try { File.Delete(output); } catch { } try { File.Delete(zipPath); } catch { } }
    }

    private static void RedditUserscriptTargetsPermalinksSafely() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string path = Path.Combine(root, "Addons", "reddit video download button.user.js");
        string script = File.ReadAllText(path);
        Require(script.IndexOf("@match       http*://", StringComparison.Ordinal) < 0, "Userscript still uses invalid http* match scheme");
        Require(script.Contains("@match       http://*.reddit.com/r/*/comments/*"), "Userscript does not explicitly match HTTP Reddit permalinks");
        Require(script.Contains("@match       https://*.reddit.com/r/*/comments/*"), "Userscript does not explicitly match HTTPS Reddit permalinks");
        Require(script.IndexOf("@match       http://*.reddit.com/r/*\n", StringComparison.Ordinal) < 0 &&
                script.IndexOf("@match       https://*.reddit.com/r/*\n", StringComparison.Ordinal) < 0,
            "Userscript still advertises subreddit listing-page support while targeting document.URL");
    }

    private static void SessionHistoryRetainsAndExports() {
        Type log = T("murrty.logging.Log");
        string marker = "session-history-" + Guid.NewGuid().ToString("N");
        string rawMarker = new string('R', 70000);
        string export = Path.Combine(Environment.CurrentDirectory, "session-export-" + Guid.NewGuid().ToString("N") + ".log");
        try {
            Call(log, null, "WriteSessionHistory", "TEST", marker);
            Call(log, null, "WriteRawProviderHistory", true, rawMarker);
            Call(log, null, "WriteQueueHistory", "ADD", "https://user:password@example.invalid/watch?v=1&token=secret");
            string sessionPath = (string)log.GetProperty("SessionHistoryPath", All).GetValue(null, null);
            string history = File.ReadAllText(sessionPath, Encoding.UTF8);
            Require(history.Contains(marker), "Session log did not retain normal diagnostic history");
            Require(history.Contains(rawMarker), "Session log truncated raw provider history");
            Require(history.Contains("token=[REDACTED]"), "Queue history did not redact sensitive URL query data");
            Require(!history.Contains("user:password@"), "Queue history retained URL authority credentials");
            Equal(true, Call(log, null, "ExportSessionHistory", export));
            Equal(history, File.ReadAllText(export, Encoding.UTF8));

            Type bounded = T("murrty.controls.BoundedProcessOutput");
            Delegate sink = (Delegate)bounded.GetProperty("RawHistorySink", All).GetValue(null, null);
            Require(sink != null, "Bounded process output is not connected to session history");
        }
        finally { try { File.Delete(export); } catch { } }
    }

    private static void RunRepairWave17Tests() {
        Test("POLICY_P002.SessionHistoryRetainsAndExports", SessionHistoryRetainsAndExports);
        Test("CURRENT_O020.ProviderReleaseRequiresAuthoritativeDigest", ProviderReleaseRequiresAuthoritativeDigest);
        Test("CURRENT_O019.FfmpegArchiveRequiresVerifiedDigest", FfmpegArchiveRequiresVerifiedDigest);
        Test("CURRENT_O031.LanguageBlobRequiresGitObjectIdentity", LanguageBlobRequiresGitObjectIdentity);
        Test("CURRENT_O027.FileDownloadsEnforceByteAndDurationBounds", FileDownloadsEnforceByteAndDurationBounds);
        Test("CURRENT_O030.SavedWindowLocationRequiresCurrentScreen", SavedWindowLocationRequiresCurrentScreen);
        Test("CURRENT_O037.FfmpegZipExtractionIsBounded", FfmpegZipExtractionIsBounded);
        Test("CURRENT_O024.RedditUserscriptTargetsPermalinksSafely", RedditUserscriptTargetsPermalinksSafely);
    }
}

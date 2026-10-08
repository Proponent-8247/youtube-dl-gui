using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static partial class AuditRegression {
    private static string Wave16Sha256(string path) {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path)) {
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    private static string ParsedSha256(string method, string value, string fileName) {
        Type updater = T("youtube_dl_gui.Updater");
        MethodInfo parser = updater.GetMethod(method, All);
        Require(parser != null, "Updater." + method + " was not found");
        object[] args = fileName == null ? new object[] { value, null } : new object[] { value, fileName, null };
        Equal(true, parser.Invoke(null, args));
        return (string)args[args.Length - 1];
    }

    private static void UpdateHashesAreParsedStrictly() {
        string hash = new string('a', 64);
        Equal(hash, ParsedSha256("TryParseSha256Digest", "sha256:" + hash.ToUpperInvariant(), null));
        Equal(hash, ParsedSha256("TryParseSha256Checksum", hash + "  yt-dlp.exe\r\n" + new string('b', 64) + "  other.exe\r\n", "yt-dlp.exe"));

        Type updater = T("youtube_dl_gui.Updater");
        MethodInfo digest = updater.GetMethod("TryParseSha256Digest", All);
        object[] invalidDigest = { "sha256:xyz", null };
        Equal(false, digest.Invoke(null, invalidDigest));
        MethodInfo checksum = updater.GetMethod("TryParseSha256Checksum", All);
        object[] wrongFile = { hash + "  other.exe", "yt-dlp.exe", null };
        Equal(false, checksum.Invoke(null, wrongFile));
    }

    private static void HashVerificationRejectsMismatch() {
        string path = Path.Combine(Environment.CurrentDirectory, "p003-hash-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllText(path, "trusted-by-hash-fixture", Encoding.UTF8);
        try {
            string hash = Wave16Sha256(path);
            Equal(true, Call(T("youtube_dl_gui.Updater"), null, "FileMatchesSha256", path, hash));
            Equal(false, Call(T("youtube_dl_gui.Updater"), null, "FileMatchesSha256", path, new string('0', 64)));
        }
        finally { try { File.Delete(path); } catch { } }
    }

    private static void DownloadedExecutableRequiresHashAndValidAvailableSignature() {
        Type updater = T("youtube_dl_gui.Updater");
        string unsigned = Path.Combine(Environment.CurrentDirectory, "p003-unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(unsigned, "unsigned hash-only fixture", Encoding.UTF8);
        try {
            string unsignedHash = Wave16Sha256(unsigned);
            Equal(true, Call(updater, null, "VerifyDownloadedExecutable", unsigned, unsignedHash));
            Equal(false, Call(updater, null, "VerifyDownloadedExecutable", unsigned, new string('0', 64)));
        }
        finally { try { File.Delete(unsigned); } catch { } }

        string signed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        Require(File.Exists(signed), "Signed Windows executable fixture is unavailable");
        Equal(true, Call(updater, null, "VerifyDownloadedExecutable", signed, Wave16Sha256(signed)));
    }

    private static void ProviderSelfUpdaterIsNotTrustedPath() {
        Equal(false, Call(T("youtube_dl_gui.Updater"), null, "CanUseProviderSelfUpdater"));
    }

    private static void RunRepairWave16Tests() {
        Test("POLICY_P003.UpdateHashesAreParsedStrictly", UpdateHashesAreParsedStrictly);
        Test("POLICY_P003.HashVerificationRejectsMismatch", HashVerificationRejectsMismatch);
        Test("POLICY_P003.DownloadedExecutableRequiresHashAndValidAvailableSignature", DownloadedExecutableRequiresHashAndValidAvailableSignature);
        Test("POLICY_P003.ProviderSelfUpdaterIsNotTrustedPath", ProviderSelfUpdaterIsNotTrustedPath);
        RunRepairWave17Tests();
    }
}
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object Wave15Authentication(string user, string password, string twoFactor, string mediaPassword) {
        object auth = New("youtube_dl_gui.AuthenticationDetails");
        Set(auth.GetType(), auth, "Username", user);
        Call(auth.GetType(), auth, "SetPassword", password);
        Set(auth.GetType(), auth, "TwoFactor", twoFactor);
        Call(auth.GetType(), auth, "SetMediaPassword", mediaPassword);
        return auth;
    }

    private static void GenericDownloadSidecarsAreOperationUnique() {
        string output = Path.Combine(Environment.CurrentDirectory, "sidecar-" + Guid.NewGuid().ToString("N") + ".exe");
        using (Form first = (Form)New("youtube_dl_gui.frmGenericDownloadProgress", "http://127.0.0.1:1/a", output))
        using (Form second = (Form)New("youtube_dl_gui.frmGenericDownloadProgress", "http://127.0.0.1:1/b", output)) {
            string firstTemp = (string)Get(first, "TempFile");
            string secondTemp = (string)Get(second, "TempFile");
            string firstBackup = (string)Get(first, "BackupFile");
            string secondBackup = (string)Get(second, "BackupFile");
            Require(!string.Equals(firstTemp, secondTemp, StringComparison.OrdinalIgnoreCase), "Independent generic downloads share the same temp sidecar");
            Require(!string.Equals(firstBackup, secondBackup, StringComparison.OrdinalIgnoreCase), "Independent generic downloads share the same backup sidecar");
            Require(!string.Equals(firstTemp, output + ".tmp", StringComparison.OrdinalIgnoreCase), "Generic download still uses deterministic .tmp sidecar");
            Require(!string.Equals(firstBackup, output + ".bck", StringComparison.OrdinalIgnoreCase), "Generic download still uses deterministic .bck sidecar");
        }
    }

    private static void GeneratedProviderArgumentsDoNotExposeSecrets() {
        const string user = "audit-user";
        const string password = "audit-password-47";
        const string twoFactor = "audit-2fa-83";
        const string mediaPassword = "audit-media-password-91";
        object auth = Wave15Authentication(user, password, twoFactor, mediaPassword);

        object quick = Download("https://example.invalid/video");
        Set(quick.GetType(), quick, "Authentication", auth);
        string quickArgs = Generate(quick);
        foreach (string secret in new[] { password, twoFactor, mediaPassword })
            Require(quickArgs.IndexOf(secret, StringComparison.Ordinal) < 0, "Quick downloader exposed authentication secret in argv");
        foreach (string flag in new[] { "--password ", "--twofactor ", "--video-password " })
            Require(quickArgs.IndexOf(flag, StringComparison.Ordinal) < 0, "Quick downloader still transports a secret option in argv: " + flag);

        object extended = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/video");
        try {
            Set(extended.GetType(), extended, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
            Set(extended.GetType(), extended, "CustomArguments", "--simulate");
            Set(extended.GetType(), extended, "Authentication", Wave15Authentication(user, password, twoFactor, mediaPassword));
            Equal(true, Call(extended.GetType(), extended, "GenerateArguments"));
            string extendedArgs = (string)Get(extended, "Arguments");
            foreach (string secret in new[] { password, twoFactor, mediaPassword })
                Require(extendedArgs.IndexOf(secret, StringComparison.Ordinal) < 0, "Extended downloader exposed authentication secret in argv");
            foreach (string flag in new[] { "--password ", "--twofactor ", "--video-password " })
                Require(extendedArgs.IndexOf(flag, StringComparison.Ordinal) < 0, "Extended downloader still transports a secret option in argv: " + flag);
        }
        finally {
            IDisposable disposable = extended as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }

    private static void ProviderAuthenticationConfigIsPrivateAndEphemeral() {
        Type config = T("youtube_dl_gui.ProviderAuthenticationConfig");
        object auth = Wave15Authentication("audit-user", "audit-secret", "audit-2fa", "audit-media");
        object instance = Call(config, null, "Create", auth);
        Require(instance is IDisposable, "Provider authentication config is not disposable");
        string path = (string)Get(instance, "FilePath");
        try {
            Require(File.Exists(path), "Provider authentication config was not created");
            string text = File.ReadAllText(path);
            Require(text.Contains("audit-secret") && text.Contains("audit-2fa") && text.Contains("audit-media"), "Provider authentication config omitted required secrets");
            FileSecurity security = File.GetAccessControl(path);
            Require(security.AreAccessRulesProtected, "Provider authentication config inherits permissive ACLs");
            SecurityIdentifier current = WindowsIdentity.GetCurrent().User;
            AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules) {
                SecurityIdentifier identity = (SecurityIdentifier)rule.IdentityReference;
                Require(identity.Equals(current) || identity.IsWellKnown(WellKnownSidType.LocalSystemSid),
                    "Provider authentication config grants an unexpected principal: " + identity.Value);
            }
        }
        finally { ((IDisposable)instance).Dispose(); }
        Require(!File.Exists(path), "Provider authentication config survived disposal");
    }

    private static void NormalDiagnosticUrlsAreRedacted() {
        Type log = T("murrty.logging.Log");
        string source = "https://user:pass@example.invalid/path?ok=1&token=abc123&api_key=def456&signature=ghi789#fragment-secret";
        string redacted = (string)Call(log, null, "RedactDiagnosticValue", source);
        Require(redacted.Contains("https://example.invalid/path"), "Diagnostic redaction lost the useful URL location");
        Require(redacted.Contains("ok=1"), "Diagnostic redaction removed a non-secret query value");
        foreach (string secret in new[] { "user", "pass", "abc123", "def456", "ghi789", "fragment-secret" })
            Require(redacted.IndexOf(secret, StringComparison.Ordinal) < 0, "Diagnostic redaction retained secret material: " + secret);
        Require(redacted.IndexOf("[REDACTED]", StringComparison.Ordinal) >= 0, "Diagnostic redaction did not mark removed values");
    }

    private static void ExecutableTrustRecognizesAuthenticode() {
        Type trust = T("youtube_dl_gui.ExecutableTrust");
        string system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        Require(File.Exists(system), "Signed Windows executable fixture is unavailable");
        object valid = Call(trust, null, "GetStatus", system);
        Equal("Valid", valid.ToString());

        string unsigned = Path.Combine(Environment.CurrentDirectory, "unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
        try {
            File.WriteAllText(unsigned, "not a signed executable");
            object status = Call(trust, null, "GetStatus", unsigned);
            Require(status.ToString() != "Valid", "Unsigned fixture was accepted as Authenticode-valid");
        }
        finally { try { File.Delete(unsigned); } catch { } }
    }

    private static void RemoteThumbnailAlwaysUsesIsolatedDecoder() {
        byte[] png;
        using (Bitmap bitmap = new Bitmap(2, 3))
        using (MemoryStream memory = new MemoryStream()) {
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            png = memory.ToArray();
        }
        string pid = Path.Combine(Environment.CurrentDirectory, "thumbnail-isolation-" + Guid.NewGuid().ToString("N") + ".pid");
        string record = pid + ".input";
        object previousFfmpeg = T("youtube_dl_gui.Verification").GetProperty("FFmpegPath", All).GetValue(null, null);
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", "thumbnail-normalize");
        Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", pid);
        Environment.SetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT", record);
        using (LoopbackResponse server = new LoopbackResponse(200, png, null, false, "/preview.png")) {
            try {
                Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe"));
                using (Image image = (Image)Call(T("youtube_dl_gui.YoutubeDlData"), ThumbnailData(server.Uri), "GetThumbnail")) {
                    Equal(2, image.Width);
                    Equal(3, image.Height);
                }
                Require(File.Exists(record), "Ordinary remote PNG bypassed the isolated decoder process");
                string input = File.ReadAllText(record);
                Require(!File.Exists(input), "Isolated thumbnail decoder left its input file behind");
            }
            finally {
                KillFixture(pid);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_PROVIDER_MODE", null);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_PID_FILE", null);
                Environment.SetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT", null);
                Set(T("youtube_dl_gui.Verification"), null, "FFmpegPath", previousFfmpeg);
                try { File.Delete(record); } catch { }
            }
        }
    }

    private static void RunRepairWave15Tests() {
        Test("CURRENT_O008.GenericDownloadSidecarsAreOperationUnique", GenericDownloadSidecarsAreOperationUnique);
        Test("CURRENT_O016.GeneratedProviderArgumentsDoNotExposeSecrets", GeneratedProviderArgumentsDoNotExposeSecrets);
        Test("CURRENT_O016.ProviderAuthenticationConfigIsPrivateAndEphemeral", ProviderAuthenticationConfigIsPrivateAndEphemeral);
        Test("CURRENT_O017.NormalDiagnosticUrlsAreRedacted", NormalDiagnosticUrlsAreRedacted);
        Test("POLICY_P003.ExecutableTrustRecognizesAuthenticode", ExecutableTrustRecognizesAuthenticode);
        Test("POLICY_P005.RemoteThumbnailAlwaysUsesIsolatedDecoder", RemoteThumbnailAlwaysUsesIsolatedDecoder);
    }
}

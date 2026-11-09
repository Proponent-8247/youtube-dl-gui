using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class AuditRegression {
    private static void ReleaseCandidateRejectsUndeclaredArtifactsAndDigestDrift() {
        string repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string guard = Path.Combine(repo, "tools", "Assert-ReleaseCandidateInventory.ps1");
        string root = Path.Combine(Environment.CurrentDirectory,
            "release-inventory-" + Guid.NewGuid().ToString("N"));
        string release = Path.Combine(root, "Release");
        string source = Path.Combine(root, "Languages");
        string packed = Path.Combine(release, "lang");
        Directory.CreateDirectory(release);
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(packed);

        string[] locales = { "English.ini", "Spanish.ini" };
        string[] payload = {
            "youtube-dl-gui.exe", "youtube-dl-gui.zip",
            "Release-hashes.md", "Release-body.md", "Release-notes-3.3.0-2.md",
            "lang/English.ini", "lang/Spanish.ini"
        };
        const string tag = "3.3.0-2";
        try {
            foreach (string locale in locales) {
                string sourceFile = Path.Combine(source, locale);
                File.WriteAllText(sourceFile, "[fixture]\r\nname=" + locale, Encoding.UTF8);
                File.Copy(sourceFile, Path.Combine(packed, locale));
            }
            foreach (string name in payload.Where(n => !n.StartsWith("lang/", StringComparison.Ordinal))) {
                File.WriteAllText(Path.Combine(release, name), "REVIEWED FIXTURE " + name, Encoding.UTF8);
            }
            Func<string> manifest = () => {
                string[] records = payload.Select(name => {
                    string file = Path.Combine(release, name.Replace('/', Path.DirectorySeparatorChar));
                    return "{\"path\":\"" + name + "\",\"sha256\":\"" +
                        FileSha256(file) + "\",\"bytes\":" +
                        new FileInfo(file).Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                }).ToArray();
                return "{\"version\":\"" + tag + "\",\"commit\":\"" +
                    new string('c', 40) + "\",\"exe_sha256\":\"" +
                    FileSha256(Path.Combine(release, payload[0])) + "\",\"zip_sha256\":\"" +
                    FileSha256(Path.Combine(release, payload[1])) +
                    "\",\"artifacts\":[" + string.Join(",", records) + "]}";
            };
            File.WriteAllText(Path.Combine(release, "Release-manifest.json"), manifest(), Encoding.UTF8);

            Func<string> run = () => {
                int exit;
                string output = GuardRunCli("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + guard +
                    "\\" -ReleaseRoot \"" + release + "\\" -ExpectedVersion " + tag +
                    " -SourceLanguageDirectory \"" + source + "\\"",
                    repo, out exit);
                return exit.ToString() + "\n" + output;
            };
            string clean = run();
            string diagnosticPath = Path.Combine(repo, "tools", "Assert-ReleaseCandidateInventory.log");
            string diagnostic = File.Exists(diagnosticPath)
                ? File.ReadAllText(diagnosticPath, Encoding.UTF8) : "(no guard diagnostic log)";
            Require(clean.StartsWith("0\n", StringComparison.Ordinal),
                "Approved release (including source locales) failed pre-upload attestation: " +
                clean + "; latest log: " + diagnostic.Substring(Math.Max(0, diagnostic.Length - 1800)));

            string extra = Path.Combine(release, "unreviewed-provider.exe");
            File.WriteAllText(extra, "NOT PART OF RELEASE", Encoding.UTF8);
            string rejected = run();
            Require(!rejected.StartsWith("0\n", StringComparison.Ordinal) &&
                rejected.Contains("Unexpected release artifact inventory count"),
                "Undeclared build-produced executable was accepted for upload: " + rejected);
            File.Delete(extra);

            string nested = Path.Combine(packed, "unreviewed-language.ini");
            File.WriteAllText(nested, "INJECTED", Encoding.UTF8);
            string nestedRejected = run();
            Require(!nestedRejected.StartsWith("0\n", StringComparison.Ordinal) &&
                nestedRejected.Contains("Unexpected release locale inventory count"),
                "Undeclared file under lang/ was accepted for upload: " + nestedRejected);
            File.Delete(nested);

            string exePath = Path.Combine(release, payload[0]);
            byte[] goodExe = File.ReadAllBytes(exePath);
            File.AppendAllText(exePath, "MODIFIED AFTER VERIFICATION");
            string tampered = run();
            Require(!tampered.StartsWith("0\n", StringComparison.Ordinal) &&
                tampered.Contains("digest/size differs"),
                "Altered release executable passed its stale manifest digest: " + tampered);
            File.WriteAllBytes(exePath, goodExe);

            string localePath = Path.Combine(packed, locales[0]);
            File.AppendAllText(localePath, "INJECTED LOCALE OVERRIDE");
            string tamperedLocale = run();
            Require(!tamperedLocale.StartsWith("0\n", StringComparison.Ordinal) &&
                tamperedLocale.Contains("Packaged locale differs from canonical source"),
                "Changed bundled locale was accepted as authentic source output: " + tamperedLocale);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}

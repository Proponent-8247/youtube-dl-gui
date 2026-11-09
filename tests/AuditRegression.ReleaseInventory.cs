using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class AuditRegression {
    private static void ReleaseCandidateRejectsUndeclaredArtifactsAndDigestDrift() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string guard = Path.Combine(root, "tools", "Assert-ReleaseCandidateInventory.ps1");
        string release = Path.Combine(Environment.CurrentDirectory,
            "release-inventory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(release);
        const string tag = "3.3.0-2";
        string[] payload = { "youtube-dl-gui.exe", "youtube-dl-gui.zip",
            "Release-hashes.md", "Release-body.md", "Release-notes-3.3.0-2.md" };
        try {
            foreach (string name in payload)
                File.WriteAllText(Path.Combine(release, name), "REVIEWED FIXTURE " + name, Encoding.UTF8);
            string[] records = payload.Select(name => {
                string file = Path.Combine(release, name);
                return "{\"path\":\"" + name + "\",\"sha256\":\"" +
                    FileSha256(file) + "\",\"bytes\":" +
                    new FileInfo(file).Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
            }).ToArray();
            string manifest = "{\"version\":\"" + tag + "\",\"commit\":\"" +
                new string('c', 40) + "\",\"exe_sha256\":\"" +
                FileSha256(Path.Combine(release, payload[0])) + "\",\"zip_sha256\":\"" +
                FileSha256(Path.Combine(release, payload[1])) +
                "\",\"artifacts\":[" + string.Join(",", records) + "]}";
            File.WriteAllText(Path.Combine(release, "Release-manifest.json"), manifest, Encoding.UTF8);

            Func<string> run = () => {
                int exit;
                string output = GuardRunCli("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + guard +
                    "\" -ReleaseRoot \"" + release + "\" -ExpectedVersion " + tag,
                    root, out exit);
                return exit.ToString() + "\n" + output;
            };
            string clean = run();
            string diagnosticLog = Path.Combine(root, "tools", "Assert-ReleaseCandidateInventory.log");
            string diagnostic = File.Exists(diagnosticLog)
                ? File.ReadAllText(diagnosticLog, Encoding.UTF8) : "(no guard diagnostic log)";
            Require(clean.StartsWith("0\n", StringComparison.Ordinal),
                "Approved release inventory failed pre-upload attestation: " + clean +
                "; latest guard diagnostic: " + diagnostic.Substring(Math.Max(0, diagnostic.Length - 1600)));
            string extra = Path.Combine(release, "unreviewed-provider.exe");
            File.WriteAllText(extra, "NOT PART OF RELEASE", Encoding.UTF8);
            string rejected = run();
            Require(!rejected.StartsWith("0\n", StringComparison.Ordinal) &&
                rejected.Contains("Unexpected release artifact inventory count"),
                "Undeclared build-produced executable was accepted for upload: " + rejected);
            File.Delete(extra);

            File.AppendAllText(Path.Combine(release, payload[0]), "MODIFIED AFTER VERIFICATION");
            string tampered = run();
            Require(!tampered.StartsWith("0\n", StringComparison.Ordinal) &&
                tampered.Contains("digest/size differs"),
                "Altered release EXE passed a stale manifest digest: " + tampered);
        }
        finally { try { Directory.Delete(release, true); } catch { } }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

internal static partial class AuditRegression {
    private static void ReleaseZipIgnoresWorkingTreeTimestamps() {
        string repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string packer = Path.Combine(repo, "tools", "New-DeterministicReleaseZip.ps1");
        string scratch = Path.Combine(Environment.CurrentDirectory,
            "reproducible-zip-" + Guid.NewGuid().ToString("N"));
        try {
            string[] names = { "youtube-dl-gui.exe", "lang/Spanish.ini",
                "lang/English.ini", "lang/audit-omega.ini" };
            byte[][] payloads = {
                Encoding.UTF8.GetBytes("IDENTICAL EXECUTABLE PAYLOAD"),
                Encoding.UTF8.GetBytes("spanish"),
                Encoding.UTF8.GetBytes("english"),
                Encoding.UTF8.GetBytes("audit-omega")
            };
            string[] outputs = new string[2];
            for (int variant = 0; variant < 2; variant++) {
                string stage = Path.Combine(scratch, "src-" + variant);
                Directory.CreateDirectory(stage);
                for (int i = 0; i < names.Length; i++) {
                    string file = Path.Combine(stage, names[i].Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllBytes(file, payloads[i]);
                    File.SetLastWriteTimeUtc(file, variant == 0
                        ? new DateTime(2002, 5, 3, 8, 5, 0, DateTimeKind.Utc)
                        : new DateTime(2025, 11, 21, 23, 55, 0, DateTimeKind.Utc));
                }
                outputs[variant] = Path.Combine(scratch, "package-" + variant + ".zip");
                int exit;
                string output = GuardRunCli("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + packer +
                    "\" -SourceDirectory \"" + stage + "\" -DestinationFile \"" + outputs[variant] + "\"",
                    repo, out exit);
                Require(exit == 0 && File.Exists(outputs[variant]),
                    "The supported release packer failed: " + output);
            }
            Equal(FileSha256(outputs[0]), FileSha256(outputs[1]));
            Require(File.ReadAllBytes(outputs[0]).SequenceEqual(File.ReadAllBytes(outputs[1])),
                "Equivalent ZIP payloads differ after source mtimes changed");
            using (ZipArchive archive = ZipFile.OpenRead(outputs[0])) {
                string[] sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
                Equal(sorted.Length, archive.Entries.Count);
                for (int i = 0; i < sorted.Length; i++) {
                    ZipArchiveEntry entry = archive.Entries[i];
                    Equal(sorted[i], entry.FullName);
                    Equal(2000, entry.LastWriteTime.Year);
                    Equal(1, entry.LastWriteTime.Month);
                    Equal(1, entry.LastWriteTime.Day);
                    Equal(0, entry.LastWriteTime.Hour);
                    Require(entry.ExternalAttributes == 0, "ZIP metadata was not normalized");
                    using (Stream stream = entry.Open())
                    using (MemoryStream memory = new MemoryStream()) {
                        stream.CopyTo(memory);
                        int original = Array.IndexOf(names, entry.FullName);
                        Require(original >= 0 && payloads[original].SequenceEqual(memory.ToArray()),
                            "Deterministic archive entry did not preserve payload bytes");
                    }
                }
            }
        }
        finally { try { Directory.Delete(scratch, true); } catch { } }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

internal static partial class AuditRegression {
    private static string GuardRunCli(string program, string arguments, string cwd, out int exitCode) {
        ProcessStartInfo psi = new ProcessStartInfo(program);
        psi.Arguments = arguments;
        psi.WorkingDirectory = cwd;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using (Process process = Process.Start(psi)) {
            if (!process.WaitForExit(20000)) {
                try { process.Kill(); } catch { }
                throw new Exception("Guard request fixture process exceeded timeout: " + program);
            }
            exitCode = process.ExitCode;
            return process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();
        }
    }

    private static string GuardGit(string root, string args) {
        int exit;
        string output = GuardRunCli("git.exe", args, root, out exit);
        Require(exit == 0, "Fixture Git operation failed: " + args + ": " + output);
        return output.Trim();
    }

    private static void GuardedRepairRejectsSmuggledRequestCommit() {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string temporary = Path.Combine(Path.GetTempPath(), "audit-request-purity-" + Guid.NewGuid().ToString("N"));
        string work = Path.Combine(temporary, "work");
        string evidence = Path.Combine(temporary, "evidence");
        Directory.CreateDirectory(Path.Combine(work, "tools"));
        Directory.CreateDirectory(Path.Combine(work, "Controls"));
        Directory.CreateDirectory(evidence);
        try {
            string runner = Path.Combine(repoRoot, "tools", "Run-AuditRepairBatch.ps1");
            File.Copy(runner, Path.Combine(work, "tools", "Run-AuditRepairBatch.ps1"));
            File.Copy(Path.Combine(repoRoot, "tools", "apply-audit-repairs.py"),
                Path.Combine(work, "tools", "apply-audit-repairs.py"));
            File.WriteAllText(Path.Combine(work, "Controls", "Fixture.cs"), "original source\n", new UTF8Encoding(false));
            GuardGit(work, "init -q");
            GuardGit(work, "config user.name fixture");
            GuardGit(work, "config user.email fixture@invalid.example");
            GuardGit(work, "add -A");
            GuardGit(work, "commit -q -m base");
            string trustedBase = GuardGit(work, "rev-parse HEAD");
            Require(trustedBase.Length == 40, "Request fixture has no valid base SHA");

            string plan = "{\"version\":2,\"base_sha\":\"" + trustedBase + "\",\"expected_failures\":[\"FIXTURE.REVIEWED_FAILURE\"]," +
                "\"remaining_failures\":[],\"repairs\":[{\"id\":\"fixture\",\"message\":\"test-only\"," +
                "\"resolves\":[\"FIXTURE.REVIEWED_FAILURE\"],\"files\":[{\"path\":\"Controls/Fixture.cs\"," +
                "\"edits\":[{\"old\":\"anchor-does-not-exist\",\"new\":\"new\"}]}]}]}";
            string request = Path.Combine(work, ".audit-repairs.json");
            string partDir = Path.Combine(work, ".audit-repairs.parts");
            string part = Path.Combine(partDir, "part-000.txt");
            string wrapper = "{\"encoding\":\"gzip-base64-parts\",\"parts\":[\".audit-repairs.parts/part-000.txt\"]}";

            Action writeRequest = () => {
                Directory.CreateDirectory(partDir);
                using (MemoryStream memory = new MemoryStream()) {
                    using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true)) {
                        byte[] payload = Encoding.UTF8.GetBytes(plan);
                        gzip.Write(payload, 0, payload.Length);
                    }
                    File.WriteAllText(part, Convert.ToBase64String(memory.ToArray()), Encoding.ASCII);
                }
                File.WriteAllText(request, wrapper, new UTF8Encoding(false));
            };

            GuardGit(work, "checkout -q -b valid-request " + trustedBase);
            writeRequest();
            GuardGit(work, "add -A");
            GuardGit(work, "commit -q -m valid-request");
            int validExit;
            string valid = GuardRunCli("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                Path.Combine(work, "tools", "Run-AuditRepairBatch.ps1") + "\" -EvidenceDirectory \"" +
                Path.Combine(evidence, "valid") + "\"", work, out validExit);
            Require(validExit != 0 &&
                (valid.Contains("Repair preflight failed") || valid.Contains("Expected one unambiguous anchor")),
                "Request-only wrapper plus referenced chunk did not pass request-purity preflight: " + valid);
            Require(!valid.Contains("undeclared paths"),
                "Legitimate request-only commit was rejected as a smuggled source change");
            string validManifest = Path.Combine(evidence, "valid", "request-commit.json");
            string validPatch = Path.Combine(evidence, "valid", "request-commit.patch");
            Require(File.Exists(validManifest) && File.Exists(validPatch),
                "Trusted request-only commit did not produce a reviewable request diff and manifest");
            string requestEvidence = File.ReadAllText(validManifest);
            Require(requestEvidence.Contains(trustedBase) &&
                requestEvidence.Contains(".audit-repairs.json") &&
                requestEvidence.Contains(".audit-repairs.parts/part-000.txt") &&
                !requestEvidence.Contains("Controls/Smuggled.cs"),
                "Request evidence does not bind the expected base and declared wrapper/part paths");
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(validPatch)) {
                string expectedHash = BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", "").ToLowerInvariant();
                Require(requestEvidence.IndexOf(expectedHash, StringComparison.OrdinalIgnoreCase) >= 0,
                    "Request-commit patch digest is not bound to the evidence manifest");
            }

            GuardGit(work, "checkout -q -b smuggled-request " + trustedBase);
            writeRequest();
            File.WriteAllText(Path.Combine(work, "Controls", "Smuggled.cs"),
                "// An undeclared file that must never reach MSBuild\n", new UTF8Encoding(false));
            GuardGit(work, "add -A");
            GuardGit(work, "commit -q -m undeclared-source");
            int rejectedExit;
            string rejected = GuardRunCli("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                Path.Combine(work, "tools", "Run-AuditRepairBatch.ps1") + "\" -EvidenceDirectory \"" +
                Path.Combine(evidence, "rejected") + "\"", work, out rejectedExit);
            Require(rejectedExit != 0 && rejected.Contains("undeclared paths") &&
                rejected.Contains("Controls/Smuggled.cs"),
                "Guard failed to reject an undeclared file in the trigger commit before builds: " + rejected);
            Require(!File.Exists(Path.Combine(evidence, "rejected", "request-commit.json")),
                "Rejected smuggled request was falsely marked as a validated request commit");
            Equal("original source\n", File.ReadAllText(Path.Combine(work, "Controls", "Fixture.cs")));
        }
        finally { try { Directory.Delete(temporary, true); } catch { } }
    }
    private static void GuardedRepairDetectsBuildTimeOracleMutation() {
        string sourceRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string integrityModule = Path.Combine(sourceRoot, "tools", "RepairInputIntegrity.ps1");
        string hostileProject = Path.Combine(sourceRoot, "tests", "AuditOracleTamperProbe.proj");
        string guardedRunner = File.ReadAllText(Path.Combine(sourceRoot, "tools", "Run-AuditRepairBatch.ps1"));
        Require(guardedRunner.Contains("Assert-ImmutableOracle \"$label / after Debug build\"") &&
                guardedRunner.Contains("Assert-ImmutableOracle \"$label / after updater build\"") &&
                guardedRunner.Contains("Assert-ImmutableOracle \"$label / before regression compilation\"") &&
                guardedRunner.Contains("Assert-ImmutableOracle \"$label / after regression execution\""),
            "Guard runner does not check trusted inputs at each build/test boundary");
        string root = Path.Combine(Path.GetTempPath(), "audit-immutable-inputs-" + Guid.NewGuid().ToString("N"));
        string tests = Path.Combine(root, "tests");
        string tools = Path.Combine(root, "tools");
        string workflows = Path.Combine(root, ".github", "workflows");
        Directory.CreateDirectory(tests);
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(workflows);
        string resourceDirectory = Path.Combine(root, "youtube-dl-gui", "Resources");
        Directory.CreateDirectory(resourceDirectory);
        string embeddedUpdater = Path.Combine(resourceDirectory, "youtube-dl-gui-updater.exe");
        try {
            File.WriteAllText(embeddedUpdater, "TRUSTED UPDATER", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(tests, "AuditRegression.Fixture.cs"), "TRUSTED TEST", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(tools, "apply-audit-repairs.py"), "TRUSTED PARSER", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(workflows, "audit-repair.yml"), "TRUSTED WORKFLOW", new UTF8Encoding(false));
            string psQuoteRoot = "'" + root.Replace("'", "''") + "'";
            string psQuoteModule = "'" + integrityModule.Replace("'", "''") + "'";
            string psQuoteProject = "'" + hostileProject.Replace("'", "''") + "'";
            StringBuilder script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine(". " + psQuoteModule);
            script.AppendLine("$root = " + psQuoteRoot);
            script.AppendLine("$project = " + psQuoteProject);
            script.AppendLine("$base = Get-RepairInputSnapshot -RepositoryRoot $root");
            script.AppendLine("Assert-RepairInputSnapshot -RepositoryRoot $root -Baseline $base -Phase 'clean'");
            script.AppendLine("function Require-OracleRejection([string]$label) {");
            script.AppendLine("  try { Assert-RepairInputSnapshot -RepositoryRoot $root -Baseline $base -Phase $label; throw 'UNSAFE_ORACLE_ACCEPTED' }");
            script.AppendLine("  catch { if ($_.Exception.Message -notmatch 'Trusted repair test/tool/workflow inputs mutated') { throw } }");
            script.AppendLine("}");
            script.AppendLine("$testSource = Join-Path $root 'tests/AuditRegression.Fixture.cs'");
            script.AppendLine("& msbuild $project /t:Tamper ('/p:SourceFile=' + $testSource) /nologo /v:quiet");
            script.AppendLine("if ($LASTEXITCODE -ne 0) { throw 'Hostile MSBuild fixture failed to modify test input' }");
            script.AppendLine("Require-OracleRejection 'MSBuild target changed tracked test'");
            script.AppendLine("[IO.File]::WriteAllText($testSource, 'TAMPERED TEST')");
            script.AppendLine("Require-OracleRejection 'direct tracked test mutation'");
            script.AppendLine("[IO.File]::WriteAllText($testSource, 'TRUSTED TEST')");
            script.AppendLine("$newTest = Join-Path $root 'tests/AuditRegression.New.cs'");
            script.AppendLine("[IO.File]::WriteAllText($newTest, 'NEW UNDECLARED TEST')");
            script.AppendLine("Require-OracleRejection 'untracked test creation'");
            script.AppendLine("[IO.File]::Delete($newTest)");
            script.AppendLine("$parser = Join-Path $root 'tools/apply-audit-repairs.py'");
            script.AppendLine("[IO.File]::WriteAllText($parser, 'TAMPERED PARSER')");
            script.AppendLine("Require-OracleRejection 'guard parser mutation'");
            script.AppendLine("[IO.File]::WriteAllText($parser, 'TRUSTED PARSER')");
            script.AppendLine("$workflow = Join-Path $root '.github/workflows/audit-repair.yml'");
            script.AppendLine("[IO.File]::WriteAllText($workflow, 'TAMPERED WORKFLOW')");
            script.AppendLine("Require-OracleRejection 'workflow mutation'");
            script.AppendLine("[IO.File]::WriteAllText($workflow, 'TRUSTED WORKFLOW')");
            script.AppendLine("$resource = Join-Path $root 'youtube-dl-gui/Resources/youtube-dl-gui-updater.exe'");
            script.AppendLine("[IO.File]::WriteAllText($resource, 'REPLACED UPDATER')");
            script.AppendLine("Require-OracleRejection 'undeclared embedded updater mutation'");
            script.AppendLine("[IO.File]::WriteAllText($resource, 'TRUSTED UPDATER')");
            script.AppendLine("$output = Join-Path $root 'bin/Debug'");
            script.AppendLine("[IO.Directory]::CreateDirectory($output) | Out-Null");
            script.AppendLine("[IO.File]::WriteAllText((Join-Path $output 'app.exe'), 'GENERATED OUTPUT')");
            script.AppendLine("Assert-RepairInputSnapshot -RepositoryRoot $root -Baseline $base -Phase 'normal build output'");
            script.AppendLine("Write-Output 'IMMUTABLE_REPAIR_INPUTS_VALIDATED'");
            string fixture = Path.Combine(root, "check-immutable.ps1");
            File.WriteAllText(fixture, script.ToString(), new UTF8Encoding(false));
            int exit;
            string result = GuardRunCli("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                fixture + "\"", root, out exit);
            Require(exit == 0 && result.Contains("IMMUTABLE_REPAIR_INPUTS_VALIDATED"),
                "Build-time oracle mutation detection failed its positive/negative runtime fixture: " + result);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void GuardedRepairCleansOnlyValidatedChunkedPayloads() {
        string sourceRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string module = Path.Combine(sourceRoot, "tools", "RepairRequestLifecycle.ps1");
        string root = Path.Combine(Path.GetTempPath(), "audit-cleanup-" + Guid.NewGuid().ToString("N"));
        string parts = Path.Combine(root, ".audit-repairs.parts");
        Directory.CreateDirectory(parts);
        try {
            File.WriteAllText(Path.Combine(root, ".audit-repairs.json"), "wrapper", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(parts, "part-000.txt"), "first chunk", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(parts, "part-001.txt"), "second chunk", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(parts, "part-999.txt"), "unrelated tracked payload", new UTF8Encoding(false));
            GuardGit(root, "init -q");
            GuardGit(root, "config user.name fixture");
            GuardGit(root, "config user.email fixture@invalid.example");
            GuardGit(root, "add -A");
            GuardGit(root, "commit -q -m fixture");
            StringBuilder script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine(". '" + module.Replace("'", "''") + "'");
            script.AppendLine("try {");
            script.AppendLine("  Complete-GuardedRepairRequest -ValidatedFiles @('.audit-repairs.json', '../out-of-tree.txt')");
            script.AppendLine("  throw 'UNSAFE_CLEANUP_ACCEPTED'");
            script.AppendLine("} catch { if ($_.Exception.Message -notmatch 'refused an undeclared/non-request path') { throw } }");
            script.AppendLine("if (!(Test-Path -LiteralPath '.audit-repairs.json')) { throw 'Negative cleanup deleted request wrapper' }");
            script.AppendLine("Complete-GuardedRepairRequest -ValidatedFiles @('.audit-repairs.json', '.audit-repairs.parts/part-000.txt', '.audit-repairs.parts/part-001.txt')");
            script.AppendLine("foreach ($path in @('.audit-repairs.json', '.audit-repairs.parts/part-000.txt', '.audit-repairs.parts/part-001.txt')) {");
            script.AppendLine("  if (Test-Path -LiteralPath $path) { throw ('Retained declared request payload: ' + $path) }");
            script.AppendLine("}");
            script.AppendLine("if (!(Test-Path -LiteralPath '.audit-repairs.parts/part-999.txt')) { throw 'Cleanup deleted an unrelated chunk' }");
            script.AppendLine("$staged = @(git diff --cached --name-only | Sort-Object)");
            script.AppendLine("if ($LASTEXITCODE -ne 0 -or ($staged -join ',') -cne '.audit-repairs.json,.audit-repairs.parts/part-000.txt,.audit-repairs.parts/part-001.txt') {");
            script.AppendLine("  throw ('Incorrect staged deletion paths: ' + ($staged -join ','))");
            script.AppendLine("}");
            script.AppendLine("Write-Output 'CHUNKED_REPAIR_CLEANUP_PASS'");
            string fixture = Path.Combine(root, "check-closure.ps1");
            File.WriteAllText(fixture, script.ToString(), new UTF8Encoding(false));
            int exit;
            string result = GuardRunCli("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                fixture + "\"", root, out exit);
            Require(exit == 0 && result.Contains("CHUNKED_REPAIR_CLEANUP_PASS"),
                "Chunked request cleanup did not preserve exact, unrelated payloads: " + result);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

}

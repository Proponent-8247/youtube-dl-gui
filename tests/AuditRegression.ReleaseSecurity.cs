using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

internal static partial class AuditRegression {
    private sealed class ReleaseSecurityResult {
        internal int ExitCode;
        internal string Output;
    }

    private static ReleaseSecurityResult ReleaseRunPowerShell(string cwd, string script, string version) {
        ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
        psi.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\"";
        psi.WorkingDirectory = cwd;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.EnvironmentVariables["EXPECTED_VERSION_INPUT"] = version;
        psi.EnvironmentVariables["GITHUB_REF_NAME"] = "security/no-release-fallback";
        using (Process process = Process.Start(psi)) {
            if (!process.WaitForExit(25000)) {
                try { process.Kill(); } catch { }
                throw new Exception("Release-security subprocess timed out");
            }
            return new ReleaseSecurityResult {
                ExitCode = process.ExitCode,
                Output = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd()
            };
        }
    }

    private static string ReleaseActualStepBody(string workflow) {
        string text = workflow.Replace("\r\n", "\n");
        int begin = text.IndexOf("      - name: Build and verify release candidate", StringComparison.Ordinal);
        int end = text.IndexOf("      - name: Summarize candidate", begin >= 0 ? begin : 0, StringComparison.Ordinal);
        Require(begin >= 0 && end > begin, "Release workflow step boundaries unavailable");
        string part = text.Substring(begin, end - begin);
        Require(part.Contains("EXPECTED_VERSION_INPUT: "), "Expected version lacks env data boundary");
        int run = part.IndexOf("        run: |\n", StringComparison.Ordinal);
        Require(run >= 0, "Release step PowerShell source missing");
        List<string> body = new List<string>();
        foreach (string line in part.Substring(run + "        run: |\n".Length).Split('\n')) {
            if (line.Length == 0) continue;
            Require(line.StartsWith("          ", StringComparison.Ordinal),
                "Unexpected indentation in release step code");
            body.Add(line.Substring(10));
        }
        Require(body.Count > 2, "Release step has no verifier");
        return string.Join(Environment.NewLine, body);
    }

    private static void ReleaseVersionInjectionCannotRunPowerShell() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release-candidate.yml"));
        string actualStep = ReleaseActualStepBody(workflow);
        Require(actualStep.Contains("$Expected = $env:EXPECTED_VERSION_INPUT"),
            "Version input is not read as environment data");
        Require(actualStep.Contains("./tools/Build-ReleaseCandidate.ps1"), "No actual release builder invocation");
        string work = Path.Combine(Path.GetTempPath(), "audit-release-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(work, "tools"));
        try {
            string step = Path.Combine(work, "actual-step.ps1");
            File.WriteAllText(step, actualStep, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(work, "tools", "Build-ReleaseCandidate.ps1"),
                "param([string]$ExpectedVersion, [string]$EvidenceDirectory)\r\n" +
                "[IO.File]::WriteAllText((Join-Path (Split-Path $PSScriptRoot -Parent) 'observed.txt'), $ExpectedVersion)\r\n" +
                "exit 0\r\n", new UTF8Encoding(false));
            string[] inputs = {
                "3.3.0-2",
                "'; Set-Content injected.marker -Value 'unwanted'; #",
                "\r\nSet-Content injected.marker -Value 'unwanted'; #",
                "$(Set-Content injected.marker -Value 'unwanted')"
            };
            foreach (string value in inputs) {
                string observed = Path.Combine(work, "observed.txt");
                if (File.Exists(observed)) File.Delete(observed);
                ReleaseSecurityResult result = ReleaseRunPowerShell(work, step, value);
                Require(result.ExitCode == 0 && File.Exists(observed),
                    "Exact workflow step did not deliver version data to its builder: " + result.Output);
                Equal(value, File.ReadAllText(observed));
                Require(!File.Exists(Path.Combine(work, "injected.marker")),
                    "A workflow_dispatch version executed arbitrary PowerShell");
            }

            // Invoke the genuine verifier with a malicious-looking value. It
            // must reject the version before any build or packaging can start.
            string real = Path.Combine(root, "tools", "Build-ReleaseCandidate.ps1").Replace("'", "''");
            string invoke = Path.Combine(work, "invoke-real.ps1");
            File.WriteAllText(invoke,
                "& '" + real + "' -ExpectedVersion $env:EXPECTED_VERSION_INPUT -EvidenceDirectory '" +
                Path.Combine(work, "reject-evidence").Replace("'", "''") + "'\r\n", new UTF8Encoding(false));
            ReleaseSecurityResult bad = ReleaseRunPowerShell(work, invoke, inputs[1]);
            Require(bad.ExitCode != 0 && bad.Output.IndexOf("Release version mismatch",
                StringComparison.OrdinalIgnoreCase) >= 0,
                "The real release verifier did not reject a malformed version early: " + bad.Output);
            Require(!File.Exists(Path.Combine(work, "injected.marker")),
                "The real release verifier executed version data");
        }
        finally { try { Directory.Delete(work, true); } catch { } }
    }

    private static int ReleaseAncestryExit(string root, string parent, string child) {
        ProcessStartInfo psi = new ProcessStartInfo("git.exe");
        psi.Arguments = "merge-base --is-ancestor " + parent + " " + child;
        psi.WorkingDirectory = root;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        using (Process process = Process.Start(psi)) {
            if (!process.WaitForExit(15000)) {
                try { process.Kill(); } catch { }
                throw new Exception("Release ancestry subprocess timed out");
            }
            return process.ExitCode;
        }
    }

    private static void ReleaseCandidateRejectsOldReviewedHistory() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string script = File.ReadAllText(Path.Combine(root, "tools", "Build-ReleaseCandidate.ps1"));
        const string reviewed = "7e369eb4afa6477e81f3e185ce9754bfd91982a6";
        const string oldPolicy = "7283444431e1243d83b86bf54838d22e3048cae9";
        const string oldCandidate = "49a290d07b220e4a9cd98d44c6db7f45a5bbbc98";
        Require(script.Contains("$AuditedBase = '" + reviewed + "'"),
            "Builder does not use the immutable reviewed audit source");
        Require(script.Contains("git merge-base --is-ancestor $AuditedBase HEAD"),
            "Builder has no required audited-source ancestry check");
        Require(script.Contains("audited_base = $AuditedBase"),
            "Release manifest lost its required audited-source provenance");
        Equal(0, ReleaseAncestryExit(root, reviewed, "HEAD"));
        Equal(0, ReleaseAncestryExit(root, oldPolicy, oldCandidate));
        Equal(1, ReleaseAncestryExit(root, reviewed, oldCandidate));
    }
    private static void AuditVerificationUsesImmutableReleaseBase() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "audit-verify.yml"));
        const string reviewed = "49a290d07b220e4a9cd98d44c6db7f45a5bbbc98";
        Require(workflow.Contains("$ReviewedBase = '" + reviewed + "'"),
            "Audit verification lacks the audited release-base immutable SHA");
        Require(workflow.Contains("git merge-base --is-ancestor $ReviewedBase HEAD"),
            "Audit verification does not verify the candidate descends from the reviewed release base");
        foreach (string evidence in new[] {
            "reviewed-base-commit.txt", "reviewed-base-source.zip",
            "reviewed-base-merge-base.txt", "reviewed-base-history.txt",
            "reviewed-base-diff-stat.txt", "reviewed-base-diff-files.txt",
            "reviewed-base-source.diff", "reviewed-base-sha256.txt"
        }) {
            Require(workflow.Contains(evidence), "Audited base provenance evidence missing: " + evidence);
        }
        Require(workflow.IndexOf("git log --format='%H %s' b734c05944eeffccfde3208cd1ea45ef2ae401f9", StringComparison.Ordinal) < 0,
            "Audit verification still uses an unrelated historical comparison base");
        Equal(0, ReleaseAncestryExit(root, reviewed, "HEAD"));
    }

}

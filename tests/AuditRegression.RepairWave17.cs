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

    private static void ReleaseCandidateDispatchVersionIsData() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release-candidate.yml"));
        int start = workflow.IndexOf("      - name: Build and verify release candidate", StringComparison.Ordinal);
        int end = workflow.IndexOf("      - name: Summarize candidate", start < 0 ? 0 : start, StringComparison.Ordinal);
        Require(start >= 0 && end > start, "Release candidate build step is missing or renamed");
        string step = workflow.Substring(start, end - start);
        Require(step.Contains("EXPECTED_VERSION_INPUT: ${{ inputs.expected_version }}"),
            "Manual release version is not supplied through a data-only step environment variable");
        int run = step.IndexOf("        run: |", StringComparison.Ordinal);
        Require(run >= 0, "Release candidate build step lost its PowerShell runner");
        string script = step.Substring(run);
        Require(script.Contains("$Expected = $env:EXPECTED_VERSION_INPUT"),
            "Release candidate build step is not reading the version from the environment");
        Require(script.IndexOf("${{ inputs.", StringComparison.Ordinal) < 0,
            "Untrusted workflow_dispatch inputs are interpolated directly into PowerShell code");
    }

    private static void GuardedRepairHasNoAutomaticWritePublisher() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "audit-repair.yml"));
        string runner = File.ReadAllText(Path.Combine(root, "tools", "Run-AuditRepairBatch.ps1"));
        Require(workflow.Contains("contents: read"), "Repair verification lost read-only repository permissions");
        Require(workflow.Contains("persist-credentials: false"), "Repair checkout is persisting repository credentials");
        Require(workflow.IndexOf("contents: write", StringComparison.Ordinal) < 0,
            "An untrusted repair workflow still contains write-capable credentials");
        Require(workflow.IndexOf("  publish:", StringComparison.Ordinal) < 0,
            "A publisher still trusts an untrusted repair artifact to update a branch");
        Require(workflow.IndexOf("persist-credentials: true", StringComparison.Ordinal) < 0,
            "A repair workflow checkout can supply a write credential to untrusted code");
        Require(runner.IndexOf("git push ", StringComparison.OrdinalIgnoreCase) < 0,
            "Untrusted repair runner attempts direct publication");
        Require(runner.Contains("$GuardParser = Join-Path $PSScriptRoot 'apply-audit-repairs.py'") &&
                runner.IndexOf("python tools/apply-audit-repairs.py", StringComparison.OrdinalIgnoreCase) < 0,
            "Repair runner invokes a candidate-workspace parser instead of its own reviewed companion");
        Require(workflow.Contains("actions/upload-artifact@"),
            "Read-only verification no longer retains a reviewable artifact");
        Require(workflow.Contains("Prove repair job cannot authenticate repository writes") &&
                workflow.Contains("tests/AuditSecurityCredentialProbe.proj"),
            "Guarded repair lacks the malicious-MSBuild credential isolation acceptance probe");
        Require(workflow.Contains("GIT_TERMINAL_PROMPT: '0'") &&
                workflow.Contains("GCM_INTERACTIVE: Never"),
            "Guarded repair credential probe may prompt instead of demonstrating denied writes");
    }
    private static string RunGuardParserWithFixture(string pythonScript, string folder, string switchName) {
        ProcessStartInfo start = new ProcessStartInfo("python.exe");
        start.Arguments = "\"" + pythonScript + "\" " + switchName;
        start.WorkingDirectory = folder;
        start.CreateNoWindow = true;
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using (Process process = Process.Start(start)) {
            Require(process.WaitForExit(15000), "Repair parser fixture exceeded the execution timeout");
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            return process.ExitCode.ToString() + "\n" + output;
        }
    }

    private static void GuardedRepairCannotSelfWhitelistFlakyFailures() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string script = Path.Combine(root, "tools", "apply-audit-repairs.py");
        string runner = File.ReadAllText(Path.Combine(root, "tools", "Run-AuditRepairBatch.ps1"));
        int deny = runner.IndexOf("if ($allowedFlaky.Count -gt 0)", StringComparison.Ordinal);
        int baseline = runner.IndexOf("$previous = Build-And-Test '00-before'", StringComparison.Ordinal);
        Require(deny >= 0 && baseline > deny, "Flaky exemptions are not denied before the first untrusted baseline build");
        string fixture = Path.Combine(Path.GetTempPath(), "audit-flaky-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try {
            string plan = "{\"version\":2,\"base_sha\":\"" + new string('a', 40) + "\",\"expected_failures\":[\"FIXTURE.REVIEWED_FAILURE\"],\"remaining_failures\":[],\"repairs\":[{\"id\":\"TEST\",\"message\":\"fixture\",\"resolves\":[\"FIXTURE.REVIEWED_FAILURE\"],\"files\":[]}]}";
            string request = Path.Combine(fixture, ".audit-repairs.json");
            File.WriteAllText(request, plan, new UTF8Encoding(false));
            string valid = RunGuardParserWithFixture(script, fixture, "--runner-metadata");
            Require(valid.StartsWith("0\n", StringComparison.Ordinal), "A valid request without flaky exemptions was rejected: " + valid);
            string malicious = plan.Replace("\"expected_failures\":[\"FIXTURE.REVIEWED_FAILURE\"]", "\"expected_failures\":[\"FIXTURE.REVIEWED_FAILURE\"],\"allowed_flaky_failures\":[\"DOWNLOAD_HISTORY.ResetRequiresOwnedBoundArchive\"]");
            File.WriteAllText(request, malicious, new UTF8Encoding(false));
            string rejected = RunGuardParserWithFixture(script, fixture, "--runner-metadata");
            Require(!rejected.StartsWith("0\n", StringComparison.Ordinal) && rejected.Contains("flaky-test exemptions"),
                "Self-declared flaky failure was accepted by the repair parser: " + rejected);
            string preflight = RunGuardParserWithFixture(script, fixture, "--preflight");
            Require(!preflight.StartsWith("0\n", StringComparison.Ordinal) && preflight.Contains("flaky-test exemptions"),
                "Flaky request escaped the preflight security gate: " + preflight);
        }
        finally { try { Directory.Delete(fixture, true); } catch { } }
    }
    private static void GuardedRepairRejectsTestOracleEdits() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string parser = Path.Combine(root, "tools", "apply-audit-repairs.py");
        string folder = Path.Combine(Path.GetTempPath(), "audit-oracle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Controls"));
        try {
            File.WriteAllText(Path.Combine(folder, "Controls", "Fixture.cs"), "before", new UTF8Encoding(false));
            string wrapper = "{\"version\":2,\"base_sha\":\"" + new string('b', 40) + "\",\"expected_failures\":[\"FIXTURE.REVIEWED_FAILURE\"],\"repairs\":[{\"id\":\"ORACLE\",\"message\":\"fixture\",\"resolves\":[\"FIXTURE.REVIEWED_FAILURE\"],\"files\":[{\"path\":\"PATH\",\"edits\":[{\"old\":\"before\",\"new\":\"after\"}]}]}]}";
            string request = Path.Combine(folder, ".audit-repairs.json");
            File.WriteAllText(request, wrapper.Replace("PATH", "Controls/Fixture.cs"), new UTF8Encoding(false));
            string permitted = RunGuardParserWithFixture(parser, folder, "--preflight");
            Require(permitted.StartsWith("0\n", StringComparison.Ordinal), "Legitimate production edit failed preflight: " + permitted);
            File.WriteAllText(request, wrapper.Replace("PATH", "tests/AuditRegression.Fixture.cs"), new UTF8Encoding(false));
            string denied = RunGuardParserWithFixture(parser, folder, "--preflight");
            Require(!denied.StartsWith("0\n", StringComparison.Ordinal) && denied.Contains("Disallowed source path"),
                "Production repair plan was permitted to weaken the test oracle: " + denied);
            Require(File.ReadAllText(Path.Combine(folder, "Controls", "Fixture.cs")) == "before",
                "Repair preflight unexpectedly modified a production fixture");
        }
        finally { try { Directory.Delete(folder, true); } catch { } }
    }

    private static void GuardedRepairRequiresReviewedFailureResolution() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string parser = Path.Combine(root, "tools", "apply-audit-repairs.py");
        string runner = File.ReadAllText(Path.Combine(root, "tools", "Run-AuditRepairBatch.ps1"));
        Require(runner.Contains("$test -notin $previous.Failed"),
            "Guard runner accepts a resolved test that was never failing before the repair");
        string folder = Path.Combine(Path.GetTempPath(), "audit-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Controls"));
        try {
            File.WriteAllText(Path.Combine(folder, "Controls", "Fixture.cs"), "before", new UTF8Encoding(false));
            string template = "{\"version\":2,\"base_sha\":\"" + new string('c', 40) +
                "\",\"expected_failures\":[\"FIXTURE.ReviewedFailure\"],\"remaining_failures\":[],\"repairs\":[{\"id\":\"PROOF\"," +
                "\"message\":\"fixture\",\"resolves\":[\"REPLACE_ME\"],\"files\":[{\"path\":\"Controls/Fixture.cs\"," +
                "\"edits\":[{\"old\":\"before\",\"new\":\"after\"}]}]}]}";
            string request = Path.Combine(folder, ".audit-repairs.json");
            File.WriteAllText(request, template.Replace("REPLACE_ME", "FIXTURE.ReviewedFailure"), new UTF8Encoding(false));
            string accepted = RunGuardParserWithFixture(parser, folder, "--preflight");
            Require(accepted.StartsWith("0\n", StringComparison.Ordinal),
                "Legitimate reviewed-failure repair plan was rejected: " + accepted);
            File.WriteAllText(request, template.Replace("\"REPLACE_ME\"", ""), new UTF8Encoding(false));
            string empty = RunGuardParserWithFixture(parser, folder, "--preflight");
            Require(!empty.StartsWith("0\n", StringComparison.Ordinal) &&
                empty.Contains("at least one reviewed baseline failure"),
                "A repair with resolves:[] was accepted: " + empty);
            File.WriteAllText(request, template.Replace("REPLACE_ME", "FIXTURE.UnrelatedPassingTest"), new UTF8Encoding(false));
            string unrelated = RunGuardParserWithFixture(parser, folder, "--runner-metadata");
            Require(!unrelated.StartsWith("0\n", StringComparison.Ordinal) &&
                unrelated.Contains("not a reviewed baseline failure"),
                "A repair claimed a passing non-baseline regression: " + unrelated);
        }
        finally { try { Directory.Delete(folder, true); } catch { } }
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
        Test("SECURITY.ReleaseCandidateDispatchVersionIsData", ReleaseCandidateDispatchVersionIsData);
        Test("SECURITY.ReleaseVersionInjectionCannotRunPowerShell", ReleaseVersionInjectionCannotRunPowerShell);
        Test("SECURITY.ReleaseCandidateRejectsOldReviewedHistory", ReleaseCandidateRejectsOldReviewedHistory);
        Test("SECURITY.AuditVerificationUsesImmutableReleaseBase", AuditVerificationUsesImmutableReleaseBase);
        Test("SECURITY.ReviewedRegressionInventoryCannotShrink", ReviewedRegressionInventoryCannotShrink);
        Test("SECURITY.AuthenticodeTrustChecksWholeSignerChainRevocation", AuthenticodeTrustChecksWholeSignerChainRevocation);
        Test("SECURITY.ProviderDiscoveryUsesFullyInitializedPersistedType", ProviderDiscoveryUsesFullyInitializedPersistedType);
        Test("SECURITY.ArgumentReferenceMatchesRuntimeParser", ArgumentReferenceMatchesRuntimeParser);
        Test("RESOURCE.ExceptionDialogRollDisposesCryptographicGenerator", ExceptionDialogRollDisposesCryptographicGenerator);
        Test("LOCALIZATION.HiddenLogAppliesCurrentLanguageBeforeFirstDisplay", HiddenLogAppliesCurrentLanguageBeforeFirstDisplay);
        Test("RESOURCE.HarnessDeadlineTerminatesStalledProcess", HarnessDeadlineTerminatesStalledProcess);
        Test("RESOURCE.RawProviderSessionHistoryHasBoundedRecentWindow", RawProviderSessionHistoryHasBoundedRecentWindow);
        Test("STARTUP.AbandonedInstanceMutexIsRecoveredWithoutRecursiveAcquisition", AbandonedInstanceMutexIsRecoveredWithoutRecursiveAcquisition);
        Test("SECURITY.RedditUserscriptDistributionCannotSwitchToMutableUpstream", RedditUserscriptDistributionCannotSwitchToMutableUpstream);
        Test("RESOURCE.NativeArchiveRejectsOversizedLineBeforeAllocatingWholeEntry", NativeArchiveRejectsOversizedLineBeforeAllocatingWholeEntry);
        Test("SECURITY.ExternalCustomProtocolNeverSilentlyReplaysAmbientHeaders", ExternalCustomProtocolNeverSilentlyReplaysAmbientHeaders);
        Test("SECURITY.ProviderProxySettingCannotInjectArguments", ProviderProxySettingCannotInjectArguments);
        Test("SECURITY.ProviderSubtitleFormatCannotInjectArguments", ProviderSubtitleFormatCannotInjectArguments);
        Test("SECURITY.ProviderSelectionFieldsCannotInjectOptions", ProviderSelectionFieldsCannotInjectOptions);
        Test("SECURITY.UpdatesNeverTargetDiscoveredExternalTools", UpdatesNeverTargetDiscoveredExternalTools);
        Test("SECURITY.GuardedRepairRejectsSmuggledRequestCommit", GuardedRepairRejectsSmuggledRequestCommit);
        Test("SECURITY.GuardedRepairRequiresReviewedFailureResolution", GuardedRepairRequiresReviewedFailureResolution);
        Test("SECURITY.GuardedRepairCleansOnlyValidatedChunkedPayloads", GuardedRepairCleansOnlyValidatedChunkedPayloads);
        Test("SECURITY.GuardedRepairDetectsBuildTimeOracleMutation", GuardedRepairDetectsBuildTimeOracleMutation);
        Test("SECURITY.GuardedRepairHasNoAutomaticWritePublisher", GuardedRepairHasNoAutomaticWritePublisher);
        Test("SECURITY.GuardedRepairCannotSelfWhitelistFlakyFailures", GuardedRepairCannotSelfWhitelistFlakyFailures);
        Test("SECURITY.GuardedRepairRejectsTestOracleEdits", GuardedRepairRejectsTestOracleEdits);
        RunDownloadHistoryTests();
        RunV011Tests();
    }
}

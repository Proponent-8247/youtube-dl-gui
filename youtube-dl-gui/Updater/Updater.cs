#nullable enable
namespace youtube_dl_gui;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using murrty.controls;
using murrty.logging;
using murrty.updater;

internal static class ExecutableTrust {
    internal enum SignatureStatus {
        Unsigned,
        Valid,
        Invalid,
    }

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_IGNORE = 0;
    private const uint WTD_REVOCATION_CHECK_NONE = 0x00000010;
    private const int MAX_PATH = 260;

    private static readonly Guid GenericVerifyAction = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private static readonly Guid DriverActionVerify = new("F750E6C3-38EE-11D1-85E5-00C04FC295EE");

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct WinTrustFileInfo {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WinTrustData {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct CatalogInfo {
        public uint cbStruct;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string wszCatalogFile;
    }

    [System.Runtime.InteropServices.DllImport("wintrust.dll", ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, IntPtr pWVTData);

    [System.Runtime.InteropServices.DllImport("wintrust.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptCATAdminAcquireContext2(out IntPtr phCatAdmin, ref Guid pgSubsystem, string? pwszHashAlgorithm, IntPtr pStrongHashPolicy, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("wintrust.dll", SetLastError = true)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr hCatAdmin, IntPtr hFile, ref uint pcbHash, [System.Runtime.InteropServices.Out] byte[]? pbHash, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("wintrust.dll", SetLastError = true)]
    private static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr hCatAdmin, byte[] pbHash, uint cbHash, uint dwFlags, IntPtr phPrevCatInfo);

    [System.Runtime.InteropServices.DllImport("wintrust.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptCATCatalogInfoFromContext(IntPtr hCatInfo, ref CatalogInfo psCatInfo, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("wintrust.dll", SetLastError = true)]
    private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr hCatAdmin, IntPtr hCatInfo, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("wintrust.dll", SetLastError = true)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr hCatAdmin, uint dwFlags);

    private static bool VerifyEmbeddedSignature(string FilePath) {
        IntPtr PathPointer = IntPtr.Zero;
        IntPtr FileInfoPointer = IntPtr.Zero;
        IntPtr TrustDataPointer = IntPtr.Zero;
        try {
            PathPointer = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUni(FilePath);
            WinTrustFileInfo FileInfo = new() {
                cbStruct = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinTrustFileInfo)),
                pcwszFilePath = PathPointer,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };
            FileInfoPointer = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinTrustFileInfo)));
            System.Runtime.InteropServices.Marshal.StructureToPtr(FileInfo, FileInfoPointer, false);

            WinTrustData TrustData = new() {
                cbStruct = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinTrustData)),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = FileInfoPointer,
                dwStateAction = WTD_STATEACTION_IGNORE,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = WTD_REVOCATION_CHECK_NONE,
                dwUIContext = 0,
            };
            TrustDataPointer = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinTrustData)));
            System.Runtime.InteropServices.Marshal.StructureToPtr(TrustData, TrustDataPointer, false);
            Guid Action = GenericVerifyAction;
            return WinVerifyTrust(new IntPtr(-1), ref Action, TrustDataPointer) == 0;
        }
        finally {
            if (TrustDataPointer != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(TrustDataPointer);
            if (FileInfoPointer != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(FileInfoPointer);
            if (PathPointer != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(PathPointer);
        }
    }

    private static bool TryVerifyCatalog(string FilePath, string HashAlgorithm, out bool CatalogFound) {
        CatalogFound = false;
        IntPtr CatalogAdmin = IntPtr.Zero;
        IntPtr CatalogContext = IntPtr.Zero;
        try {
            Guid DriverAction = DriverActionVerify;
            if (!CryptCATAdminAcquireContext2(out CatalogAdmin, ref DriverAction, HashAlgorithm, IntPtr.Zero, 0)) {
                return false;
            }

            using FileStream Stream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            IntPtr FileHandle = Stream.SafeFileHandle.DangerousGetHandle();
            uint HashLength = 0;
            if (!CryptCATAdminCalcHashFromFileHandle2(CatalogAdmin, FileHandle, ref HashLength, null, 0) || HashLength == 0 || HashLength > 4096) {
                return false;
            }
            byte[] Hash = new byte[HashLength];
            if (!CryptCATAdminCalcHashFromFileHandle2(CatalogAdmin, FileHandle, ref HashLength, Hash, 0)) {
                return false;
            }

            CatalogContext = CryptCATAdminEnumCatalogFromHash(CatalogAdmin, Hash, HashLength, 0, IntPtr.Zero);
            if (CatalogContext == IntPtr.Zero) {
                return false;
            }
            CatalogFound = true;

            CatalogInfo Catalog = new() {
                cbStruct = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(CatalogInfo)),
                wszCatalogFile = string.Empty,
            };
            return CryptCATCatalogInfoFromContext(CatalogContext, ref Catalog, 0)
                && !Catalog.wszCatalogFile.IsNullEmptyWhitespace()
                && VerifyEmbeddedSignature(Catalog.wszCatalogFile);
        }
        finally {
            if (CatalogContext != IntPtr.Zero && CatalogAdmin != IntPtr.Zero) CryptCATAdminReleaseCatalogContext(CatalogAdmin, CatalogContext, 0);
            if (CatalogAdmin != IntPtr.Zero) CryptCATAdminReleaseContext(CatalogAdmin, 0);
        }
    }

    internal static SignatureStatus GetStatus(string FilePath) {
        if (FilePath.IsNullEmptyWhitespace() || !File.Exists(FilePath)) {
            return SignatureStatus.Invalid;
        }

        try {
            if (VerifyEmbeddedSignature(FilePath)) {
                return SignatureStatus.Valid;
            }

            bool CatalogFound = false;
            if (TryVerifyCatalog(FilePath, "SHA256", out bool Sha256CatalogFound)) {
                return SignatureStatus.Valid;
            }
            CatalogFound |= Sha256CatalogFound;
            if (TryVerifyCatalog(FilePath, "SHA1", out bool Sha1CatalogFound)) {
                return SignatureStatus.Valid;
            }
            CatalogFound |= Sha1CatalogFound;
            if (CatalogFound) {
                return SignatureStatus.Invalid;
            }

            try {
                using System.Security.Cryptography.X509Certificates.X509Certificate Certificate =
                    System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(FilePath);
                return SignatureStatus.Invalid;
            }
            catch (CryptographicException) {
                return SignatureStatus.Unsigned;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException or DllNotFoundException or EntryPointNotFoundException) {
            return SignatureStatus.Invalid;
        }
    }
}

internal static class Updater {
    private const int MaxRetries = 5;
    private const int RetryDelay = 1_000; // Time between retries.

    /// <summary>
    /// This is the known SHA-256 hash of the updater.
    /// </summary>
    private const string KnownUpdaterHash = GeneratedUpdaterHash.Value;

    /// <summary>
    /// This is the direct ffmpeg download link.
    /// </summary>
    private const string FfmpegDownloadLink = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private static int UpdateCheckerRunning;
    private static CancellationTokenSource UpdateToken = new();

    #region Properties
    internal static int ExpectedUpdaterProcessId { get; set; }
    internal static bool IsExpectedUpdaterProcess(int ProcessId) =>
        ProcessId > 0 && ProcessId == ExpectedUpdaterProcessId;
    internal static void ClearExpectedUpdaterProcess() => ExpectedUpdaterProcessId = 0;

    /// <summary>
    /// Represents the very last checked repository release.
    /// </summary>
    public static GithubData? LastChecked { get; private set; }
    /// <summary>
    /// Represents the last checked latest release from a repository.
    /// </summary>
    public static GithubData? LastCheckedLatestRelease { get; private set; }
    /// <summary>
    /// Represents the last checked any release from a repository (this can include pre-releases).
    /// </summary>
    public static GithubData? LastCheckedAllRelease { get; private set; }

    /// <summary>
    /// Represents the latest youtube-dl provider release.
    /// </summary>
    public static GithubData? LatestYoutubeDl { get; private set; }
    private static int LatestYoutubeDlType = -1;
    #endregion

    #region Major methods
    [MemberNotNullWhen(true, nameof(LastChecked)), MemberNotNullWhen(false, nameof(LastChecked))]
    public static async Task<bool?> CheckForUpdate(bool ForceCheck) {
        if (!Program.UpdaterEnabled) {
            Log.Write("Cannot check for updates: TLS 1.2+ is not in use.");
            Program.TryOpenWebUrl(GithubLinks.ApplicationReleasesUrl);
            return null;
        }

        if (Interlocked.CompareExchange(ref UpdateCheckerRunning, 1, 0) != 0) {
            return null;
        }

        bool IncludePreReleases = General.DownloadBetaVersions;
        try {
            if (ForceCheck || (IncludePreReleases ? LastCheckedAllRelease is null : LastCheckedLatestRelease is null)) {
                await RefreshRelease(IncludePreReleases);
            }

            LastChecked = IncludePreReleases ? LastCheckedAllRelease : LastCheckedLatestRelease;
            return LastChecked?.IsNewerVersion == true;
        }
        finally {
            Interlocked.Exchange(ref UpdateCheckerRunning, 0);
        }
    }
    public static bool IsSkipped() {
        if (LastChecked?.IsBetaVersion == true) {
            return LastChecked.Version == Initialization.SkippedBetaVersion;
        }
        else {
            return LastChecked?.Version == Initialization.SkippedVersion;
        }
    }
    public static void AbortUpdateCheck() {
        UpdateToken.Cancel();
        UpdateToken = new();
    }
    public async static void ShowUpdateForm(bool AllowSkip) {
        bool IncludePreReleases = General.DownloadBetaVersions;
        if (IncludePreReleases ? LastCheckedAllRelease is null : LastCheckedLatestRelease is null) {
            await RefreshRelease(IncludePreReleases);
            if (LastChecked?.IsNewerVersion != true) {
                return;
            }
        }

        LastChecked = IncludePreReleases ? LastCheckedAllRelease : LastCheckedLatestRelease;
        if (LastChecked is null) {
            return;
        }

        using frmUpdateAvailable UpdateDialog = new(LastChecked) {
            BlockSkip = !AllowSkip,
        };
        switch (UpdateDialog.ShowDialog()) {
            case DialogResult.Yes: {
                BeginUpdate();
            } break;

            case DialogResult.Ignore when AllowSkip: {
                Log.Write($"Ignoring update v{LastChecked.Version}");

                if (IncludePreReleases) {
                    if (LastCheckedAllRelease is not null) {
                        Initialization.SkippedBetaVersion = LastCheckedAllRelease.Version;
                    }
                }
                else if (LastCheckedLatestRelease is not null) {
                    Initialization.SkippedVersion = LastCheckedLatestRelease.Version;
                }
            } break;
        }
    }
    private static bool UpdaterFileMatchesKnownHash(string UpdaterPath) {
        try {
            return File.Exists(UpdaterPath) &&
                Program.CalculateSha256Hash(UpdaterPath).Equals(KnownUpdaterHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) {
            return false;
        }
        catch (UnauthorizedAccessException) {
            return false;
        }
        catch (CryptographicException) {
            return false;
        }
    }

    private static bool TryParseSha256Digest(string? Value, out string Digest) {
        Digest = string.Empty;
        if (Value.IsNullEmptyWhitespace()) {
            return false;
        }

        string Candidate = Value!.Trim();
        if (Candidate.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) {
            Candidate = Candidate[7..].Trim();
        }
        if (Candidate.Length != 64 || !Candidate.All(Uri.IsHexDigit)) {
            return false;
        }

        Digest = Candidate.ToLowerInvariant();
        return true;
    }

    private static bool TryParseSha256Checksum(string? Content, string FileName, out string Digest) {
        Digest = string.Empty;
        if (Content.IsNullEmptyWhitespace() || FileName.IsNullEmptyWhitespace()) {
            return false;
        }

        string[] Lines = Content!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < Lines.Length; i++) {
            string Line = Lines[i].Trim();
            if (Line.Length < 66) {
                continue;
            }

            int Separator = Line.IndexOfAny([' ', '\t']);
            if (Separator != 64 || !TryParseSha256Digest(Line[..Separator], out string Parsed)) {
                continue;
            }

            string Name = Line[(Separator + 1)..].TrimStart();
            if (Name.StartsWith("*", StringComparison.Ordinal)) {
                Name = Name[1..];
            }
            if (!Name.Equals(FileName, StringComparison.Ordinal)) {
                continue;
            }

            Digest = Parsed;
            return true;
        }
        return false;
    }

    private static bool FileMatchesSha256(string FilePath, string ExpectedHash) {
        if (!TryParseSha256Digest(ExpectedHash, out string Parsed) || !File.Exists(FilePath)) {
            return false;
        }
        try {
            return Program.CalculateSha256Hash(FilePath).Equals(Parsed, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException) {
            return false;
        }
    }

    private static bool VerifyDownloadedExecutable(string FilePath, string ExpectedHash) {
        if (!FileMatchesSha256(FilePath, ExpectedHash)) {
            return false;
        }
        return ExecutableTrust.GetStatus(FilePath) != ExecutableTrust.SignatureStatus.Invalid;
    }

    private static bool CanUseProviderSelfUpdater() => false;

    private static bool TryGetProviderAsset(GithubData Release, string FileName, out string? DownloadUrl, out string? Digest, out string? ChecksumUrl, out long Length) {
        DownloadUrl = null;
        Digest = null;
        ChecksumUrl = null;
        Length = 0;
        if (Release.Files is null || FileName.IsNullEmptyWhitespace()) return false;

        GithubAsset? Executable = null;
        GithubAsset? Checksums = null;
        for (int i = 0; i < Release.Files.Length; i++) {
            GithubAsset Asset = Release.Files[i];
            if (Asset.Name?.Equals(FileName, StringComparison.Ordinal) == true) Executable = Asset;
            else if (Asset.Name?.Equals("SHA2-256SUMS", StringComparison.Ordinal) == true) Checksums = Asset;
        }
        if (Executable is null || Executable.Value.DownloadUrl.IsNullEmptyWhitespace()) return false;

        DownloadUrl = Executable.Value.DownloadUrl;
        Length = Executable.Value.Length;
        if (TryParseSha256Digest(Executable.Value.Digest, out string ParsedDigest)) {
            Digest = ParsedDigest;
            return true;
        }
        if (Checksums is not null && !Checksums.Value.DownloadUrl.IsNullEmptyWhitespace()) {
            ChecksumUrl = Checksums.Value.DownloadUrl;
            return true;
        }
        DownloadUrl = null;
        Length = 0;
        return false;
    }

    private static bool CommitVerifiedFile(string StagedPath, string DestinationPath, Func<string, bool> Verify) {
        if (!Verify(StagedPath)) {
            try { if (File.Exists(StagedPath)) File.Delete(StagedPath); } catch { }
            return false;
        }

        string BackupPath = DestinationPath + ".verified." + Guid.NewGuid().ToString("N") + ".bck";
        bool MovedOld = false;
        try {
            if (File.Exists(DestinationPath)) {
                File.Move(DestinationPath, BackupPath);
                MovedOld = true;
            }
            try {
                File.Move(StagedPath, DestinationPath);
            }
            catch {
                if (MovedOld && !File.Exists(DestinationPath) && File.Exists(BackupPath)) File.Move(BackupPath, DestinationPath);
                throw;
            }
            try { if (File.Exists(BackupPath)) File.Delete(BackupPath); }
            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException) {
                Log.Write($"Could not remove verified replacement backup: {cleanupEx.Message}");
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Log.Write($"Could not commit verified replacement: {ex.Message}");
            try {
                if (!File.Exists(DestinationPath) && File.Exists(BackupPath)) File.Move(BackupPath, DestinationPath);
            } catch (Exception rollbackEx) { Log.Write($"Could not roll back verified replacement: {rollbackEx.Message}"); }
            return false;
        }
        finally { try { if (File.Exists(StagedPath)) File.Delete(StagedPath); } catch { } }
    }

    private static bool CommitVerifiedExecutable(string StagedPath, string DestinationPath, string ExpectedHash) =>
        CommitVerifiedFile(StagedPath, DestinationPath, Path => VerifyDownloadedExecutable(Path, ExpectedHash));

    private static void BeginUpdate() {
        ExpectedUpdaterProcessId = 0;
        if (LastChecked?.ExecutableHash.IsNullEmptyWhitespace() != false) {
            Log.MessageBox("The selected release does not include a valid executable SHA-256 hash. The update cannot continue.");
            return;
        }

        string UpdaterPath = Environment.CurrentDirectory + Path.DirectorySeparatorChar + "youtube-dl-gui-updater.exe";

        // Delete the file that already exists
        if (File.Exists(UpdaterPath)) {
            if (Program.CalculateSha256Hash(UpdaterPath) != KnownUpdaterHash.ToLowerInvariant()) {
                // Delete the old one & Write the one from resource.
                File.Delete(UpdaterPath);

                // Write it.
                File.WriteAllBytes(UpdaterPath, Properties.Resources.youtube_dl_gui_updater);
            }
        }
        else {
            // Write it.
            File.WriteAllBytes(UpdaterPath, Properties.Resources.youtube_dl_gui_updater);
        }

        // Sanity check the updater. A cryptographic mismatch is terminal; user consent cannot make untrusted bytes safe.
        if (!UpdaterFileMatchesKnownHash(UpdaterPath)) {
            Log.MessageBox("The updater failed its SHA-256 integrity check and will not be executed.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            try {
                File.Delete(UpdaterPath);
            }
            catch (Exception ex) {
                Log.Write($"Failed to remove the untrusted updater after its integrity check failed: {ex.Message}");
            }
            return;
        }

        using Process CurrentProcess = Process.GetCurrentProcess();
        int ProcessId = CurrentProcess.Id;
        using Process Updater = new() {
            StartInfo = new() {
                Arguments = $"-pid {ProcessId} -hwnd {Program.GetMessagesHandle()}",
                FileName = UpdaterPath,
                WorkingDirectory = Environment.CurrentDirectory
            }
        };
        Log.Write($"Using the pid {ProcessId} with hwnd {Program.GetMessagesHandle()}");
        if (Updater.Start()) {
            ExpectedUpdaterProcessId = Updater.Id;
        }
    }

    /// <summary>
    /// Checks for a update to youtube-dl, and forks.
    /// </summary>
    /// <param name="ForceCheck"></param>
    public static async Task<bool> CheckForYoutubeDlUpdate(bool ForceCheck = false) {
        int TypeIndex = Verification.GetYoutubeDlType();
        if (LatestYoutubeDl is null || LatestYoutubeDl.VersionTag is null || LatestYoutubeDlType != TypeIndex || ForceCheck) {
            bool CanRetry;

            do {
                try {
                    Log.Write("Manually checking for youtube-dl update...");
                    // Always get the latest ytdl version regardless of if the application exists.
                    await GetLatestYoutubeDl(TypeIndex);

                    // If the file does not exist, it needs to be re-downloaded.
                    if (!Verification.YoutubeDlAvailable || LatestYoutubeDl is null) {
                        return true;
                    }

                    // Set the is-latest flag in the git data for the ytdl tag.
                    LatestYoutubeDl.IsNewerVersion = Verification.YoutubeDlVersion != LatestYoutubeDl.VersionTag;

                    // Return the flag.
                    return LatestYoutubeDl.IsNewerVersion;
                }
                catch (ApiParsingException APEx) {
                    if (Log.ReportRetriableException(APEx) != DialogResult.Retry)
                        return false;

                    CanRetry = true;
                }
                catch (WebException WebEx) {
                    if (Log.ReportRetriableException(WebEx, GithubLinks.ApplicationDownloadUrl.Format(
                        GithubLinks.ProviderRepos[TypeIndex].User,
                        GithubLinks.ProviderRepos[TypeIndex].Repo,
                        GithubLinks.ProviderRepos[TypeIndex].FriendlyName,
                        LatestYoutubeDl?.VersionTag ?? "0")) != DialogResult.Retry) {
                        return false;
                    }

                    CanRetry = true;
                }
                catch (ThreadAbortException) {
                    return false;
                }
                catch (Exception ex) {
                    if (Log.ReportRetriableException(ex) != DialogResult.Retry) {
                        return false;
                    }

                    CanRetry = true;
                }
            } while (CanRetry);
        }

        Log.Write("Assuming the update check finished.");

        if (LatestYoutubeDl is null) {
            Log.ReportException(new InvalidOperationException("LatestYoutubeDl is still null!"));
            return false;
        }

        Log.Write($"Found youtube-dl version: {LatestYoutubeDl.VersionTag}");
        return LatestYoutubeDl.IsNewerVersion;
    }

    /// <summary>
    /// Updates the current youtube-dl provider to the latest version.
    /// </summary>
    /// <param name="Internal">Whether to use the youtube-dl providers' internal updater as opposed to youtube-dl-guis' updater.</param>
    /// <param name="Location">The location where the generic downloader should appear.</param>
    /// <returns><see langword="true"/> if the youtube-dl provider update has went through regardless of success; otherwise, <see langword="false"/>.</returns>
    public static bool UpdateYoutubeDl(bool Internal, System.Drawing.Point? Location = null) {
        if (Internal) {
            Log.Write("Provider self-update is disabled because the replacement executable cannot be independently verified before installation.");
            return CanUseProviderSelfUpdater();
        }
        else {
            int TypeIndex = Verification.GetYoutubeDlType();
            if (LatestYoutubeDl is null || LatestYoutubeDlType != TypeIndex) {
                Log.Write("Provider selection changed; check its release before downloading an update.");
                return false;
            }

            if (Verification.YoutubeDlAvailable && !LatestYoutubeDl.IsNewerVersion) {
                return false;
            }

            Log.Write($"Downloading youtube-dl version {LatestYoutubeDl.VersionTag}.");

            string ProviderFileName = GithubLinks.ProviderRepos[TypeIndex].FriendlyName + ".exe";
            if (!TryGetProviderAsset(LatestYoutubeDl, ProviderFileName, out string? DownloadUrl, out _, out _, out _) ||
                DownloadUrl.IsNullEmptyWhitespace() || LatestYoutubeDl.ExecutableHash.IsNullEmptyWhitespace()) {
                Log.Write("The provider release does not expose a verifiable executable asset.");
                return false;
            }
            string ExpectedHash = LatestYoutubeDl.ExecutableHash!;
            using frmGenericDownloadProgress Downloader = new(
                DownloadUrl!,
                Verification.YoutubeDlPath ?? Verification.GetExpectedYoutubeDlPath(),
                Location,
                TempPath => VerifyDownloadedExecutable(TempPath, ExpectedHash));
            if (Downloader.ShowDialog() != DialogResult.OK) {
                return false;
            }

            Verification.RefreshYoutubeDlLocation();
            LatestYoutubeDl.IsNewerVersion = false;
            return true;
        }
    }

    private static bool TryDeleteFfmpegArchive(string FilePath) {
        try {
            File.Delete(FilePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Log.Write($"Could not remove downloaded FFmpeg archive: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Updates ffmpeg.
    /// </summary>
    /// <returns><see langword="true"/> if it was updated; otherwise, <see langword="false"/>.</returns>
    public static async Task<bool> UpdateFfmpeg(System.Drawing.Point? Location) {
        Log.Write("Downloading the latest ffmpeg release.");
        string FfmpegZipPath = Environment.CurrentDirectory + "\\ffmpeg.zip";

        using frmGenericDownloadProgress Downloader = new(FfmpegDownloadLink, FfmpegZipPath, Location);
        if (Downloader.ShowDialog() != DialogResult.OK) {
            return false;
        }

        bool CanRetry = true;
        do {
            try {
                string FfmpegPath = Path.GetDirectoryName(Verification.FFmpegPath ?? Verification.GetExpectedFfmpegPath()) ??
                    Environment.CurrentDirectory;

                using ZipArchive archive = ZipFile.OpenRead(FfmpegZipPath);
                ZipArchiveEntry[] Files = archive.Entries
                    .Where(e => e.Name.ToLower() switch {
                        "ffmpeg.exe" or "ffprobe.exe" => true,
                        _ => false
                    })
                    .ToArray();

                bool HasFfmpeg = Files.Any(e => e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                bool HasFfprobe = Files.Any(e => e.Name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase));
                if (!HasFfmpeg || !HasFfprobe) {
                    return false;
                }

                ZipArchiveEntry FfmpegEntry = Files.First(e => e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                ZipArchiveEntry FfprobeEntry = Files.First(e => e.Name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase));

                string FfmpegOutputPath = Verification.FFmpegPath ?? Verification.GetExpectedFfmpegPath();
                string FfprobeOutputPath = Path.Combine(FfmpegPath, "ffprobe.exe");
                string FfmpegTempPath = FfmpegOutputPath + ".update";
                string FfprobeTempPath = FfprobeOutputPath + ".update";
                string FfmpegBackupPath = FfmpegOutputPath + ".bck";
                string FfprobeBackupPath = FfprobeOutputPath + ".bck";

                try {
                    if (!File.Exists(FfmpegOutputPath) && File.Exists(FfmpegBackupPath)) {
                        File.Move(FfmpegBackupPath, FfmpegOutputPath);
                    }
                    else if (File.Exists(FfmpegBackupPath)) {
                        File.Delete(FfmpegBackupPath);
                    }

                    if (!File.Exists(FfprobeOutputPath) && File.Exists(FfprobeBackupPath)) {
                        File.Move(FfprobeBackupPath, FfprobeOutputPath);
                    }
                    else if (File.Exists(FfprobeBackupPath)) {
                        File.Delete(FfprobeBackupPath);
                    }

                    if (File.Exists(FfmpegTempPath)) {
                        File.Delete(FfmpegTempPath);
                    }
                    if (File.Exists(FfprobeTempPath)) {
                        File.Delete(FfprobeTempPath);
                    }

                    await Task.Run(() => FfmpegEntry.ExtractToFile(FfmpegTempPath));
                    await Task.Run(() => FfprobeEntry.ExtractToFile(FfprobeTempPath));

                    bool FfmpegMovedOld = false;
                    bool FfprobeMovedOld = false;
                    bool FfmpegMovedNew = false;
                    bool FfprobeMovedNew = false;

                    try {
                        if (File.Exists(FfmpegOutputPath)) {
                            File.Move(FfmpegOutputPath, FfmpegBackupPath);
                            FfmpegMovedOld = true;
                        }
                        File.Move(FfmpegTempPath, FfmpegOutputPath);
                        FfmpegMovedNew = true;

                        if (File.Exists(FfprobeOutputPath)) {
                            File.Move(FfprobeOutputPath, FfprobeBackupPath);
                            FfprobeMovedOld = true;
                        }
                        File.Move(FfprobeTempPath, FfprobeOutputPath);
                        FfprobeMovedNew = true;
                    }
                    catch {
                        try {
                            if (FfprobeMovedNew && File.Exists(FfprobeOutputPath)) {
                                File.Delete(FfprobeOutputPath);
                            }
                            if (FfprobeMovedOld && File.Exists(FfprobeBackupPath)) {
                                File.Move(FfprobeBackupPath, FfprobeOutputPath);
                            }
                        }
                        catch (Exception rollbackEx) {
                            Log.Write($"Failed to roll back ffprobe after an update error: {rollbackEx.Message}");
                        }

                        try {
                            if (FfmpegMovedNew && File.Exists(FfmpegOutputPath)) {
                                File.Delete(FfmpegOutputPath);
                            }
                            if (FfmpegMovedOld && File.Exists(FfmpegBackupPath)) {
                                File.Move(FfmpegBackupPath, FfmpegOutputPath);
                            }
                        }
                        catch (Exception rollbackEx) {
                            Log.Write($"Failed to roll back ffmpeg after an update error: {rollbackEx.Message}");
                        }

                        throw;
                    }
                }
                finally {
                    try {
                        if (File.Exists(FfmpegTempPath)) {
                            File.Delete(FfmpegTempPath);
                        }
                        if (File.Exists(FfprobeTempPath)) {
                            File.Delete(FfprobeTempPath);
                        }
                    }
                    catch (Exception cleanupEx) {
                        Log.Write($"Failed to clean up temporary ffmpeg update files: {cleanupEx.Message}");
                    }
                }

                CanRetry = false;
            }
            catch (Exception ex) {
                if (Log.ReportRetriableException(ex) != DialogResult.Retry) {
                    CanRetry = false;
                    return false;
                }
            }
        } while (CanRetry);

        // The replacement is already complete; archive cleanup must not turn success into failure.
        _ = TryDeleteFfmpegArchive(FfmpegZipPath);
        Verification.RefreshFFmpegLocation();
        return true;
    }

    /// <summary>
    /// Retrieves the available youtube-dl-gui languages.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static async Task<GithubRepoContent[]> GetAvailableLanguages() {
        Log.Write("Enumerating languages available.");
        const string Url = "https://api.github.com/repos/murrty/youtube-dl-gui/contents/Languages";

        string? JSON = await GetJSON(Url);

        if (JSON.IsNullEmptyWhitespace()) {
            throw new NullReferenceException("Github api is null empty or whitespace.");
        }

        GithubRepoContent[] ParsedLanguages = JSON.JsonDeserialize<GithubRepoContent[]>()
            ?? throw new ApiParsingException("Could not deserialize language metadata.", Url);
        var AvailableLanguages = ParsedLanguages
            .Where(x => x.name != "English.ini")
            .ToArray();

        return AvailableLanguages.Length > 0 ? AvailableLanguages : throw new ArgumentOutOfRangeException(nameof(AvailableLanguages));
    }
    #endregion

    #region Supporting methods
    /// <summary>
    /// Gets a JSON string using an internal web-client.
    /// </summary>
    /// <param name="Url">The URL to download the string from.</param>
    /// <returns>A string from the URL.</returns>
    private static async Task<string?> GetJSON(string Url) {
        CancellationToken Token = UpdateToken.Token;
        string? Json = null;
        bool CanRetry;
        int Retries = 0;
        do {
            try {
                Json = await Program.HttpClient.DownloadStringTaskAsync(new Uri(Url), Token);
                CanRetry = false;
            }
            catch (Exception ex) {
                while (ex.InnerException is not null) {
                    ex = ex.InnerException;
                }

                if (ex is ThreadAbortException or TaskCanceledException or OperationCanceledException) {
                    throw;
                }

                if (Retries != MaxRetries && (ex is not HttpException hex || (int)hex.StatusCode > 499)) {
                    Log.Write("An exception occurred, retrying...");
                    await Task.Delay(RetryDelay, Token);
                    Retries++;
                    CanRetry = true;
                    continue;
                }

                switch (Log.ReportRetriableException(ex, $"URL: \"{Url}\"")) {
                    case DialogResult.Retry: {
                        CanRetry = true;
                    } break;
                    default: return null;
                }
            }
        } while (CanRetry);

        return Json;
    }

    /// <summary>
    /// Refreshes the release data within the application.
    /// </summary>
    private static async Task RefreshRelease(bool IncludePreReleases) {
        string ReleaseUrl = GithubLinks.GetApplicationReleaseMetadataUrl(IncludePreReleases);
        string? Json = await GetJSON(ReleaseUrl);

        if (Json.IsNullEmptyWhitespace()) {
            throw new InvalidOperationException("JSON downloaded was empty");
        }

        GithubData CurrentCheck;

        if (IncludePreReleases) {
            GithubData[] Releases = Json.JsonDeserialize<GithubData[]>()
                ?? throw new ApiParsingException("Could not deserialize release metadata.", ReleaseUrl);
            CurrentCheck = LastCheckedAllRelease = GithubData.GetNewestRelease(Releases);
        }
        else {
            CurrentCheck = Json.JsonDeserialize<GithubData>()
                ?? throw new ApiParsingException("Could not deserialize release metadata.", ReleaseUrl);
            LastCheckedLatestRelease = CurrentCheck;
        }

        LastChecked = CurrentCheck;
    }

    /// <summary>
    /// Gets the latest github version of the specified fork ID.
    /// </summary>
    /// <param name="GitID">The youtube-dl fork ID from <seealso cref="GithubLinks.GitLinks.Repos"/>, authored by <seealso cref="GithubLinks.GitLinks.Users"/></param>
    /// <returns>The string of the latest version of the specified fork ID.</returns>
    private static async Task GetLatestYoutubeDl(int GitID) {
        if (GitID < 0 || GitID + 1 > GithubLinks.ProviderRepos.Length) {
            throw new ArgumentOutOfRangeException(nameof(GitID), GitID, "The GitID is invalid, youtube-dl cannot be redownloaded.");
        }

        Log.Write("Retrieving Github release data for youtube-dl");

        string Url = GithubLinks.GithubLatestJson.Format(GithubLinks.ProviderRepos[GitID].User, GithubLinks.ProviderRepos[GitID].Repo);
        string Json = await GetJSON(Url)
            .ConfigureAwait(true) ?? throw new ApiParsingException("The retrieved xml returned null.", Url);

        GithubData CurrentRelease = Json.JsonDeserialize<GithubData>()
            ?? throw new ApiParsingException("Could not deserialize provider release metadata.", Url);

        string ProviderFileName = GithubLinks.ProviderRepos[GitID].FriendlyName + ".exe";
        if (!TryGetProviderAsset(CurrentRelease, ProviderFileName, out _, out string? ProviderDigest, out string? ChecksumUrl, out _)) {
            throw new ApiParsingException("The provider release does not expose an executable with authoritative SHA-256 metadata.", Url);
        }
        if (ProviderDigest is null) {
            if (ChecksumUrl.IsNullEmptyWhitespace()) throw new ApiParsingException("The provider release checksum URL is missing.", Url);
            string ChecksumText = await Program.HttpClient.DownloadStringTaskAsync(new Uri(ChecksumUrl!), UpdateToken.Token).ConfigureAwait(true);
            if (!TryParseSha256Checksum(ChecksumText, ProviderFileName, out ProviderDigest)) {
                throw new ApiParsingException("The provider release checksum manifest does not contain the expected executable.", ChecksumUrl!);
            }
        }
        CurrentRelease.ExecutableHash = ProviderDigest;

        if (LatestYoutubeDlType == GitID && LatestYoutubeDl is not null && LatestYoutubeDl.VersionTag == CurrentRelease.VersionTag &&
            string.Equals(LatestYoutubeDl.ExecutableHash, ProviderDigest, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        LatestYoutubeDl = CurrentRelease;
        LatestYoutubeDlType = GitID;
    }
    #endregion
}
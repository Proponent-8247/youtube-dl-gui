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
    #endregion

    #region Major methods
    [MemberNotNullWhen(true, nameof(LastChecked)), MemberNotNullWhen(false, nameof(LastChecked))]
    public static async Task<bool?> CheckForUpdate(bool ForceCheck) {
        if (!Program.UpdaterEnabled) {
            Log.Write("Cannot check for updates: TLS 1.2+ is not in use.");
            Process.Start("https://github.com/murrty/youtube-dl-gui/releases");
            return null;
        }

        if (Interlocked.CompareExchange(ref UpdateCheckerRunning, 1, 0) != 0) {
            return null;
        }

        try {
            if (ForceCheck || (General.DownloadBetaVersions ? LastCheckedAllRelease is null : LastCheckedLatestRelease is null)) {
                await RefreshRelease();
            }

            return General.DownloadBetaVersions ?
                LastCheckedAllRelease?.IsNewerVersion == true :
                LastCheckedLatestRelease?.IsNewerVersion == true;
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
        if (General.DownloadBetaVersions ? LastCheckedAllRelease is null : LastCheckedLatestRelease is null) {
            await RefreshRelease();
            if (LastChecked?.IsNewerVersion != true) {
                return;
            }
        }

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

                if (General.DownloadBetaVersions) {
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
    private static void BeginUpdate() {
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

        // Sanity check the updater.
        if (Program.CalculateSha256Hash(UpdaterPath) != KnownUpdaterHash.ToLowerInvariant() &&
        Log.MessageBox(Language.dlgUpdaterHashNoMatch, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) {
            File.Delete(UpdaterPath);
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
        Updater.Start();
    }

    /// <summary>
    /// Checks for a update to youtube-dl, and forks.
    /// </summary>
    /// <param name="ForceCheck"></param>
    public static async Task<bool> CheckForYoutubeDlUpdate(bool ForceCheck = false) {
        if (LatestYoutubeDl is null || LatestYoutubeDl.VersionTag is null || ForceCheck) {
            int TypeIndex = Verification.GetYoutubeDlType();
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
        if (LatestYoutubeDl is null) {
            return false;
        }

        if (Internal) {
            if (!Verification.YoutubeDlAvailable) {
                return false;
            }

            Log.Write("Using youtube-dls' internal updater to update the program.");

            using Process UpdateYoutubeDl = new() {
                StartInfo = new(Verification.YoutubeDlPath) {
                    Arguments = "-U",
                }
            };
            UpdateYoutubeDl.Start();
            return true;
        }
        else {
            if (Verification.YoutubeDlAvailable && !LatestYoutubeDl.IsNewerVersion) {
                return false;
            }

            Log.Write($"Downloading youtube-dl version {LatestYoutubeDl.VersionTag}.");
            int TypeIndex = Verification.GetYoutubeDlType();

            string DownloadUrl =
                GithubLinks.ApplicationDownloadUrl.Format(
                    GithubLinks.ProviderRepos[TypeIndex].User,
                    GithubLinks.ProviderRepos[TypeIndex].Repo,
                    GithubLinks.ProviderRepos[TypeIndex].FriendlyName,
                    LatestYoutubeDl.VersionTag ?? "0");

            using frmGenericDownloadProgress Downloader = new(DownloadUrl, Verification.YoutubeDlPath ?? Verification.GetExpectedYoutubeDlPath(), Location);
            if (Downloader.ShowDialog() != DialogResult.OK) {
                return false;
            }

            Verification.RefreshYoutubeDlLocation();
            LatestYoutubeDl.IsNewerVersion = false;
            return true;
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

        // Delete the zip file.
        File.Delete(FfmpegZipPath);
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
        string? Json = null;
        bool CanRetry;
        int Retries = 0;
        do {
            try {
                Json = await Program.HttpClient.DownloadStringTaskAsync(new Uri(Url), CancellationToken.None);
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
                    await Task.Delay(RetryDelay);
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
    private static async Task RefreshRelease() {
        string ReleaseUrl = (General.DownloadBetaVersions ? GithubLinks.GithubAllReleasesJson : GithubLinks.GithubLatestJson)
            .Format("murrty", Language.ApplicationName);
        string? Json = await GetJSON(ReleaseUrl);

        if (Json.IsNullEmptyWhitespace()) {
            throw new InvalidOperationException("JSON downloaded was empty");
        }

        GithubData CurrentCheck;

        if (General.DownloadBetaVersions) {
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

        if (LatestYoutubeDl is not null && LatestYoutubeDl.VersionTag == CurrentRelease.VersionTag) {
            return;
        }

        LatestYoutubeDl = CurrentRelease;
    }
    #endregion
}
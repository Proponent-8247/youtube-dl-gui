#nullable enable
namespace youtube_dl_gui;
using System.Diagnostics.CodeAnalysis;
using System.IO;
/// <summary>
///     Represents an object that contains information about a media download, with settings for the download.
/// </summary>
/// <remarks>
///     Initializes a new instance of <see cref="DownloadInfo"/> with information for downloading a media object.
/// </remarks>
/// <param name="URL">
///     The URL to download.
/// </param>
internal sealed class DownloadInfo(string URL) : MediaInfo(URL) {
    private string? _argsCensored;
    private ProviderAuthenticationConfig? AuthenticationConfig;

    /// <summary>
    /// The URL of the video to download.
    /// </summary>
    public string DownloadURL => base.Source;
    /// <summary>
    /// The arguments (censored) for display.
    /// These are 'one-and-done', when you get the arguments this instance will be set to <see langword="null"/>.
    /// </summary>
    public string? ArgumentsCensored {
        get {
            string? args = _argsCensored;
            _argsCensored = null;
            return args;
        }
        set => _argsCensored = value;
    }
    /// <summary>
    /// Custom arguments for youtube-dl.
    /// </summary>
    public string? CustomArguments { get; set; }
    /// <summary>
    /// The status of the current download
    /// </summary>
    public DownloadStatus Status { get; set; } = DownloadStatus.None;
    /// <summary>
    /// The file-name schema of the download.
    /// </summary>
    public string FileNameSchema { get; set; } = Downloads.fileNameSchema;
    /// <summary>
    /// Whether the only generated arguments through this application is the output folder. This should be <see langword="true"/> for ytarchive downloads or downloads that are specialized and only require the output folder to be generated for the arguments.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CustomArguments))]
    public bool MostlyCustomArguments { get; set; }

    /// <summary>
    /// The type of the download.
    /// </summary>
    public DownloadType Type { get; set; } = DownloadType.None;
    /// <summary>
    /// The quality of the video download.
    /// </summary>
    public VideoQualityType VideoQuality { get; set; } = VideoQualityType.none;
    /// <summary>
    /// The format of the video download.
    /// </summary>
    public VideoFormatType VideoFormat { get; set; } = VideoFormatType.none;
    /// <summary>
    /// The CBR quality of the audio download.
    /// </summary>
    public AudioCBRQualityType AudioCBRQuality { get; set; } = AudioCBRQualityType.none;
    /// <summary>
    /// The VBR quality of the audio download.
    /// </summary>
    public AudioVBRQualityType AudioVBRQuality { get; set; } = AudioVBRQualityType.none;
    /// <summary>
    /// the format of the audio download.
    /// </summary>
    public AudioFormatType AudioFormat { get; set; } = AudioFormatType.none;
    /// <summary>
    /// The playlist selection type.
    /// </summary>
    public PlaylistSelectionType PlaylistSelection { get; set; } = PlaylistSelectionType.None;

    /// <summary>
    /// Determines of the video should skip downloading the audio
    /// </summary>
    public bool SkipAudioForVideos { get; set; }
    /// <summary>
    /// Determines if the audio should be in VBR (Variable bit rate)
    /// </summary>
    public bool UseVBR { get; set; }
    /// <summary>
    /// Determines if the download is a part of a batch process.
    /// </summary>
    public bool BatchDownload { get; set; }
    /// <summary>
    /// The time of the batch download start.
    /// </summary>
    public string? BatchTime { get; set; }
    /// <summary>
    /// The authentication for this instance.
    /// </summary>
    public AuthenticationDetails? Authentication { get; set; }
    /// <summary>
    /// The arguments for playlist selection.
    /// </summary>
    public string? PlaylistSelectionArg { get; set; }
    /// <summary>
    /// The int index of the start of the playlist.
    /// </summary>
    public int PlaylistSelectionIndexStart { get; set; } = -1;
    /// <summary>
    /// The int index of the end of the playlist.
    /// </summary>
    public int PlaylistSelectionIndexEnd { get; set; } = -1;

    /// <summary>
    /// Generates the arguments for the download instance.
    /// </summary>
    /// <param name="Verbose">The ExtendedRichTextBox object to export verbose information to.</param>
    /// <returns><see langword="true"/> if the arguments generated successfully; otherwise, <see langword="false"/>.</returns>
    public override bool GenerateArguments(Action<string> Verbose) {
        Status = DownloadStatus.Preparing;
        DisposeAuthenticationConfig();

        if (DownloadURL.IsNullEmptyWhitespace()) {
            Verbose("The URL is null or empty. Please enter a URL to download.");
            Log.Write("Cannot continue download.");
            return false;
        }

        ArgumentList ArgumentsBuffer = [];
        ArgumentList PreviewArguments;

        #region youtube-dl path
        string DownloadProvider = Verification.GetYoutubeDlProvider(false);

        Verbose($"Using {DownloadProvider} as the download provider.");
        if (!Verification.YoutubeDlAvailable) {
            Verbose($"The download provider has not been found\r\nA rescan for {DownloadProvider} was called");
            Verification.RefreshYoutubeDlLocation();
            if (!Verification.YoutubeDlAvailable) {
                Verbose($"still couldnt find {DownloadProvider}.");
                Status = DownloadStatus.ProgramError;
                Log.Write($"{DownloadProvider} could not be found.");
                return false;
            }
        }
        Verbose($"{DownloadProvider} has been found and set");
        #endregion

        #region Output
        Verbose("Generating output directory structure");

        StringBuilder OutputDirectory = new($"\"{(
            Downloads.downloadPath.StartsWith("./") || Downloads.downloadPath.StartsWith(".\\") ?
                $"{Program.ProgramPath}\\{Downloads.downloadPath[2..]}" :
                Downloads.downloadPath)}");

        if (BatchDownload && Downloads.SeparateBatchDownloads) {
            OutputDirectory.Append("\\# Batch Downloads #");

            if (Downloads.AddDateToBatchDownloadFolders)
                OutputDirectory.Append('\\').Append(BatchTime);
        }

        if (Downloads.separateIntoWebsiteURL)
            OutputDirectory.Append('\\').Append(DownloadHelper.GetUrlBase(DownloadURL, MostlyCustomArguments));

        if (Downloads.separateDownloads && !MostlyCustomArguments) {
            switch (Type) {
                case DownloadType.Video:
                    OutputDirectory.Append("\\Video");
                    break;
                case DownloadType.Audio:
                    OutputDirectory.Append("\\Audio");
                    break;
                case DownloadType.Custom:
                    OutputDirectory.Append("\\Custom");
                    break;
                default:
                    Verbose("Unable to determine what download type to use.");
                    Status = DownloadStatus.ProgramError;
                    return false;
            }
        }
        if (FileNameSchema.IsNullEmptyWhitespace()) {
            Verbose("The file name schema is not properly set, falling back to the default one. Consider setting it in the settings, or making sure the schema list has a proper schema format on the main form.");
            OutputDirectory.Append("\\%(title)s-%(id)s.%(ext)s\"");
        }
        else {
            OutputDirectory.Append('\\').Append(FileNameSchema).Append('\"');
        }

        if (!MostlyCustomArguments) {
            ArgumentsBuffer.Add($"-o {OutputDirectory}");
        }

        Verbose("The output was generated and will be used");
        #endregion

        #region Quality & format
        switch (Type) {
            case DownloadType.Video: {
                if (SkipAudioForVideos) {
                    ArgumentsBuffer.Add(Formats.GetVideoQualityArgsNoSound(VideoQuality));
                }
                else {
                    ArgumentsBuffer.Add(Formats.GetVideoQualityArgs(VideoQuality));
                }

                bool SupportsRemux = Downloads.YtdlType == (int)GitID.YtDlp || Downloads.YtdlType == (int)GitID.YtDlpNightly;
                ArgumentsBuffer.Add(Formats.GetVideoRecodeInfo(VideoFormat, SupportsRemux));
            } break;
            case DownloadType.Audio: {
                if (AudioCBRQuality == AudioCBRQualityType.best || AudioVBRQuality == AudioVBRQualityType.q0) {
                    ArgumentsBuffer.Add("--extract-audio --audio-quality 0");
                }
                else {
                    if (UseVBR) {
                        ArgumentsBuffer.Add($"--extract-audio --audio-quality {(int)AudioVBRQuality}");
                    }
                    else {
                        ArgumentsBuffer.Add($"--extract-audio --audio-quality {Formats.GetAudioQuality(AudioCBRQuality)}");
                    }
                }

                if (AudioFormat == AudioFormatType.best) {
                    ArgumentsBuffer.Add("--audio-format best");
                }
                else {
                    ArgumentsBuffer.Add($"--extract-audio --audio-format {Formats.GetAudioFormat(AudioFormat)}");
                }
            } break;
            case DownloadType.Custom: {
                Verbose("Custom was requested, skipping quality + format");
                if (MostlyCustomArguments) {
                    ArgumentsBuffer = new($"{CustomArguments} -o {OutputDirectory}");
                    break;
                }

                if (CustomArguments.IsNullEmptyWhitespace()) {
                    Verbose("No custom arguments were provided.");
                    return false;
                }

                ArgumentsBuffer.Add(CustomArguments);
            } break;
            default: {
                Verbose("Expected a downloadtype (Quality + Format)");
                Status = DownloadStatus.ProgramError;
            } return false;
        }

        Verbose("The quality and format has been set");
        #endregion

        #region Arguments
        if (Type != DownloadType.Custom) {
            switch (PlaylistSelection) {
                case PlaylistSelectionType.PlaylistStartPlaylistEnd: // playlist-start and playlist-end
                    if (PlaylistSelectionIndexStart > 0) {
                        ArgumentsBuffer.Add($"--playlist-start {PlaylistSelectionIndexStart}");
                    }

                    if (PlaylistSelectionIndexEnd > 0) {
                        ArgumentsBuffer.Add($"--playlist-end {PlaylistSelectionIndexEnd}");
                    }
                    break;
                case PlaylistSelectionType.PlaylistItems: // playlist-items
                    ArgumentsBuffer.Add($"--playlist-items {PlaylistSelectionArg}");
                    break;
                case PlaylistSelectionType.DateBefore: // datebefore
                    ArgumentsBuffer.Add($"--datebefore {PlaylistSelectionArg}");
                    break;
                case PlaylistSelectionType.DateDuring: // date
                    ArgumentsBuffer.Add($"--date {PlaylistSelectionArg}");
                    break;
                case PlaylistSelectionType.DateAfter: // dateafter
                    ArgumentsBuffer.Add($"--dateafter {PlaylistSelectionArg}");
                    break;
            }

            if (!Verification.FfmpegAvailable) {
                Verification.RefreshFFmpegLocation();
            }

            if (Verification.FfmpegAvailable) {
                ArgumentsBuffer.Add($"--ffmpeg-location \"{Verification.FFmpegPath}\"");
                if (Downloads.PreferFFmpeg || (DownloadHelper.IsReddit(DownloadURL) && Downloads.fixReddit)) {
                    Verbose("ffmpeg will be used for HLS");
                    ArgumentsBuffer.Add("--hls-prefer-ffmpeg");
                }
            }
            else if (Downloads.PreferFFmpeg || (DownloadHelper.IsReddit(DownloadURL) && Downloads.fixReddit)) {
                Verbose("WARNING: Could not find ffmpeg, it will not be used, downloading may be affected");
            }

            if (Downloads.SaveSubtitles) {
                ArgumentsBuffer.Add("--all-subs");

                if (!Downloads.SubtitleFormat.IsNullEmptyWhitespace()) {
                    ArgumentsBuffer.Add($"--sub-format {Downloads.SubtitleFormat} ");
                }

                if (Downloads.EmbedSubtitles && Type == DownloadType.Video) {
                    ArgumentsBuffer.Add("--embed-subs");
                }
            }
            if (Downloads.SaveVideoInfo) {
                ArgumentsBuffer.Add("--write-info-json");
            }
            if (Downloads.SaveDescription) {
                ArgumentsBuffer.Add("--write-description");
            }
            if (Downloads.SaveAnnotations) {
                if (Downloads.YtdlType == (int)GitID.YoutubeDl || Downloads.YtdlType == (int)GitID.YoutubeDlNightly) {
                    ArgumentsBuffer.Add("--write-annotations");
                }
                else {
                    Verbose("Annotations are not supported by yt-dlp; skipping --write-annotations.");
                }
            }
            if (Downloads.SaveThumbnail) {
                // ArgumentsBuffer += "--write-all-thumbnails "; // Maybe?
                ArgumentsBuffer.Add("--write-thumbnail");
                if (Downloads.EmbedThumbnails) {
                    switch (Type) {
                        case DownloadType.Video:
                            if (VideoFormat == VideoFormatType.mp4) {
                                ArgumentsBuffer.Add("--embed-thumbnail");
                            }
                            else {
                                Verbose("!!!!!!!! WARNING !!!!!!!!\r\nCannot embed thumbnail to non-mp4 videos files");
                            }
                            break;
                        case DownloadType.Audio:
                            if (AudioFormat == AudioFormatType.m4a || AudioFormat == AudioFormatType.mp3) {
                                ArgumentsBuffer.Add("--embed-thumbnail");
                            }
                            else {
                                Verbose("!!!!!!!! WARNING !!!!!!!!\r\nCannot embed thumbnail to non-m4a/mp3 audio files");
                            }
                            break;
                    }
                }
            }
            if (Downloads.WriteMetadata) {
                ArgumentsBuffer.Add("--add-metadata");
            }

            if (Downloads.KeepOriginalFiles) {
                ArgumentsBuffer.Add("-k");
            }

            if (Downloads.LimitDownloads && Downloads.DownloadLimit > 0) {
                ArgumentsBuffer.Add($"--limit-rate {Downloads.DownloadLimit}" + Downloads.DownloadLimitType switch {
                    1 => "M",
                    2 => "G",
                    _ => "K"
                });
            }

            if (Downloads.RetryAttempts != 10 && Downloads.RetryAttempts >= 0) {
                ArgumentsBuffer.Add($"--retries {Downloads.RetryAttempts}");
            }

            if (Downloads.ForceIPv4) {
                ArgumentsBuffer.Add("--force-ipv4");
            }
            else if (Downloads.ForceIPv6) {
                ArgumentsBuffer.Add("--force-ipv6");
            }

            if (Downloads.UseProxy && Downloads.ProxyType > -1 && Downloads.ProxyType < DownloadHelper.ProxyProtocols.Length && !string.IsNullOrEmpty(Downloads.ProxyIP) && !string.IsNullOrEmpty(Downloads.ProxyPort)) {
                ArgumentsBuffer.Add($"--proxy {DownloadHelper.ProxyProtocols[Downloads.ProxyType]}{Downloads.ProxyIP}:{Downloads.ProxyPort}/");
            }

            if (Downloads.SkipUnavailableFragments) {
                ArgumentsBuffer.Add("--skip-unavailable-fragments");
            }
            else if (Downloads.YtdlType == (int)GitID.YtDlp || Downloads.YtdlType == (int)GitID.YtDlpNightly) {
                ArgumentsBuffer.Add("--abort-on-unavailable-fragments");
            }
            else {
                ArgumentsBuffer.Add("--abort-on-unavailable-fragment");
            }

            if (Downloads.AbortOnError) {
                ArgumentsBuffer.Add("--abort-on-error");
            }
            else if (Downloads.YtdlType == (int)GitID.YtDlp || Downloads.YtdlType == (int)GitID.YtDlpNightly) {
                ArgumentsBuffer.Add("--no-abort-on-error");
            }
            else {
                ArgumentsBuffer.Add("--ignore-errors");
            }

            if (Downloads.FragmentThreads > 1
            && (Downloads.YtdlType == (int)GitID.YtDlp || Downloads.YtdlType == (int)GitID.YtDlpNightly)) {
                ArgumentsBuffer.Add("--concurrent-fragments " + Downloads.FragmentThreads);
            }

            if (!BatchDownload && PlaylistSelection == PlaylistSelectionType.None) {
                ArgumentsBuffer.Add("--no-playlist");
            }

            if (!CustomArguments.IsNullEmptyWhitespace()) {
                CustomArguments = CustomArguments.Trim();
                if (!CustomArguments.IsNullEmptyWhitespace()) {
                    ArgumentsBuffer.Add(CustomArguments);
                }
            }
        }
        #endregion

        #region Authentication
        // Provider secrets are transported through a private, per-operation config file.
        PreviewArguments = new(ArgumentsBuffer.ToString());
        if (!MostlyCustomArguments && Authentication is not null) {
            try {
                AuthenticationConfig = ProviderAuthenticationConfig.Create(Authentication);
                if (AuthenticationConfig is not null) {
                    ArgumentsBuffer.Add("--config-location " + ArgumentList.EscapeArgument(AuthenticationConfig.FilePath));
                    PreviewArguments.Add("--config-location ***");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException) {
                Verbose("Could not create a private authentication config for the download provider.");
                Log.Write($"Could not create provider authentication config: {ex.Message}");
                Status = DownloadStatus.ProgramError;
                return false;
            }
        }
        #endregion

        if (!MostlyCustomArguments) {
            string SourceArgument = "-- " + ArgumentList.EscapeArgument(DownloadURL);
            ArgumentsBuffer.Add(SourceArgument);
            PreviewArguments.Add(SourceArgument);
        }

        Verbose("Arguments have been generated");
        base.Arguments = ArgumentsBuffer.ToString();
        ArgumentsCensored = PreviewArguments.ToString();

        ArgumentsBuffer.Clear();
        PreviewArguments.Clear();
        return true;
    }

    internal void DisposeAuthenticationConfig() {
        AuthenticationConfig?.Dispose();
        AuthenticationConfig = null;
    }

    protected override void Dispose(bool disposing) {
        if (Disposed) return;
        DisposeAuthenticationConfig();
        base.Dispose(disposing);
    }
}

internal sealed class ProviderAuthenticationConfig : IDisposable {
    private readonly string DirectoryPath;
    private bool Disposed;

    public string FilePath { get; }

    private ProviderAuthenticationConfig(string FilePath, string DirectoryPath) {
        this.FilePath = FilePath;
        this.DirectoryPath = DirectoryPath;
    }

    public static ProviderAuthenticationConfig? Create(AuthenticationDetails Authentication) {
        if (Authentication is null) throw new ArgumentNullException(nameof(Authentication));

        List<string> Options = [];
        if (!Authentication.Username.IsNullEmptyWhitespace()) Options.Add("--username " + QuoteConfigValue(Authentication.Username));
        if (Authentication.Password?.Length > 0) Options.Add("--password " + QuoteConfigValue(Authentication.GetPassword()));
        if (!Authentication.TwoFactor.IsNullEmptyWhitespace()) Options.Add("--twofactor " + QuoteConfigValue(Authentication.TwoFactor));
        if (Authentication.MediaPassword?.Length > 0) Options.Add("--video-password " + QuoteConfigValue(Authentication.GetMediaPassword()));
        if (Authentication.NetRC) Options.Add("--netrc");
        if (!Authentication.CookiesFile.IsNullEmptyWhitespace()) Options.Add("--cookies " + QuoteConfigValue(Authentication.CookiesFile));
        if (!Authentication.CookiesFromBrowser.IsNullEmptyWhitespace()) Options.Add("--cookies-from-browser " + QuoteConfigValue(Authentication.CookiesFromBrowser));
        if (Options.Count == 0) return null;

        System.Security.Principal.SecurityIdentifier CurrentUser = System.Security.Principal.WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows user has no security identifier.");
        System.Security.Principal.SecurityIdentifier LocalSystem = new(System.Security.Principal.WellKnownSidType.LocalSystemSid, null);

        System.Security.AccessControl.DirectorySecurity DirectorySecurity = new();
        DirectorySecurity.SetAccessRuleProtection(true, false);
        System.Security.AccessControl.InheritanceFlags Inheritance = System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit;
        DirectorySecurity.AddAccessRule(new(CurrentUser, System.Security.AccessControl.FileSystemRights.FullControl, Inheritance, System.Security.AccessControl.PropagationFlags.None, System.Security.AccessControl.AccessControlType.Allow));
        DirectorySecurity.AddAccessRule(new(LocalSystem, System.Security.AccessControl.FileSystemRights.FullControl, Inheritance, System.Security.AccessControl.PropagationFlags.None, System.Security.AccessControl.AccessControlType.Allow));

        string DirectoryPath = Path.Combine(Path.GetTempPath(), "youtube-dl-gui-auth-" + Guid.NewGuid().ToString("N"));
        string FilePath = Path.Combine(DirectoryPath, "provider.conf");
        try {
            Directory.CreateDirectory(DirectoryPath, DirectorySecurity);
            File.WriteAllText(FilePath, string.Join(Environment.NewLine, Options), new UTF8Encoding(false));
            System.Security.AccessControl.FileSecurity FileSecurity = new();
            FileSecurity.SetAccessRuleProtection(true, false);
            FileSecurity.AddAccessRule(new(CurrentUser, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
            FileSecurity.AddAccessRule(new(LocalSystem, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
            File.SetAccessControl(FilePath, FileSecurity);
            return new ProviderAuthenticationConfig(FilePath, DirectoryPath);
        }
        catch {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
            try { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); } catch { }
            throw;
        }
    }

    private static string QuoteConfigValue(string Value) => "'" + Value.Replace("'", "'\"'\"'") + "'";

    public void Dispose() {
        if (Disposed) return;
        Disposed = true;
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write($"Could not remove temporary provider authentication config: {ex.Message}"); }
        try { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write($"Could not remove temporary provider authentication directory: {ex.Message}"); }
    }
}
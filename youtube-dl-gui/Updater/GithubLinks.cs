#nullable enable
namespace youtube_dl_gui;

internal static class GithubLinks {
    /// <summary>
    /// A URL to a Github Repository.
    /// Format with the following args:
    /// <para/>
    /// 0 -> Username
    /// <para/>
    /// 1 -> Repository
    /// </summary>
    public const string GithubRepoUrl           = "https://github.com/{0}/{1}";

    /// <summary>
    /// An API URL to githubs' latest release. This is the "Latest" tagged release.
    /// Format with the following args:
    /// <para/>
    /// 0 -> Username
    /// <para/>
    /// 1 -> Repository
    /// </summary>
    public const string GithubLatestJson        = "https://api.github.com/repos/{0}/{1}/releases/latest";

    /// <summary>
    /// A api URL to githubs' newest repo release. This includes pre-releases.
    /// Format with the following args:
    /// <para/>
    /// 0 -> Username
    /// <para/>
    /// 1 -> Repository
    /// </summary>
    public const string GithubAllReleasesJson   = "https://api.github.com/repos/{0}/{1}/releases";

    public const string ApplicationUpdateUser = "Proponent-8247";
    public const string ApplicationUpdateRepo = "youtube-dl-gui";
    public const string ApplicationRepositoryUrl = "https://github.com/Proponent-8247/youtube-dl-gui";
    public const string ApplicationReleasesUrl = ApplicationRepositoryUrl + "/releases";
    public const string ApplicationIssuesUrl = ApplicationRepositoryUrl + "/issues";
    public const string ApplicationProtocolDocumentationUrl = ApplicationRepositoryUrl + "/blob/master/ARGUMENTS.md#protocol-support";
    public const string ApplicationLanguagesApiUrl = "https://api.github.com/repos/Proponent-8247/youtube-dl-gui/contents/Languages";

    // Always enumerate releases for application update checks. Unlike /releases/latest,
    // the collection endpoint has a valid empty/prerelease-only representation, which
    // lets the client handle a new fork before its first stable release exists.
    public static string GetApplicationReleaseMetadataUrl(bool IncludePreReleases) =>
        GithubAllReleasesJson.Format(ApplicationUpdateUser, ApplicationUpdateRepo);

    /// <summary>
    /// A download URL to a piece of github content.
    /// Format with the following args:
    /// <para/>
    /// 0 -> Username
    /// <para/>
    /// 1 -> Repository
    /// <para/>
    /// 2 -> File name
    /// <para/>
    /// 3 -> Release tag
    /// </summary>
    public const string ApplicationDownloadUrl  = "https://github.com/{0}/{1}/releases/download/{3}/{2}.exe";

    /// <summary>
    /// An array of youtube-dl provider repositories supported.
    /// <para/>
    /// This is a (<see langword="string"/> User, <see langword="string"/> Repo, <see langword="string"/> FriendlyName) tuple array.
    /// <para/>
    /// FriendlyName is the expected file name that is associated with the release for the repo, not the local file name.
    /// </summary>
    public static (string User, string Repo, string FriendlyName)[] ProviderRepos { get; } = [
        (
            "yt-dlp",
            "yt-dlp",
            "yt-dlp"
        ),

        (
            "yt-dlp",
            "yt-dlp-nightly-builds",
            "yt-dlp"
        ),

        (
            "ytdl-org",
            "youtube-dl",
            "youtube-dl"
        ),

        (
            "ytdl-patched",
            "youtube-dl",
            "youtube-dl"
        ),
    ];
}
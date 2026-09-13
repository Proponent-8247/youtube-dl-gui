#nullable enable
namespace youtube_dl_gui;
internal static class Downloads {
    private const string ConfigName = "Downloads";

    internal static string DefaultDownloadPath =>
        System.IO.Path.Combine(NativeMethods.GetDownloadsFolderPath(), "youtube-dl");

    static Downloads() {
        Log.Write("Loading Download config.");

        fdownloadPath =
            IniProvider.Read(downloadPath, DefaultDownloadPath, ConfigName);

        fseparateDownloads =
            IniProvider.Read(separateDownloads, true, ConfigName);

        fSaveFormatQuality =
            IniProvider.Read(SaveFormatQuality, true, ConfigName);

        fdeleteYtdlOnClose =
            IniProvider.Read(deleteYtdlOnClose, false, ConfigName);

        fuseYtdlUpdater =
            IniProvider.Read(useYtdlUpdater, false, ConfigName);

        ffileNameSchema =
            IniProvider.Read(fileNameSchema, "%(title)s-%(id)s.%(ext)s", ConfigName);

        ffixReddit =
            IniProvider.Read(fixReddit, true, ConfigName);

        fseparateIntoWebsiteURL =
            IniProvider.Read(separateIntoWebsiteURL, true, ConfigName);

        fSaveSubtitles =
            IniProvider.Read(SaveSubtitles, false, ConfigName);

        fsubtitlesLanguages =
            IniProvider.Read(subtitlesLanguages, "en", ConfigName);

        fCloseDownloaderAfterFinish =
            IniProvider.Read(CloseDownloaderAfterFinish, true, ConfigName);

        fCloseExtendedDownloaderAfterFinish =
            IniProvider.Read(CloseExtendedDownloaderAfterFinish, false, ConfigName);

        fUseProxy =
            IniProvider.Read(UseProxy, false, ConfigName);

        fProxyType =
            IniProvider.Read(ProxyType, -1, ConfigName);

        fProxyIP =
            IniProvider.Read(ProxyIP, string.Empty, ConfigName);

        fProxyPort =
            IniProvider.Read(ProxyPort, string.Empty, ConfigName);

        fSaveThumbnail =
            IniProvider.Read(SaveThumbnail, false, ConfigName);

        fSaveDescription =
            IniProvider.Read(SaveDescription, false, ConfigName);

        fSaveVideoInfo =
            IniProvider.Read(SaveVideoInfo, false, ConfigName);

        fSaveAnnotations =
            IniProvider.Read(SaveAnnotations, false, ConfigName);

        fSubtitleFormat =
            IniProvider.Read(SubtitleFormat, string.Empty, ConfigName);

        fDownloadLimit =
            IniProvider.Read(DownloadLimit, 0, ConfigName);

        fRetryAttempts =
            IniProvider.Read(RetryAttempts, 10, ConfigName);

        fDownloadLimitType =
            IniProvider.Read(DownloadLimitType, 1, ConfigName);

        fForceIPv4 =
            IniProvider.Read(ForceIPv4, false, ConfigName);

        fForceIPv6 =
            IniProvider.Read(ForceIPv6, false, ConfigName);

        fLimitDownloads =
            IniProvider.Read(LimitDownloads, false, ConfigName);

        fEmbedSubtitles =
            IniProvider.Read(EmbedSubtitles, false, ConfigName);

        fEmbedThumbnails =
            IniProvider.Read(EmbedThumbnails, false, ConfigName);

        fVideoDownloadSound =
            IniProvider.Read(VideoDownloadSound, true, ConfigName);

        fAudioDownloadAsVBR =
            IniProvider.Read(AudioDownloadAsVBR, false, ConfigName);

        fKeepOriginalFiles =
            IniProvider.Read(KeepOriginalFiles, false, ConfigName);

        fWriteMetadata =
            IniProvider.Read(WriteMetadata, false, ConfigName);

        fSkipBatchTip =
            IniProvider.Read(SkipBatchTip, false, ConfigName);

        fAutomaticallyDownloadFromProtocol =
            IniProvider.Read(AutomaticallyDownloadFromProtocol, true, ConfigName);

        fPreferFFmpeg =
            IniProvider.Read(PreferFFmpeg, false, ConfigName);

        fSeparateBatchDownloads =
            IniProvider.Read(SeparateBatchDownloads, true, ConfigName);

        fAddDateToBatchDownloadFolders =
            IniProvider.Read(AddDateToBatchDownloadFolders, true, ConfigName);

        int readType = IniProvider.Read(YtdlType, 0, ConfigName);
        fYtdlType = Verification.GetYoutubeDlType(readType);

        fSubdomainFolderNames =
            IniProvider.Read(SubdomainFolderNames, false, ConfigName);

        fExtendedDownloaderPreferExtendedForm =
            IniProvider.Read(ExtendedDownloaderPreferExtendedForm, false, ConfigName);

        fExtendedDownloaderAutoDownloadThumbnail =
            IniProvider.Read(ExtendedDownloaderAutoDownloadThumbnail, false, ConfigName);

        fExtendedDownloaderIncludeCustomArguments =
            IniProvider.Read(ExtendedDownloaderIncludeCustomArguments, true, ConfigName);

        fSkipUnavailableFragments =
            IniProvider.Read(SkipUnavailableFragments, true, ConfigName);

        fAbortOnError =
            IniProvider.Read(AbortOnError, true, ConfigName);

        fFragmentThreads =
            Math.Max(IniProvider.Read(FragmentThreads, 1, ConfigName), 1);
    }

    public static string downloadPath {
        get => fdownloadPath;
        set {
            if (fdownloadPath != value) {
                IniProvider.Write(value, ConfigName, nameof(downloadPath));
                fdownloadPath = value;
            }
        }
    }
    private static string fdownloadPath;

    public static bool separateDownloads {
        get => fseparateDownloads;
        set {
            if (fseparateDownloads != value) {
                IniProvider.Write(value, ConfigName, nameof(separateDownloads));
                fseparateDownloads = value;
            }
        }
    }
    private static bool fseparateDownloads;

    public static bool SaveFormatQuality {
        get => fSaveFormatQuality;
        set {
            if (fSaveFormatQuality != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveFormatQuality));
                fSaveFormatQuality = value;
            }
        }
    }
    private static bool fSaveFormatQuality;

    public static bool deleteYtdlOnClose {
        get => fdeleteYtdlOnClose;
        set {
            if (fdeleteYtdlOnClose != value) {
                IniProvider.Write(value, ConfigName, nameof(deleteYtdlOnClose));
                fdeleteYtdlOnClose = value;
            }
        }
    }
    private static bool fdeleteYtdlOnClose;

    public static bool useYtdlUpdater {
        get => fuseYtdlUpdater;
        set {
            if (fuseYtdlUpdater != value) {
                IniProvider.Write(value, ConfigName, nameof(useYtdlUpdater));
                fuseYtdlUpdater = value;
            }
        }
    }
    private static bool fuseYtdlUpdater;

    public static string fileNameSchema {
        get => ffileNameSchema;
        set {
            if (ffileNameSchema != value) {
                IniProvider.Write(value, ConfigName, nameof(fileNameSchema));
                ffileNameSchema = value;
            }
        }
    }
    private static string ffileNameSchema;

    public static bool fixReddit {
        get => ffixReddit;
        set {
            if (ffixReddit != value) {
                IniProvider.Write(value, ConfigName, nameof(fixReddit));
                ffixReddit = value;
            }
        }
    }
    private static bool ffixReddit;

    public static bool separateIntoWebsiteURL {
        get => fseparateIntoWebsiteURL;
        set {
            if (fseparateIntoWebsiteURL != value) {
                IniProvider.Write(value, ConfigName, nameof(separateIntoWebsiteURL));
                fseparateIntoWebsiteURL = value;
            }
        }
    }
    private static bool fseparateIntoWebsiteURL;

    public static bool SaveSubtitles {
        get => fSaveSubtitles;
        set {
            if (fSaveSubtitles != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveSubtitles));
                fSaveSubtitles = value;
            }
        }
    }
    private static bool fSaveSubtitles;

    public static string subtitlesLanguages {
        get => fsubtitlesLanguages;
        set {
            if (fsubtitlesLanguages != value) {
                IniProvider.Write(value, ConfigName, nameof(subtitlesLanguages));
                fsubtitlesLanguages = value;
            }
        }
    }
    private static string fsubtitlesLanguages;

    public static bool CloseDownloaderAfterFinish {
        get => fCloseDownloaderAfterFinish;
        set {
            if (fCloseDownloaderAfterFinish != value) {
                IniProvider.Write(value, ConfigName, nameof(CloseDownloaderAfterFinish));
                fCloseDownloaderAfterFinish = value;
            }
        }
    }
    private static bool fCloseDownloaderAfterFinish;

    public static bool CloseExtendedDownloaderAfterFinish {
        get => fCloseExtendedDownloaderAfterFinish;
        set {
            if (fCloseExtendedDownloaderAfterFinish != value) {
                IniProvider.Write(value, ConfigName, nameof(CloseExtendedDownloaderAfterFinish));
                fCloseExtendedDownloaderAfterFinish = value;
            }
        }
    }
    private static bool fCloseExtendedDownloaderAfterFinish;

    public static bool UseProxy {
        get => fUseProxy;
        set {
            if (fUseProxy != value) {
                IniProvider.Write(value, ConfigName, nameof(UseProxy));
                fUseProxy = value;
            }
        }
    }
    private static bool fUseProxy;

    public static int ProxyType {
        get => fProxyType;
        set {
            if (fProxyType != value) {
                IniProvider.Write(value, ConfigName, nameof(ProxyType));
                fProxyType = value;
            }
        }
    }
    private static int fProxyType;

    public static string ProxyIP {
        get => fProxyIP;
        set {
            if (fProxyIP != value) {
                IniProvider.Write(value, ConfigName, nameof(ProxyIP));
                fProxyIP = value;
            }
        }
    }
    private static string fProxyIP;

    public static string ProxyPort {
        get => fProxyPort;
        set {
            if (fProxyPort != value) {
                IniProvider.Write(value, ConfigName, nameof(ProxyPort));
                fProxyPort = value;
            }
        }
    }
    private static string fProxyPort;

    public static bool SaveThumbnail {
        get => fSaveThumbnail;
        set {
            if (fSaveThumbnail != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveThumbnail));
                fSaveThumbnail = value;
            }
        }
    }
    private static bool fSaveThumbnail;

    public static bool SaveDescription {
        get => fSaveDescription;
        set {
            if (fSaveDescription != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveDescription));
                fSaveDescription = value;
            }
        }
    }
    private static bool fSaveDescription;

    public static bool SaveVideoInfo {
        get => fSaveVideoInfo;
        set {
            if (fSaveVideoInfo != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveVideoInfo));
                fSaveVideoInfo = value;
            }
        }
    }
    private static bool fSaveVideoInfo;

    public static bool SaveAnnotations {
        get => fSaveAnnotations;
        set {
            if (fSaveAnnotations != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveAnnotations));
                fSaveAnnotations = value;
            }
        }
    }
    private static bool fSaveAnnotations;

    public static string SubtitleFormat {
        get => fSubtitleFormat;
        set {
            if (fSubtitleFormat != value) {
                IniProvider.Write(value, ConfigName, nameof(SubtitleFormat));
                fSubtitleFormat = value;
            }
        }
    }
    private static string fSubtitleFormat;

    public static int DownloadLimit {
        get => fDownloadLimit;
        set {
            if (fDownloadLimit != value) {
                IniProvider.Write(value, ConfigName, nameof(DownloadLimit));
                fDownloadLimit = value;
            }
        }
    }
    private static int fDownloadLimit;

    public static int RetryAttempts {
        get => fRetryAttempts;
        set {
            if (fRetryAttempts != value) {
                IniProvider.Write(value, ConfigName, nameof(RetryAttempts));
                fRetryAttempts = value;
            }
        }
    }
    private static int fRetryAttempts;

    public static int DownloadLimitType {
        get => fDownloadLimitType;
        set {
            if (fDownloadLimitType != value) {
                IniProvider.Write(value, ConfigName, nameof(DownloadLimitType));
                fDownloadLimitType = value;
            }
        }
    }
    private static int fDownloadLimitType;

    public static bool ForceIPv4 {
        get => fForceIPv4;
        set {
            if (fForceIPv4 != value) {
                IniProvider.Write(value, ConfigName, nameof(ForceIPv4));
                fForceIPv4 = value;
            }
        }
    }
    private static bool fForceIPv4;

    public static bool ForceIPv6 {
        get => fForceIPv6;
        set {
            if (fForceIPv6 != value) {
                IniProvider.Write(value, ConfigName, nameof(ForceIPv6));
                fForceIPv6 = value;
            }
        }
    }
    private static bool fForceIPv6;

    public static bool LimitDownloads {
        get => fLimitDownloads;
        set {
            if (fLimitDownloads != value) {
                IniProvider.Write(value, ConfigName, nameof(LimitDownloads));
                fLimitDownloads = value;
            }
        }
    }
    private static bool fLimitDownloads;

    public static bool EmbedSubtitles {
        get => fEmbedSubtitles;
        set {
            if (fEmbedSubtitles != value) {
                IniProvider.Write(value, ConfigName, nameof(EmbedSubtitles));
                fEmbedSubtitles = value;
            }
        }
    }
    private static bool fEmbedSubtitles;

    public static bool EmbedThumbnails {
        get => fEmbedThumbnails;
        set {
            if (fEmbedThumbnails != value) {
                IniProvider.Write(value, ConfigName, nameof(EmbedThumbnails));
                fEmbedThumbnails = value;
            }
        }
    }
    private static bool fEmbedThumbnails;

    public static bool VideoDownloadSound {
        get => fVideoDownloadSound;
        set {
            if (fVideoDownloadSound != value) {
                IniProvider.Write(value, ConfigName, nameof(VideoDownloadSound));
                fVideoDownloadSound = value;
            }
        }
    }
    private static bool fVideoDownloadSound;

    public static bool AudioDownloadAsVBR {
        get => fAudioDownloadAsVBR;
        set {
            if (fAudioDownloadAsVBR != value) {
                IniProvider.Write(value, ConfigName, nameof(AudioDownloadAsVBR));
                fAudioDownloadAsVBR = value;
            }
        }
    }
    private static bool fAudioDownloadAsVBR;

    public static bool KeepOriginalFiles {
        get => fKeepOriginalFiles;
        set {
            if (fKeepOriginalFiles != value) {
                IniProvider.Write(value, ConfigName, nameof(KeepOriginalFiles));
                fKeepOriginalFiles = value;
            }
        }
    }
    private static bool fKeepOriginalFiles;

    public static bool WriteMetadata {
        get => fWriteMetadata;
        set {
            if (fWriteMetadata != value) {
                IniProvider.Write(value, ConfigName, nameof(WriteMetadata));
                fWriteMetadata = value;
            }
        }
    }
    private static bool fWriteMetadata;

    public static bool SkipBatchTip {
        get => fSkipBatchTip;
        set {
            if (fSkipBatchTip != value) {
                IniProvider.Write(value, ConfigName, nameof(SkipBatchTip));
                fSkipBatchTip = value;
            }
        }
    }
    private static bool fSkipBatchTip;

    public static bool AutomaticallyDownloadFromProtocol {
        get => fAutomaticallyDownloadFromProtocol;
        set {
            if (fAutomaticallyDownloadFromProtocol != value) {
                IniProvider.Write(value, ConfigName, nameof(AutomaticallyDownloadFromProtocol));
                fAutomaticallyDownloadFromProtocol = value;
            }
        }
    }
    private static bool fAutomaticallyDownloadFromProtocol;

    public static bool PreferFFmpeg {
        get => fPreferFFmpeg;
        set {
            if (fPreferFFmpeg != value) {
                IniProvider.Write(value, ConfigName, nameof(PreferFFmpeg));
                fPreferFFmpeg = value;
            }
        }
    }
    private static bool fPreferFFmpeg;

    public static bool SeparateBatchDownloads {
        get => fSeparateBatchDownloads;
        set {
            if (fSeparateBatchDownloads != value) {
                IniProvider.Write(value, ConfigName, nameof(SeparateBatchDownloads));
                fSeparateBatchDownloads = value;
            }
        }
    }
    private static bool fSeparateBatchDownloads;

    public static bool AddDateToBatchDownloadFolders {
        get => fAddDateToBatchDownloadFolders;
        set {
            if (fAddDateToBatchDownloadFolders != value) {
                IniProvider.Write(value, ConfigName, nameof(AddDateToBatchDownloadFolders));
                fAddDateToBatchDownloadFolders = value;
            }
        }
    }
    private static bool fAddDateToBatchDownloadFolders;

    public static int YtdlType {
        get => fYtdlType;
        set {
            if (fYtdlType != value) {
                IniProvider.Write(value, ConfigName, nameof(YtdlType));
                fYtdlType = value;
            }
        }
    }
    private static int fYtdlType;

    public static bool SubdomainFolderNames {
        get => fSubdomainFolderNames;
        set {
            if (fSubdomainFolderNames != value) {
                IniProvider.Write(value, ConfigName, nameof(SubdomainFolderNames));
                fSubdomainFolderNames = value;
            }
        }
    }
    private static bool fSubdomainFolderNames;

    public static bool ExtendedDownloaderPreferExtendedForm {
        get => fExtendedDownloaderPreferExtendedForm;
        set {
            if (fExtendedDownloaderPreferExtendedForm != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderPreferExtendedForm));
                fExtendedDownloaderPreferExtendedForm = value;
            }
        }
    }
    private static bool fExtendedDownloaderPreferExtendedForm;

    public static bool ExtendedDownloaderAutoDownloadThumbnail {
        get => fExtendedDownloaderAutoDownloadThumbnail;
        set {
            if (fExtendedDownloaderAutoDownloadThumbnail != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderAutoDownloadThumbnail));
                fExtendedDownloaderAutoDownloadThumbnail = value;
            }
        }
    }
    private static bool fExtendedDownloaderAutoDownloadThumbnail;

    public static bool ExtendedDownloaderIncludeCustomArguments {
        get => fExtendedDownloaderIncludeCustomArguments;
        set {
            if (fExtendedDownloaderIncludeCustomArguments != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderIncludeCustomArguments));
                fExtendedDownloaderIncludeCustomArguments = value;
            }
        }
    }
    private static bool fExtendedDownloaderIncludeCustomArguments;

    public static bool SkipUnavailableFragments {
        get => fSkipUnavailableFragments;
        set {
            if (fSkipUnavailableFragments != value) {
                IniProvider.Write(value, ConfigName, nameof(SkipUnavailableFragments));
                fSkipUnavailableFragments = value;
            }
        }
    }
    private static bool fSkipUnavailableFragments;

    public static bool AbortOnError {
        get => fAbortOnError;
        set {
            if (fAbortOnError != value) {
                IniProvider.Write(value, ConfigName, nameof(AbortOnError));
                fAbortOnError = value;
            }
        }
    }
    private static bool fAbortOnError;

    public static int FragmentThreads {
        get => fFragmentThreads;
        set {
            if (fFragmentThreads != value) {
                IniProvider.Write(value, ConfigName, nameof(FragmentThreads));
                fFragmentThreads = value;
            }
        }
    }
    private static int fFragmentThreads;
}
#nullable enable
namespace youtube_dl_gui;
internal static class General {
    private const string ConfigName = "General";

    static General() {
        Log.Write("Loading General config.");

        fUseStaticYtdl = IniProvider.Read(UseStaticYtdl, false, ConfigName);
        fytdlPath = IniProvider.Read(ytdlPath, string.Empty, ConfigName);
        fUseStaticFFmpeg = IniProvider.Read(UseStaticFFmpeg, false, ConfigName);
        fffmpegPath = IniProvider.Read(ffmpegPath, string.Empty, ConfigName);
        fCheckForUpdatesOnLaunch = IniProvider.Read(CheckForUpdatesOnLaunch, false, ConfigName);
        fDownloadBetaVersions = IniProvider.Read(DownloadBetaVersions, false, ConfigName);
        fHoverOverURLTextBoxToPaste = IniProvider.Read(HoverOverURLTextBoxToPaste, true, ConfigName);
        fClearURLOnDownload = IniProvider.Read(ClearURLOnDownload, false, ConfigName);
        fSaveCustomArgs = IniProvider.Read(SaveCustomArgs, 2, ConfigName);
        fClearClipboardOnDownload = IniProvider.Read(ClearClipboardOnDownload, false, ConfigName);
        fextensionsName = IniProvider.Read(extensionsName, string.Empty, ConfigName);
        fextensionsShort = IniProvider.Read(extensionsShort, string.Empty, ConfigName);
        fDeleteUpdaterOnStartup = IniProvider.Read(DeleteUpdaterOnStartup, true, ConfigName);
        fDeleteBackupOnStartup = IniProvider.Read(DeleteBackupOnStartup, false, ConfigName);
        fClipboardAutoDownloadNoticeRead = IniProvider.Read(ClipboardAutoDownloadNoticeRead, false, ConfigName);
        fClipboardAutoDownloadVerifyLinks = IniProvider.Read(ClipboardAutoDownloadVerifyLinks, true, ConfigName);
        fAutoUpdateYoutubeDl = IniProvider.Read(AutoUpdateYoutubeDl, false, ConfigName);
    }

    public static bool UseStaticYtdl {
        get => fUseStaticYtdl;
        set {
            if (fUseStaticYtdl != value) {
                IniProvider.Write(value, ConfigName, nameof(UseStaticYtdl));
                fUseStaticYtdl = value;
            }
        }
    }
    private static bool fUseStaticYtdl;

    public static string ytdlPath {
        get => fytdlPath;
        set {
            if (fytdlPath != value) {
                IniProvider.Write(value, ConfigName, nameof(ytdlPath));
                fytdlPath = value;
            }
        }
    }
    private static string fytdlPath;

    public static bool UseStaticFFmpeg {
        get => fUseStaticFFmpeg;
        set {
            if (fUseStaticFFmpeg != value) {
                IniProvider.Write(value, ConfigName, nameof(UseStaticFFmpeg));
                fUseStaticFFmpeg = value;
            }
        }
    }
    private static bool fUseStaticFFmpeg;

    public static string ffmpegPath {
        get => fffmpegPath;
        set {
            if (fffmpegPath != value) {
                IniProvider.Write(value, ConfigName, nameof(ffmpegPath));
                fffmpegPath = value;
            }
        }
    }
    private static string fffmpegPath;

    public static bool CheckForUpdatesOnLaunch {
        get => fCheckForUpdatesOnLaunch;
        set {
            if (fCheckForUpdatesOnLaunch != value) {
                IniProvider.Write(value, ConfigName, nameof(CheckForUpdatesOnLaunch));
                fCheckForUpdatesOnLaunch = value;
            }
        }
    }
    private static bool fCheckForUpdatesOnLaunch;

    public static bool DownloadBetaVersions {
        get => fDownloadBetaVersions;
        set {
            if (fDownloadBetaVersions != value) {
                IniProvider.Write(value, ConfigName, nameof(DownloadBetaVersions));
                fDownloadBetaVersions = value;
            }
        }
    }
    private static bool fDownloadBetaVersions;

    public static bool HoverOverURLTextBoxToPaste {
        get => fHoverOverURLTextBoxToPaste;
        set {
            if (fHoverOverURLTextBoxToPaste != value) {
                IniProvider.Write(value, ConfigName, nameof(HoverOverURLTextBoxToPaste));
                fHoverOverURLTextBoxToPaste = value;
            }
        }
    }
    private static bool fHoverOverURLTextBoxToPaste;

    public static bool ClearURLOnDownload {
        get => fClearURLOnDownload;
        set {
            if (fClearURLOnDownload != value) {
                IniProvider.Write(value, ConfigName, nameof(ClearURLOnDownload));
                fClearURLOnDownload = value;
            }
        }
    }
    private static bool fClearURLOnDownload;

    public static int SaveCustomArgs {
        get => fSaveCustomArgs;
        set {
            if (fSaveCustomArgs != value) {
                IniProvider.Write(value, ConfigName, nameof(SaveCustomArgs));
                fSaveCustomArgs = value;
            }
        }
    }
    private static int fSaveCustomArgs;

    public static bool ClearClipboardOnDownload {
        get => fClearClipboardOnDownload;
        set {
            if (fClearClipboardOnDownload != value) {
                IniProvider.Write(value, ConfigName, nameof(ClearClipboardOnDownload));
                fClearClipboardOnDownload = value;
            }
        }
    }
    private static bool fClearClipboardOnDownload;

    public static string extensionsName {
        get => fextensionsName;
        set {
            if (fextensionsName != value) {
                IniProvider.Write(value, ConfigName, nameof(extensionsName));
                fextensionsName = value;
            }
        }
    }
    private static string fextensionsName;

    public static string extensionsShort {
        get => fextensionsShort;
        set {
            if (fextensionsShort != value) {
                IniProvider.Write(value, ConfigName, nameof(extensionsShort));
                fextensionsShort = value;
            }
        }
    }
    private static string fextensionsShort;

    public static bool DeleteUpdaterOnStartup {
        get => fDeleteUpdaterOnStartup;
        set {
            if (fDeleteUpdaterOnStartup != value) {
                IniProvider.Write(value, ConfigName, nameof(DeleteUpdaterOnStartup));
                fDeleteUpdaterOnStartup = value;
            }
        }
    }
    private static bool fDeleteUpdaterOnStartup;

    public static bool DeleteBackupOnStartup {
        get => fDeleteBackupOnStartup;
        set {
            if (fDeleteBackupOnStartup != value) {
                IniProvider.Write(value, ConfigName, nameof(DeleteBackupOnStartup));
                fDeleteBackupOnStartup = value;
            }
        }
    }
    private static bool fDeleteBackupOnStartup;

    public static bool ClipboardAutoDownloadNoticeRead {
        get => fClipboardAutoDownloadNoticeRead;
        set {
            if (fClipboardAutoDownloadNoticeRead != value) {
                IniProvider.Write(value, ConfigName, nameof(ClipboardAutoDownloadNoticeRead));
                fClipboardAutoDownloadNoticeRead = value;
            }
        }
    }
    private static bool fClipboardAutoDownloadNoticeRead;

    public static bool ClipboardAutoDownloadVerifyLinks {
        get => fClipboardAutoDownloadVerifyLinks;
        set {
            if (fClipboardAutoDownloadVerifyLinks != value) {
                IniProvider.Write(value, ConfigName, nameof(ClipboardAutoDownloadVerifyLinks));
                fClipboardAutoDownloadVerifyLinks = value;
            }
        }
    }
    private static bool fClipboardAutoDownloadVerifyLinks;

    public static bool AutoUpdateYoutubeDl {
        get => fAutoUpdateYoutubeDl;
        set {
            if (fAutoUpdateYoutubeDl != value) {
                IniProvider.Write(value, ConfigName, nameof(AutoUpdateYoutubeDl));
                fAutoUpdateYoutubeDl = value;
            }
        }
    }
    private static bool fAutoUpdateYoutubeDl;
}
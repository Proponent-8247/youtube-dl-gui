#nullable enable
namespace youtube_dl_gui;
internal static class Batch {
    private const string ConfigName = "Batch";

    static Batch() {
        Log.Write("Loading Batch config.");

        fSelectedType = IniProvider.Read(SelectedType, -1, ConfigName);
        fSelectedVideoQuality = IniProvider.Read(SelectedVideoQuality, 0, ConfigName);
        fSelectedVideoFormat = IniProvider.Read(SelectedVideoFormat, 0, ConfigName);
        fSelectedAudioQuality = IniProvider.Read(SelectedAudioQuality, 0, ConfigName);
        fSelectedAudioFormat = IniProvider.Read(SelectedAudioFormat, 0, ConfigName);
        fDownloadVideoSound = IniProvider.Read(DownloadVideoSound, true, ConfigName);
        fDownloadAudioVBR = IniProvider.Read(DownloadAudioVBR, false, ConfigName);
        fSelectedAudioQualityVBR = IniProvider.Read(SelectedAudioQualityVBR, 0, ConfigName);
        fCustomArguments = IniProvider.Read(CustomArguments, string.Empty, ConfigName);
        fClipboardScannerNoticeViewed = IniProvider.Read(ClipboardScannerNoticeViewed, false, ConfigName);
        fClipboardScannerVerifyLinks = IniProvider.Read(ClipboardScannerVerifyLinks, true, ConfigName);
    }

    public static int SelectedType {
        get => fSelectedType;
        set {
            if (fSelectedType != value) {
                IniProvider.Write(value, ConfigName, nameof(SelectedType));
                fSelectedType = value;
            }
        }
    }
    private static int fSelectedType;

    public static int SelectedVideoQuality {
        get => fSelectedVideoQuality;
        set {
            if (fSelectedVideoQuality != value) {
                IniProvider.Write(value, ConfigName, nameof(SelectedVideoQuality));
                fSelectedVideoQuality = value;
            }
        }
    }
    private static int fSelectedVideoQuality;

    public static int SelectedVideoFormat {
        get => fSelectedVideoFormat;
        set {
            if (fSelectedVideoFormat != value) {
                IniProvider.Write(value, ConfigName, nameof(SelectedVideoFormat));
                fSelectedVideoFormat = value;
            }
        }
    }
    private static int fSelectedVideoFormat;

    public static int SelectedAudioQuality {
        get => fSelectedAudioQuality;
        set {
            if (fSelectedAudioQuality != value) {
                IniProvider.Write(value, ConfigName, nameof(SelectedAudioQuality));
                fSelectedAudioQuality = value;
            }
        }
    }
    private static int fSelectedAudioQuality;

    public static int SelectedAudioFormat {
        get => fSelectedAudioFormat;
        set {
            if (fSelectedAudioFormat != value) {
                IniProvider.Write(value, ConfigName, nameof(SelectedAudioFormat));
                fSelectedAudioFormat = value;
            }
        }
    }
    private static int fSelectedAudioFormat;

    public static bool DownloadVideoSound {
        get => fDownloadVideoSound;
        set {
            if (fDownloadVideoSound != value) {
                IniProvider.Write(value, ConfigName, nameof(DownloadVideoSound));
                fDownloadVideoSound = value;
            }
        }
    }
    private static bool fDownloadVideoSound;

    public static bool DownloadAudioVBR {
        get => fDownloadAudioVBR;
        set {
            if (fDownloadAudioVBR != value) {
                IniProvider.Write(value, ConfigName, nameof(DownloadAudioVBR));
                fDownloadAudioVBR = value;
            }
        }
    }
    private static bool fDownloadAudioVBR;

    public static int SelectedAudioQualityVBR {
        get => fSelectedAudioQualityVBR;
        set {
            if (fSelectedAudioQualityVBR != value) {
                IniProvider.Write(value, ConfigName, nameof(SelectedAudioQualityVBR));
                fSelectedAudioQualityVBR = value;
            }
        }
    }
    private static int fSelectedAudioQualityVBR;

    public static string CustomArguments {
        get => fCustomArguments;
        set {
            if (fCustomArguments != value) {
                IniProvider.Write(value, ConfigName, nameof(CustomArguments));
                fCustomArguments = value;
            }
        }
    }
    private static string fCustomArguments;

    public static bool ClipboardScannerNoticeViewed {
        get => fClipboardScannerNoticeViewed;
        set {
            if (fClipboardScannerNoticeViewed != value) {
                IniProvider.Write(value, ConfigName, nameof(ClipboardScannerNoticeViewed));
                fClipboardScannerNoticeViewed = value;
            }
        }
    }
    private static bool fClipboardScannerNoticeViewed;

    public static bool ClipboardScannerVerifyLinks {
        get => fClipboardScannerVerifyLinks;
        set {
            if (fClipboardScannerVerifyLinks != value) {
                IniProvider.Write(value, ConfigName, nameof(ClipboardScannerVerifyLinks));
                fClipboardScannerVerifyLinks = value;
            }
        }
    }
    private static bool fClipboardScannerVerifyLinks;
}
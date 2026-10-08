#nullable enable
namespace youtube_dl_gui;
internal static class Saved {
    private const string ConfigName = "Saved";

    static Saved() {
        Log.Write("Loading Saved config.");

        fdownloadType = IniProvider.Read(downloadType, 0, ConfigName);
        fconvertSaveVideoIndex = IniProvider.Read(convertSaveVideoIndex, 0, "Saved");
        fconvertSaveAudioIndex = IniProvider.Read(convertSaveAudioIndex, 0, "Saved");
        fconvertSaveUnknownIndex = IniProvider.Read(convertSaveUnknownIndex, 0, "Saved");
        fconvertType = IniProvider.Read(convertType, 0, ConfigName);
        fconvertCustom = IniProvider.Read(convertCustom, string.Empty, ConfigName);
        fvideoQuality = IniProvider.Read(videoQuality, 0, ConfigName);
        faudioQuality = IniProvider.Read(audioQuality, 0, ConfigName);
        fVideoFormat = IniProvider.Read(VideoFormat, 0, ConfigName);
        fAudioFormat = IniProvider.Read(AudioFormat, 0, ConfigName);
        fAudioVBRQuality = IniProvider.Read(AudioVBRQuality, 0, ConfigName);
        fBatchDownloaderLocation = IniProvider.Read(BatchDownloaderLocation, Point.Invalid, ConfigName);
        fBatchConverterLocation = IniProvider.Read(BatchConverterLocation, Point.Invalid, ConfigName);
        fMainFormSize = IniProvider.Read(MainFormSize, Size.Empty, ConfigName);
        fSettingsFormSize = IniProvider.Read(SettingsFormSize, Size.Empty, ConfigName);
        fFileNameSchemaHistory = IniProvider.Read(FileNameSchemaHistory, "%(title)s-%(id)s.%(ext)s|%(uploader)s\\(%(playlist_index)s) %(title)s-%(id)s.%(ext)s", ConfigName);
        fDownloadCustomArguments = IniProvider.Read(DownloadCustomArguments, string.Empty, ConfigName);
        fCustomArgumentsIndex = IniProvider.Read(CustomArgumentsIndex, -1, ConfigName);
        fConvertCustomArguments = IniProvider.Read(ConvertCustomArguments, string.Empty, ConfigName);
        fConvertCustomArgumentsIndex = IniProvider.Read(ConvertCustomArgumentsIndex, -1, ConfigName);
        fMainFormLocation = IniProvider.Read(MainFormLocation, Point.Invalid, ConfigName);
        fExtendedDownloaderLocation = IniProvider.Read(ExtendedDownloaderLocation, Point.Invalid, ConfigName);
        fExtendedDownloaderSize = IniProvider.Read(ExtendedDownloaderSize, Size.Empty, ConfigName);
        fArchiveDownloaderLocation = IniProvider.Read(ArchiveDownloaderLocation, Point.Invalid, ConfigName);
        fLogLocation = IniProvider.Read(LogLocation, Point.Invalid, ConfigName);
        fLogSize = IniProvider.Read(LogSize, Size.Empty, ConfigName);
        fExtendedDownloaderVideoColumns = IniProvider.Read(ExtendedDownloaderVideoColumns, string.Empty, ConfigName);
        fExtendedDownloaderAudioColumns = IniProvider.Read(ExtendedDownloaderAudioColumns, string.Empty, ConfigName);
        fExtendedDownloaderUnknownColumns = IniProvider.Read(ExtendedDownloaderUnknownColumns, string.Empty, ConfigName);
        fQuickDownloaderLocation = IniProvider.Read(QuickDownloaderLocation, Point.Invalid, ConfigName);
        fFileNameSchemaHistoryLocation = IniProvider.Read(FileNameSchemaHistoryLocation, Point.Invalid, ConfigName);
        fFileNameSchemaHistorySize = IniProvider.Read(FileNameSchemaHistorySize, Size.Empty, ConfigName);
        fExtendedBatchDownloaderLocation = IniProvider.Read(ExtendedBatchDownloaderLocation, Point.Invalid, ConfigName);
        fExtendedBatchDownloaderSize = IniProvider.Read(ExtendedBatchDownloaderSize, Size.Empty, ConfigName);
        fExtendedBatchDownloaderQueuedColumns = IniProvider.Read(ExtendedBatchDownloaderQueuedColumns, string.Empty, ConfigName);
    }

    public static int downloadType {
        get => fdownloadType;
        set {
            if (fdownloadType != value) {
                IniProvider.Write(value, ConfigName, nameof(downloadType));
                fdownloadType = value;
            }
        }
    }
    private static int fdownloadType;

    public static int convertSaveVideoIndex {
        get => fconvertSaveVideoIndex;
        set {
            if (fconvertSaveVideoIndex != value) {
                IniProvider.Write(value, ConfigName, nameof(convertSaveVideoIndex));
                fconvertSaveVideoIndex = value;
            }
        }
    }
    private static int fconvertSaveVideoIndex;

    public static int convertSaveAudioIndex {
        get => fconvertSaveAudioIndex;
        set {
            if (fconvertSaveAudioIndex != value) {
                IniProvider.Write(value, ConfigName, nameof(convertSaveAudioIndex));
                fconvertSaveAudioIndex = value;
            }
        }
    }
    private static int fconvertSaveAudioIndex;

    public static int convertSaveUnknownIndex {
        get => fconvertSaveUnknownIndex;
        set {
            if (fconvertSaveUnknownIndex != value) {
                IniProvider.Write(value, ConfigName, nameof(convertSaveUnknownIndex));
                fconvertSaveUnknownIndex = value;
            }
        }
    }
    private static int fconvertSaveUnknownIndex;

    public static int convertType {
        get => fconvertType;
        set {
            if (fconvertType != value) {
                IniProvider.Write(value, ConfigName, nameof(convertType));
                fconvertType = value;
            }
        }
    }
    private static int fconvertType;

    public static string convertCustom {
        get => fconvertCustom;
        set {
            if (fconvertCustom != value) {
                IniProvider.Write(value, ConfigName, nameof(convertCustom));
                fconvertCustom = value;
            }
        }
    }
    private static string fconvertCustom;

    public static int videoQuality {
        get => fvideoQuality;
        set {
            if (fvideoQuality != value) {
                IniProvider.Write(value, ConfigName, nameof(videoQuality));
                fvideoQuality = value;
            }
        }
    }
    private static int fvideoQuality;

    public static int audioQuality {
        get => faudioQuality;
        set {
            if (faudioQuality != value) {
                IniProvider.Write(value, ConfigName, nameof(audioQuality));
                faudioQuality = value;
            }
        }
    }
    private static int faudioQuality;

    public static int VideoFormat {
        get => fVideoFormat;
        set {
            if (fVideoFormat != value) {
                IniProvider.Write(value, ConfigName, nameof(VideoFormat));
                fVideoFormat = value;
            }
        }
    }
    private static int fVideoFormat;

    public static int AudioFormat {
        get => fAudioFormat;
        set {
            if (fAudioFormat != value) {
                IniProvider.Write(value, ConfigName, nameof(AudioFormat));
                fAudioFormat = value;
            }
        }
    }
    private static int fAudioFormat;

    public static int AudioVBRQuality {
        get => fAudioVBRQuality;
        set {
            if (fAudioVBRQuality != value) {
                IniProvider.Write(value, ConfigName, nameof(AudioVBRQuality));
                fAudioVBRQuality = value;
            }
        }
    }
    private static int fAudioVBRQuality;

    public static Point BatchDownloaderLocation {
        get => fBatchDownloaderLocation;
        set {
            if (fBatchDownloaderLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(BatchDownloaderLocation));
                fBatchDownloaderLocation = value;
            }
        }
    }
    private static Point fBatchDownloaderLocation;

    public static Point BatchConverterLocation {
        get => fBatchConverterLocation;
        set {
            if (fBatchConverterLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(BatchConverterLocation));
                fBatchConverterLocation = value;
            }
        }
    }
    private static Point fBatchConverterLocation;

    public static Size MainFormSize {
        get => fMainFormSize;
        set {
            if (fMainFormSize != value) {
                IniProvider.Write(value, ConfigName, nameof(MainFormSize));
                fMainFormSize = value;
            }
        }
    }
    private static Size fMainFormSize;

    public static Size SettingsFormSize {
        get => fSettingsFormSize;
        set {
            if (fSettingsFormSize != value) {
                IniProvider.Write(value, ConfigName, nameof(SettingsFormSize));
                fSettingsFormSize = value;
            }
        }
    }
    private static Size fSettingsFormSize;

    public static string FileNameSchemaHistory {
        get => fFileNameSchemaHistory;
        set {
            if (fFileNameSchemaHistory != value) {
                IniProvider.Write(value, ConfigName, nameof(FileNameSchemaHistory));
                fFileNameSchemaHistory = value;
            }
        }
    }
    private static string fFileNameSchemaHistory;

    public static string DownloadCustomArguments {
        get => fDownloadCustomArguments;
        set {
            if (fDownloadCustomArguments != value) {
                IniProvider.Write(value, ConfigName, nameof(DownloadCustomArguments));
                fDownloadCustomArguments = value;
            }
        }
    }
    private static string fDownloadCustomArguments;

    public static int CustomArgumentsIndex {
        get => fCustomArgumentsIndex;
        set {
            if (fCustomArgumentsIndex != value) {
                IniProvider.Write(value, ConfigName, nameof(CustomArgumentsIndex));
                fCustomArgumentsIndex = value;
            }
        }
    }
    private static int fCustomArgumentsIndex;

    public static string ConvertCustomArguments {
        get => fConvertCustomArguments;
        set {
            if (fConvertCustomArguments != value) {
                IniProvider.Write(value, ConfigName, nameof(ConvertCustomArguments));
                fConvertCustomArguments = value;
            }
        }
    }
    private static string fConvertCustomArguments;

    public static int ConvertCustomArgumentsIndex {
        get => fConvertCustomArgumentsIndex;
        set {
            if (fConvertCustomArgumentsIndex != value) {
                IniProvider.Write(value, ConfigName, nameof(ConvertCustomArgumentsIndex));
                fConvertCustomArgumentsIndex = value;
            }
        }
    }
    private static int fConvertCustomArgumentsIndex;

    public static Point MainFormLocation {
        get => fMainFormLocation;
        set {
            if (fMainFormLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(MainFormLocation));
                fMainFormLocation = value;
            }
        }
    }
    private static Point fMainFormLocation;

    public static Point ExtendedDownloaderLocation {
        get => fExtendedDownloaderLocation;
        set {
            if (fExtendedDownloaderLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderLocation));
                fExtendedDownloaderLocation = value;
            }
        }
    }
    private static Point fExtendedDownloaderLocation;

    public static Size ExtendedDownloaderSize {
        get => fExtendedDownloaderSize;
        set {
            if (fExtendedDownloaderSize != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderSize));
                fExtendedDownloaderSize = value;
            }
        }
    }
    private static Size fExtendedDownloaderSize;

    public static Point ArchiveDownloaderLocation {
        get => fArchiveDownloaderLocation;
        set {
            if (fArchiveDownloaderLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(ArchiveDownloaderLocation));
                fArchiveDownloaderLocation = value;
            }
        }
    }
    private static Point fArchiveDownloaderLocation;

    public static Point LogLocation {
        get => fLogLocation;
        set {
            if (fLogLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(LogLocation));
                fLogLocation = value;
            }
        }
    }
    private static Point fLogLocation;

    public static Size LogSize {
        get => fLogSize;
        set {
            if (fLogSize != value) {
                IniProvider.Write(value, ConfigName, nameof(LogSize));
                fLogSize = value;
            }
        }
    }
    private static Size fLogSize;

    public static string ExtendedDownloaderVideoColumns {
        get => fExtendedDownloaderVideoColumns;
        set {
            if (fExtendedDownloaderVideoColumns != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderVideoColumns));
                fExtendedDownloaderVideoColumns = value;
            }
        }
    }
    private static string fExtendedDownloaderVideoColumns;

    public static string ExtendedDownloaderAudioColumns {
        get => fExtendedDownloaderAudioColumns;
        set {
            if (fExtendedDownloaderAudioColumns != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderAudioColumns));
                fExtendedDownloaderAudioColumns = value;
            }
        }
    }
    private static string fExtendedDownloaderAudioColumns;

    public static string ExtendedDownloaderUnknownColumns {
        get => fExtendedDownloaderUnknownColumns;
        set {
            if (fExtendedDownloaderUnknownColumns != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedDownloaderUnknownColumns));
                fExtendedDownloaderUnknownColumns = value;
            }
        }
    }
    private static string fExtendedDownloaderUnknownColumns;

    public static Point QuickDownloaderLocation {
        get => fQuickDownloaderLocation;
        set {
            if (fQuickDownloaderLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(QuickDownloaderLocation));
                fQuickDownloaderLocation = value;
            }
        }
    }
    private static Point fQuickDownloaderLocation;

    public static Point FileNameSchemaHistoryLocation {
        get => fFileNameSchemaHistoryLocation;
        set {
            if (fFileNameSchemaHistoryLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(FileNameSchemaHistoryLocation));
                fFileNameSchemaHistoryLocation = value;
            }
        }
    }
    private static Point fFileNameSchemaHistoryLocation;

    public static Size FileNameSchemaHistorySize {
        get => fFileNameSchemaHistorySize;
        set {
            if (fFileNameSchemaHistorySize != value) {
                IniProvider.Write(value, ConfigName, nameof(FileNameSchemaHistorySize));
                fFileNameSchemaHistorySize = value;
            }
        }
    }
    private static Size fFileNameSchemaHistorySize;

    public static Point ExtendedBatchDownloaderLocation {
        get => fExtendedBatchDownloaderLocation;
        set {
            if (fExtendedBatchDownloaderLocation != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedBatchDownloaderLocation));
                fExtendedBatchDownloaderLocation = value;
            }
        }
    }
    private static Point fExtendedBatchDownloaderLocation;

    public static Size ExtendedBatchDownloaderSize {
        get => fExtendedBatchDownloaderSize;
        set {
            if (fExtendedBatchDownloaderSize != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedBatchDownloaderSize));
                fExtendedBatchDownloaderSize = value;
            }
        }
    }
    private static Size fExtendedBatchDownloaderSize;

    public static string ExtendedBatchDownloaderQueuedColumns {
        get => fExtendedBatchDownloaderQueuedColumns;
        set {
            if (fExtendedBatchDownloaderQueuedColumns != value) {
                IniProvider.Write(value, ConfigName, nameof(ExtendedBatchDownloaderQueuedColumns));
                fExtendedBatchDownloaderQueuedColumns = value;
            }
        }
    }
    private static string fExtendedBatchDownloaderQueuedColumns;
}
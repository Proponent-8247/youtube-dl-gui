#nullable enable
namespace youtube_dl_gui;
internal static class Converts {
    private const string ConfigName = "Converts";

    static Converts() {
        Log.Write("Loading Converter config.");

        fdetectFiletype = IniProvider.Read(detectFiletype, true, ConfigName);
        fclearOutput = IniProvider.Read(clearOutput, false, ConfigName);
        fclearInput = IniProvider.Read(clearInput, false, ConfigName);
        fvideoBitrate = IniProvider.Read(videoBitrate, 7500, ConfigName);
        fvideoPreset = IniProvider.Read(videoPreset, 5, ConfigName);
        fvideoProfile = IniProvider.Read(videoProfile, 1, ConfigName);
        fvideoCRF = IniProvider.Read(videoCRF, 8, ConfigName);
        fvideoFastStart = IniProvider.Read(videoFastStart, false, ConfigName);
        fhideFFmpegCompile = IniProvider.Read(hideFFmpegCompile, false, ConfigName);
        faudioBitrate = IniProvider.Read(audioBitrate, 256, ConfigName);
        fvideoUseBitrate = IniProvider.Read(videoUseBitrate, false, ConfigName);
        fvideoUsePreset = IniProvider.Read(videoUsePreset, false, ConfigName);
        fvideoUseProfile = IniProvider.Read(videoUseProfile, false, ConfigName);
        fvideoUseCRF = IniProvider.Read(videoUseCRF, true, ConfigName);
        faudioUseBitrate = IniProvider.Read(audioUseBitrate, true, ConfigName);
        fCloseAfterFinish = IniProvider.Read(CloseAfterFinish, false, ConfigName);
    }

    public static bool detectFiletype {
        get => fdetectFiletype;
        set {
            if (fdetectFiletype != value) {
                IniProvider.Write(value, ConfigName, nameof(detectFiletype));
                fdetectFiletype = value;
            }
        }
    }
    private static bool fdetectFiletype;

    public static bool clearOutput {
        get => fclearOutput;
        set {
            if (fclearOutput != value) {
                IniProvider.Write(value, ConfigName, nameof(clearOutput));
                fclearOutput = value;
            }
        }
    }
    private static bool fclearOutput;

    public static bool clearInput {
        get => fclearInput;
        set {
            if (fclearInput != value) {
                IniProvider.Write(value, ConfigName, nameof(clearInput));
                fclearInput = value;
            }
        }
    }
    private static bool fclearInput;

    public static int videoBitrate {
        get => fvideoBitrate;
        set {
            if (fvideoBitrate != value) {
                IniProvider.Write(value, ConfigName, nameof(videoBitrate));
                fvideoBitrate = value;
            }
        }
    }
    private static int fvideoBitrate;

    public static int videoPreset {
        get => fvideoPreset;
        set {
            if (fvideoPreset != value) {
                IniProvider.Write(value, ConfigName, nameof(videoPreset));
                fvideoPreset = value;
            }
        }
    }
    private static int fvideoPreset;

    public static int videoProfile {
        get => fvideoProfile;
        set {
            if (fvideoProfile != value) {
                IniProvider.Write(value, ConfigName, nameof(videoProfile));
                fvideoProfile = value;
            }
        }
    }
    private static int fvideoProfile;

    public static int videoCRF {
        get => fvideoCRF;
        set {
            if (fvideoCRF != value) {
                IniProvider.Write(value, ConfigName, nameof(videoCRF));
                fvideoCRF = value;
            }
        }
    }
    private static int fvideoCRF;

    public static bool videoFastStart {
        get => fvideoFastStart;
        set {
            if (fvideoFastStart != value) {
                IniProvider.Write(value, ConfigName, nameof(videoFastStart));
                fvideoFastStart = value;
            }
        }
    }
    private static bool fvideoFastStart;

    public static bool hideFFmpegCompile {
        get => fhideFFmpegCompile;
        set {
            if (fhideFFmpegCompile != value) {
                IniProvider.Write(value, ConfigName, nameof(hideFFmpegCompile));
                fhideFFmpegCompile = value;
            }
        }
    }
    private static bool fhideFFmpegCompile;

    public static int audioBitrate {
        get => faudioBitrate;
        set {
            if (faudioBitrate != value) {
                IniProvider.Write(value, ConfigName, nameof(audioBitrate));
                faudioBitrate = value;
            }
        }
    }
    private static int faudioBitrate;

    public static bool videoUseBitrate {
        get => fvideoUseBitrate;
        set {
            if (fvideoUseBitrate != value) {
                IniProvider.Write(value, ConfigName, nameof(videoUseBitrate));
                fvideoUseBitrate = value;
            }
        }
    }
    private static bool fvideoUseBitrate;

    public static bool videoUsePreset {
        get => fvideoUsePreset;
        set {
            if (fvideoUsePreset != value) {
                IniProvider.Write(value, ConfigName, nameof(videoUsePreset));
                fvideoUsePreset = value;
            }
        }
    }
    private static bool fvideoUsePreset;

    public static bool videoUseProfile {
        get => fvideoUseProfile;
        set {
            if (fvideoUseProfile != value) {
                IniProvider.Write(value, ConfigName, nameof(videoUseProfile));
                fvideoUseProfile = value;
            }
        }
    }
    private static bool fvideoUseProfile;

    public static bool videoUseCRF {
        get => fvideoUseCRF;
        set {
            if (fvideoUseCRF != value) {
                IniProvider.Write(value, ConfigName, nameof(videoUseCRF));
                fvideoUseCRF = value;
            }
        }
    }
    private static bool fvideoUseCRF;

    public static bool audioUseBitrate {
        get => faudioUseBitrate;
        set {
            if (faudioUseBitrate != value) {
                IniProvider.Write(value, ConfigName, nameof(audioUseBitrate));
                faudioUseBitrate = value;
            }
        }
    }
    private static bool faudioUseBitrate;

    public static bool CloseAfterFinish {
        get => fCloseAfterFinish;
        set {
            if (fCloseAfterFinish != value) {
                IniProvider.Write(value, ConfigName, nameof(CloseAfterFinish));
                fCloseAfterFinish = value;
            }
        }
    }
    private static bool fCloseAfterFinish;
}
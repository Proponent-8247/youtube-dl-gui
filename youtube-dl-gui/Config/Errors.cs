#nullable enable
namespace youtube_dl_gui;
internal static class Errors {
    private const string ConfigName = "Errors";

    static Errors() {
        Log.Write("Loading Error config.");
        fdetailedErrors = IniProvider.Read(detailedErrors, false, ConfigName);
        flogErrors = IniProvider.Read(logErrors, false, ConfigName);
        Log.AllowWritingToFile = flogErrors;
        fsuppressErrors = IniProvider.Read(suppressErrors, false, ConfigName);
    }

    public static bool detailedErrors {
        get => fdetailedErrors;
        set {
            if (fdetailedErrors != value) {
                IniProvider.Write(value, ConfigName, nameof(detailedErrors));
                fdetailedErrors = value;
            }
        }
    }
    private static bool fdetailedErrors;

    public static bool logErrors {
        get => flogErrors;
        set {
            if (flogErrors != value) {
                IniProvider.Write(value, ConfigName, nameof(logErrors));
                flogErrors = value;
                Log.AllowWritingToFile = value;
            }
        }
    }
    private static bool flogErrors;

    public static bool suppressErrors {
        get => fsuppressErrors;
        set {
            if (fsuppressErrors != value) {
                IniProvider.Write(value, ConfigName, nameof(suppressErrors));
                fsuppressErrors = value;
            }
        }
    }
    private static bool fsuppressErrors;
}
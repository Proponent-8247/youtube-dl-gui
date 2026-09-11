#nullable enable
namespace youtube_dl_gui;
/// <summary>
/// Manages the custom arguments the user provides.
/// </summary>
internal static class CustomArguments {
    /// <summary>
    /// Represends a list of all used youtube-dl arguments.
    /// </summary>
    public static List<string> YtdlArguments { get; } = [];
    /// <summary>
    /// Represents the last used youtube-dl argument by the user.
    /// </summary>
    public static string LastUsedYtdlArgument { get; set; } = string.Empty;

    /// <summary>
    /// Represends a list of all used ffmpeg arguments.
    /// </summary>
    public static List<string> FfmpegArguments { get; } = [];
    /// <summary>
    /// Represents the last used ffmpeg argument by the user.
    /// </summary>
    public static string LastUsedFfmpegArgument { get; set; } = string.Empty;

    internal static string ArgsFilePath => System.IO.Path.Combine(Environment.CurrentDirectory, "args.txt");

    internal static bool TryReadArgsFile(out string[] Arguments, out string Error) {
        Arguments = [];
        Error = string.Empty;
        try {
            if (!System.IO.File.Exists(ArgsFilePath)) {
                Error = $"args.txt does not exist: {ArgsFilePath}";
                return false;
            }
            Arguments = System.IO.File.ReadAllLines(ArgsFilePath);
            return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException
                                or UnauthorizedAccessException
                                or System.Security.SecurityException
                                or ArgumentException
                                or NotSupportedException) {
            Error = $"Could not read args.txt at '{ArgsFilePath}': {ex.Message}";
            return false;
        }
    }

    internal static bool TryWriteArgsFile(IEnumerable<string> Arguments, out string Error) {
        Error = string.Empty;
        string TempPath = ArgsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            System.IO.File.WriteAllLines(TempPath, Arguments);
            if (System.IO.File.Exists(ArgsFilePath)) {
                System.IO.File.Replace(TempPath, ArgsFilePath, null);
            }
            else {
                System.IO.File.Move(TempPath, ArgsFilePath);
            }
            return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException
                                or UnauthorizedAccessException
                                or System.Security.SecurityException
                                or ArgumentException
                                or NotSupportedException) {
            Error = $"Could not write args.txt at '{ArgsFilePath}': {ex.Message}";
            try { if (System.IO.File.Exists(TempPath)) System.IO.File.Delete(TempPath); } catch { }
            return false;
        }
    }

    static CustomArguments() {
        string[] YtdlArgs = [];
        switch (General.SaveCustomArgs) {
            case 1 when System.IO.File.Exists(ArgsFilePath):
                if (!TryReadArgsFile(out YtdlArgs, out string Error)) {
                    Log.Write(Error);
                }
                break;
            case 2 when !Saved.DownloadCustomArguments.IsNullEmptyWhitespace():
                YtdlArgs = Saved.DownloadCustomArguments.Trim('|', ' ').Split('|');
                break;
        }
        if (YtdlArgs.Length > 0) {
            HashSet<string> Arguments = [];
            YtdlArgs.For((Arg) => {
                if (!Arg.IsNullEmptyWhitespace() && Arguments.Add(Arg)) {
                    YtdlArguments.Add(Arg);
                }
            });

            int Index = Saved.CustomArgumentsIndex;
            if (Index > -1 && Index < YtdlArguments.Count)
                LastUsedYtdlArgument = YtdlArguments[Index];
        }

        if (!Saved.ConvertCustomArguments.IsNullEmptyWhitespace()) {
            HashSet<string> Arguments = [];
            string[] Args = Saved.ConvertCustomArguments.Trim('|', ' ').Split('|');
            Args.For((Arg) => {
                if (!Arg.IsNullEmptyWhitespace() && Arguments.Add(Arg)) {
                    FfmpegArguments.Add(Arg);
                }
            });

            int Index = Saved.ConvertCustomArgumentsIndex;
            if (Index > -1 && Index < FfmpegArguments.Count)
                LastUsedFfmpegArgument = FfmpegArguments[Index];
        }
    }

    /// <summary>
    ///     Adds a custom argument to the youtube-dl argument list.
    /// </summary>
    /// <param name="Arg">
    ///     The argument to add to the argument list.
    /// </param>
    /// <param name="SetAsLastUsed">
    ///     Whether the last used argument should be set to the <paramref name="Arg"/> value.
    /// </param>
    public static void AddYtdlArgument(string Arg, bool SetAsLastUsed) {
        if (!YtdlArguments.Contains(Arg)) {
            YtdlArguments.Add(Arg);
        }

        if (SetAsLastUsed)
            LastUsedYtdlArgument = Arg;
    }

    /// <summary>
    ///     Adds a custom argument to the ffmpeg argument list.
    /// </summary>
    /// <param name="Arg">
    ///     The argument to add to the argument list.
    /// </param>
    /// <param name="SetAsLastUsed">
    ///     Whether the last used argument should be set to the <paramref name="Arg"/> value.
    /// </param>
    public static void AddFfmpegArgument(string Arg, bool SetAsLastUsed) {
        if (!FfmpegArguments.Contains(Arg)) {
            FfmpegArguments.Add(Arg);
            Saved.ConvertCustomArguments += "|" + Arg;
        }

        if (SetAsLastUsed)
            LastUsedFfmpegArgument = Arg;
    }
}
#nullable enable
namespace youtube_dl_gui;
using System.Text.RegularExpressions;
internal static class Arguments {
    /// <summary>
    /// The prefix of the protocol.
    /// </summary>
    private const string ProtocolPrefix = "ytdlgui:";

    /// <summary>
    /// Whether the arguments have been parsed.
    /// </summary>
    public static bool ArgumentsParsed {
        get; private set;
    } = false;

    /// <summary>
    /// The tuple list of parsed arguments.
    /// </summary>
    public static List<(ArgumentType Type, string? Data)> ParsedArguments {
        get;
    } = [];

    /// <summary>
    /// Parses an array of arguments.
    /// </summary>
    /// <param name="Arguments">The array of arguments that should be parsed.</param>
    /// <returns>True if the arguments were fully parsed; otherwise, false.</returns>
    public static bool ParseArguments(string[] Arguments) {
        if (!ArgumentsParsed) {
            try {
                ParsedArguments.AddRange(RetrieveArguments(Arguments));
                ArgumentsParsed = true;
            }
            catch {
                return false;
            }
        }
        return ArgumentsParsed;
    }

    /// <summary>
    /// Gets a tuple-list of arguments parsed from an array.
    /// </summary>
    /// <param name="Arguments">The arguments array to parse.</param>
    /// <returns>A filled list with valid arguments parsed from the array; otherwise, empty if the array is empty or no valid arguments are within it.</returns>
    public static List<(ArgumentType Type, string? Data)> RetrieveArguments(string[] Arguments) {
        List<(ArgumentType Type, string? Data)> TemporaryList = [];
        if (Arguments.Length  < 1) {
            Log.Write("No arguments are in the arguments array.");
            return TemporaryList;
        }

        List<string> Args = [.. Arguments];
        string RawFirstArgument = Arguments[0];
        bool ProtocolArgument = RawFirstArgument.StartsWith(ProtocolPrefix, StringComparison.InvariantCultureIgnoreCase);
        if (ProtocolArgument) {
            RawFirstArgument = RawFirstArgument[ProtocolPrefix.Length..];
            int LiteralSeparator = RawFirstArgument.IndexOf(' ');
            int EncodedSeparator = RawFirstArgument.IndexOf("%20", StringComparison.OrdinalIgnoreCase);
            int SeparatorIndex;
            int SeparatorLength;
            if (LiteralSeparator >= 0 && (EncodedSeparator < 0 || LiteralSeparator < EncodedSeparator)) {
                SeparatorIndex = LiteralSeparator;
                SeparatorLength = 1;
            }
            else {
                SeparatorIndex = EncodedSeparator;
                SeparatorLength = EncodedSeparator >= 0 ? 3 : 0;
            }

            Args.RemoveAt(0);
            if (SeparatorIndex >= 0) {
                string Command = RawFirstArgument[..SeparatorIndex];
                try { Command = Uri.UnescapeDataString(Command); } catch (UriFormatException) { }
                string Operand = RawFirstArgument[(SeparatorIndex + SeparatorLength)..];
                if (Operand.StartsWith("%22", StringComparison.OrdinalIgnoreCase)
                    && Operand.EndsWith("%22", StringComparison.OrdinalIgnoreCase)
                    && Operand.Length >= 6) {
                    Operand = Operand[3..^3];
                }
                else if (Operand.StartsWith("\"") && Operand.EndsWith("\"") && Operand.Length >= 2) {
                    Operand = Operand[1..^1].Replace("\"\"", "\"");
                }
                Args.Insert(0, Command);
                Args.Insert(1, Operand);
            }
            else {
                try { RawFirstArgument = Uri.UnescapeDataString(RawFirstArgument); } catch (UriFormatException) { }
                Args.Insert(0, RawFirstArgument);
            }
            Arguments = [.. Args];
        }
        else {
            string FirstArgument;
            try {
                FirstArgument = Uri.UnescapeDataString(RawFirstArgument);
            }
            catch (UriFormatException) {
                FirstArgument = RawFirstArgument;
            }

            var Matches = Regex.Matches(FirstArgument, @"\""(\""\""|[^\""])+\""|[^ ]+", RegexOptions.ExplicitCapture);
            if (Matches.Count > 0) {
                Args.RemoveAt(0);
                for (int i = 0; i < Matches.Count; i++) {
                    if (Matches[i].Success) {
                        string NewArgument = Matches[i].Value;
                        if (NewArgument.StartsWith("\"") && NewArgument.EndsWith("\"")) {
                            NewArgument = NewArgument[1..^1].Replace("\"\"", "\"");
                        }
                        if (NewArgument.StartsWith("\\\"") && NewArgument.EndsWith("\"")) {
                            NewArgument = NewArgument[1..^1];
                        }

                        Args.Insert(i, NewArgument);
                    }
                }
                Arguments = [.. Args];
            }
        }

        Log.Write($"There are {Arguments.Length} arguments to parse through.");
        bool BreakLoop = false;
        for (int i = 0; i < Arguments.Length; i++) {
            if (Arguments[i].ToLowerInvariant().StartsWith(ProtocolPrefix))
                Arguments[i] = Arguments[i][ProtocolPrefix.Length..];

            if (Arguments[i].StartsWith("-"))
                Arguments[i] = Arguments[i][1..];

            switch (Arguments[i].ToLowerInvariant()) {
                case "v" or "video" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadVideo, Arguments[i]));
                } break;
                case "vau" or "vauth" or "videoauth" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadAuthenticateVideo, Arguments[i]));
                } break;

                case "a" or "audio" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadAudio, Arguments[i]));
                } break;
                case "aau" or "aauth" or "audioauth" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadAuthenticateAudio, Arguments[i]));
                } break;

                case "c" or "custom" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadCustom, Arguments[i]));
                } break;
                case "cau" or "cauth" or "customauth" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadAuthenticateCustom, Arguments[i]));
                } break;

                case "archived" or "ar" or "var" or "varchive" or "videoarchive" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadArchived, Arguments[i]));
                } break;
                case "aar" or "aarchive" or "audioarchive" when ++i < Arguments.Length: {
                    TemporaryList.Add((ArgumentType.DownloadArchivedNoSound, Arguments[i]));
                } break;

                case "installprotocol": {
                    TemporaryList.Add((ArgumentType.InstallProtocol, null));
                    BreakLoop = true;
                } break;
            }

            if (BreakLoop) {
                break;
            }
        }

        TemporaryList.TrimExcess();
        return TemporaryList;
    }
}
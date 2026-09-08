#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace youtube_dl_gui.History {
    // This allow-list is deliberately scoped to protected runs. Unknown options, aliases,
    // external configuration and output overrides cannot silently weaken the guarantee.
    internal static class HistoryCommandPolicy {
        private static readonly HashSet<string> Flags = new HashSet<string>((
            "--ignore-config --abort-on-error --no-abort-on-error --no-playlist --yes-playlist " +
            "--playlist-reverse --playlist-random --lazy-playlist --force-ipv4 --force-ipv6 -4 -6 " +
            "--geo-bypass --no-geo-bypass --no-check-certificates --no-warnings --verbose -v " +
            "--newline --progress --no-progress --extract-audio -x --keep-video -k " +
            "--write-info-json --write-description --write-thumbnail --write-all-thumbnails " +
            "--embed-thumbnail --embed-subs --embed-metadata --add-metadata --all-subs " +
            "--write-subs --write-auto-subs --no-write-subs --no-write-auto-subs " +
            "--restrict-filenames --windows-filenames --hls-prefer-ffmpeg --hls-prefer-native " +
            "--abort-on-unavailable-fragments --abort-on-unavailable-fragment --skip-unavailable-fragments " +
            "--netrc --no-cache-dir --no-color --continue --no-continue --part " +
            "--video-multistreams --audio-multistreams --no-video-multistreams --no-audio-multistreams " +
            "--prefer-free-formats --no-prefer-free-formats --no-overwrites").Split(' '), StringComparer.Ordinal);
        private static readonly HashSet<string> Values = new HashSet<string>((
            "-o --output -f --format -S --format-sort --merge-output-format --audio-quality --audio-format " +
            "--remux-video --recode-video --ffmpeg-location --playlist-start --playlist-end --playlist-items " +
            "--datebefore --dateafter --date --match-filter --min-filesize --max-filesize " +
            "--min-views --max-views --age-limit --sub-format --sub-langs --convert-subs --convert-thumbnails " +
            "--limit-rate -r --retries -R --fragment-retries --file-access-retries --extractor-retries " +
            "--socket-timeout --retry-sleep --sleep-interval --max-sleep-interval --sleep-requests --sleep-subtitles " +
            "--concurrent-fragments -N --proxy --source-address --user-agent --referer --add-headers " +
            "--username -u --password -p --twofactor --video-password --cookies --cookies-from-browser " +
            "--netrc-location --geo-bypass-country --geo-bypass-ip-block --extractor-args --js-runtimes " +
            "--remote-components --impersonate --batch-file -a").Split(' '), StringComparer.Ordinal);

        public static string Build(string arguments, string libraryPath, string archivePath) {
            string template;
            return Build(arguments, libraryPath, archivePath, out template);
        }
        public static string Build(string arguments, string libraryPath, string archivePath, out string template) {
            template = null;
            string root = HistoryStore.Canonical(libraryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var tokens = Tokenize(arguments);
            int outputs = 0;
            int inputs = 0;
            for (int i = 0; i < tokens.Count; i++) {
                string token = tokens[i];
                if (!token.StartsWith("-", StringComparison.Ordinal)) {
                    Uri uri;
                    if (Regex.IsMatch(token, @"\A[A-Za-z0-9_-]{11}\z")
                        || (Uri.TryCreate(token, UriKind.Absolute, out uri) &&
                            (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "ytarchive"))) { inputs++; continue; }
                    throw new HistoryException("Protected downloads require explicit media URLs or a batch file. Unexpected positional argument.");
                }
                int equals = token.IndexOf('=');
                string key = equals < 0 ? token : token.Substring(0, equals);
                if (Flags.Contains(key)) {
                    if (equals >= 0) throw new HistoryException("Unexpected value for protected-download option " + key);
                    continue;
                }
                if (!Values.Contains(key)) {
                    throw new HistoryException("Download History cannot safely use " + key + ". Remove this option or disable Download History for this operation. Archive overrides, aliases, external configs, executable hooks and partial-section downloads are not permitted.");
                }
                string value;
                if (equals >= 0) value = token.Substring(equals + 1);
                else {
                    if (++i >= tokens.Count) throw new HistoryException("Missing value for " + key);
                    value = tokens[i];
                }
                if (key == "-o" || key == "--output") {
                    if (++outputs != 1) throw new HistoryException("Download History requires the GUI's single output template; remove custom output overrides.");
                    string full = Path.GetFullPath(value);
                    if (!Path.IsPathRooted(value) || !full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new HistoryException("The output template is outside the protected library.");
                    template = full.Substring(root.Length);
                    HistoryTemplate.Validate(template);
                }
                if (key == "--batch-file" || key == "-a") {
                    if (value == "-") throw new HistoryException("Interactive batch input is not supported by protected jobs.");
                    inputs++;
                }
                if ((key == "--format" || key == "-f") && (value.Contains(",") || value == "-" || System.Text.RegularExpressions.Regex.IsMatch(value, @"\ball\b")))
                    throw new HistoryException("Multiple independent format outputs need a separate history policy. Disable Download History for this operation or select one combined media output.");
                if (key == "--cookies") {
                    string cookiePath = Path.GetFullPath(value);
                    if (cookiePath.StartsWith(archivePath, StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileName(cookiePath).StartsWith(".ytdlg-history", StringComparison.OrdinalIgnoreCase))
                        throw new HistoryException("The cookies file overlaps Download History storage.");
                }
            }
            if (outputs != 1 || inputs == 0) throw new HistoryException("Protected downloads require a media input and exactly one output template.");
            return arguments + Suffix(archivePath);
        }
        public static string Suffix(string archivePath) {
            // The opt-in UI discloses these integrity requirements. Keep them last so that
            // skipped fragments cannot turn an incomplete source into a trusted success.
            return " --ignore-config --no-break-on-existing --abort-on-unavailable-fragments --write-info-json --download-archive " + Quote(archivePath);
        }
        public static string Quote(string argument) {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in argument) {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        internal static List<string> Tokenize(string command) {
            if (command == null || command.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
                throw new HistoryException("Invalid command line for a protected download.");
            var result = new List<string>();
            int i = 0;
            while (i < command.Length) {
                while (i < command.Length && char.IsWhiteSpace(command[i])) i++;
                if (i == command.Length) break;
                var token = new StringBuilder();
                bool quoted = false;
                while (i < command.Length && (quoted || !char.IsWhiteSpace(command[i]))) {
                    int slashes = 0;
                    while (i < command.Length && command[i] == '\\') { slashes++; i++; }
                    if (i < command.Length && command[i] == '"') {
                        token.Append('\\', slashes / 2);
                        if (slashes % 2 != 0) token.Append('"');
                        else if (quoted && i + 1 < command.Length && command[i + 1] == '"') { token.Append('"'); i++; }
                        else quoted = !quoted;
                        i++;
                    }
                    else {
                        token.Append('\\', slashes);
                        if (i < command.Length && (quoted || !char.IsWhiteSpace(command[i]))) token.Append(command[i++]);
                    }
                }
                if (quoted) throw new HistoryException("Unbalanced quotes in protected-download arguments.");
                result.Add(token.ToString());
            }
            return result;
        }
    }
}

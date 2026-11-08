using System;
using System.Collections;
using System.IO;
using System.Reflection;

internal static partial class AuditRegression {
    private static void ArgumentReferenceMatchesRuntimeParser() {
        string repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string documentation = File.ReadAllText(Path.Combine(repo, "ARGUMENTS.md"));
        Type parser = T("youtube_dl_gui.Arguments");

        // Exact parser contract, not future/reserved CLI aliases.
        string[,] supported = {
            { "v", "DownloadVideo" }, { "video", "DownloadVideo" },
            { "vau", "DownloadAuthenticateVideo" }, { "vauth", "DownloadAuthenticateVideo" },
            { "videoauth", "DownloadAuthenticateVideo" },
            { "archived", "DownloadArchived" }, { "ar", "DownloadArchived" },
            { "var", "DownloadArchived" }, { "varchive", "DownloadArchived" },
            { "videoarchive", "DownloadArchived" },
            { "a", "DownloadAudio" }, { "audio", "DownloadAudio" },
            { "aau", "DownloadAuthenticateAudio" }, { "aauth", "DownloadAuthenticateAudio" },
            { "audioauth", "DownloadAuthenticateAudio" },
            { "aar", "DownloadArchivedNoSound" }, { "aarchive", "DownloadArchivedNoSound" },
            { "audioarchive", "DownloadArchivedNoSound" },
            { "c", "DownloadCustom" }, { "custom", "DownloadCustom" },
            { "cau", "DownloadAuthenticateCustom" }, { "cauth", "DownloadAuthenticateCustom" },
            { "customauth", "DownloadAuthenticateCustom" },
            { "installprotocol", "InstallProtocol" }
        };

        const string source = "https://example.invalid/watch?v=audit";
        for (int i = 0; i < supported.GetLength(0); i++) {
            string alias = supported[i, 0], expected = supported[i, 1];
            Require(documentation.Contains("`-" + alias + "`"),
                "Documented parser alias is missing: " + alias);
            foreach (string prefix in new[] { "", "-" }) {
                string[] input = expected == "InstallProtocol"
                    ? new[] { prefix + alias }
                    : new[] { prefix + alias, source };
                IEnumerable values = (IEnumerable)Call(parser, null, "RetrieveArguments", (object)input);
                IEnumerator it = values.GetEnumerator();
                Require(it.MoveNext(), "Parser rejected documented argument: " + prefix + alias);
                object entry = it.Current;
                FieldInfo type = entry.GetType().GetField("Item1");
                FieldInfo data = entry.GetType().GetField("Item2");
                Require(type != null && data != null,
                    "Parser returned an unknown argument representation for " + alias);
                Equal(expected, type.GetValue(entry).ToString());
                Equal(expected == "InstallProtocol" ? null : source, data.GetValue(entry));
                Require(!it.MoveNext(), "Parser produced duplicate actions for " + alias);
            }
            // Alias matching is case-insensitive in both direct and protocol forwarding.
            string[] upperInput = expected == "InstallProtocol"
                ? new[] { "-" + alias.ToUpperInvariant() }
                : new[] { "-" + alias.ToUpperInvariant(), source };
            IEnumerable upperParsed = (IEnumerable)Call(parser, null, "RetrieveArguments", (object)upperInput);
            IEnumerator upperCursor = upperParsed.GetEnumerator();
            Require(upperCursor.MoveNext(),
                "Case-insensitive documented alias was rejected: " + alias);
            FieldInfo upperType = upperCursor.Current.GetType().GetField("Item1");
            Require(upperType != null && upperType.GetValue(upperCursor.Current).ToString() == expected,
                "Case-insensitive alias was misrouted: " + alias);
            Require(!upperCursor.MoveNext(), "Uppercase alias produced multiple actions: " + alias);
        }

        foreach (string unsupported in new[] { "vaa", "varchiveauth", "videoarchiveauth",
            "aaa", "aarchiveauth", "audioarchiveauth" }) {
            Require(!documentation.Contains("`-" + unsupported + "`"),
                "README still describes an unsupported archive-auth alias: " + unsupported);
            IEnumerable parsed = (IEnumerable)Call(parser, null, "RetrieveArguments",
                (object)new[] { "-" + unsupported, source });
            Require(!parsed.GetEnumerator().MoveNext(),
                "Formerly advertised unsupported alias unexpectedly became active: " + unsupported);
        }
    }
}

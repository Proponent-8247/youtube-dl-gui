using System;
using System.Reflection;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object NewProviderBoundaryDownloader(bool extended) {
        string url = "https://www.youtube.com/watch?v=9qFjkwAElDs";
        object item = New(extended ? "youtube_dl_gui.ExtendedMediaDetails" : "youtube_dl_gui.DownloadInfo", url);
        Type type = item.GetType();
        Set(type, item, "FileNameSchema", "%(title)s-%(id)s.%(ext)s");
        Set(type, item, extended ? "SelectedType" : "Type",
            Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
        if (extended) {
            object format = New("youtube_dl_gui.YoutubeDlSubdata+Format");
            Set(format.GetType(), format, "Identifier", "18");
            Set(format.GetType(), format, "Extension", "mp4");
            ListViewItem listItem = new ListViewItem();
            listItem.Tag = format;
            Set(type, item, "SelectedVideoItem", listItem);
        }
        return item;
    }

    private static bool ProviderBoundaryGenerate(object item, bool extended) {
        return extended
            ? (bool)Call(item.GetType(), item, "GenerateArguments")
            : (bool)Call(item.GetType(), item, "GenerateArguments",
                (Action<string>)(delegate(string ignored) { }));
    }

    private static void ProviderProxySettingCannotInjectArguments() {
        Type downloads = T("youtube_dl_gui.Downloads");
        FieldInfo use = downloads.GetField("fUseProxy", All);
        FieldInfo type = downloads.GetField("fProxyType", All);
        FieldInfo host = downloads.GetField("fProxyIP", All);
        FieldInfo port = downloads.GetField("fProxyPort", All);
        object[] prior = { use.GetValue(null), type.GetValue(null), host.GetValue(null), port.GetValue(null) };
        try {
            use.SetValue(null, true);
            type.SetValue(null, 0);
            host.SetValue(null, "127.0.0.1");
            port.SetValue(null, "8080");
            using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
                DownloadHistoryEnable(fixture, string.Empty);
                Type helper = T("youtube_dl_gui.DownloadHelper");
                object[] safe = { null, null };
                Equal(true, Call(helper, null, "TryBuildProxyArgument", safe));
                Equal("--proxy https://127.0.0.1:8080/", safe[0]);
                for (int i = 0; i < 2; i++) {
                    bool extended = i == 1;
                    object item = NewProviderBoundaryDownloader(extended);
                    Require(ProviderBoundaryGenerate(item, extended),
                        "Safe proxy arguments were rejected by downloader " + i);
                    string args = (string)Get(item, "Arguments");
                    Require(args.Contains("--proxy https://127.0.0.1:8080/") &&
                        args.Contains("--download-archive"),
                        "Safe proxy generation or protected suffix was altered for downloader " + i);
                }

                string[] hostileHosts = { "1 -- ", "127.0.0.1 --", "127.0.0.1\r\n--" };
                foreach (string invalid in hostileHosts) {
                    host.SetValue(null, invalid);
                    object[] denied = { null, null };
                    Equal(false, Call(helper, null, "TryBuildProxyArgument", denied));
                    Require(((string)denied[1]).IndexOf("proxy", StringComparison.OrdinalIgnoreCase) >= 0,
                        "Malformed proxy host was rejected without an explanation");
                    for (int i = 0; i < 2; i++) {
                        bool extended = i == 1;
                        object item = NewProviderBoundaryDownloader(extended);
                        Require(!ProviderBoundaryGenerate(item, extended),
                            "Malformed proxy host created a provider command for downloader " + i);
                        Equal(null, Get(item, "DownloadHistoryExecution"));
                    }
                }

                host.SetValue(null, "127.0.0.1");
                port.SetValue(null, "80 --exec");
                object[] badPort = { null, null };
                Equal(false, Call(helper, null, "TryBuildProxyArgument", badPort));
                for (int i = 0; i < 2; i++) {
                    bool extended = i == 1;
                    object item = NewProviderBoundaryDownloader(extended);
                    Require(!ProviderBoundaryGenerate(item, extended),
                        "Malformed proxy port created a provider command for downloader " + i);
                }
            }
        }
        finally {
            use.SetValue(null, prior[0]);
            type.SetValue(null, prior[1]);
            host.SetValue(null, prior[2]);
            port.SetValue(null, prior[3]);
        }
    }

    private static void ProviderSelectionFieldsCannotInjectOptions() {
        Type selection = T("youtube_dl_gui.PlaylistSelectionType");
        using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
            DownloadHistoryEnable(fixture, string.Empty);
            string[,] safe = {
                { "PlaylistItems", "1,3:5", "--playlist-items 1,3:5" },
                { "DateBefore", "20261106", "--datebefore 20261106" },
                { "DateDuring", "20261106", "--date 20261106" },
                { "DateAfter", "20261106", "--dateafter 20261106" }
            };
            for (int i = 0; i < safe.GetLength(0); i++) {
                object item = NewProviderBoundaryDownloader(false);
                Set(item.GetType(), item, "PlaylistSelection", Enum.Parse(selection, safe[i, 0]));
                Set(item.GetType(), item, "PlaylistSelectionArg", safe[i, 1]);
                Require(ProviderBoundaryGenerate(item, false), "Valid selection rejected: " + safe[i, 0]);
                string args = (string)Get(item, "Arguments");
                Require(args.Contains(safe[i, 2]) && args.Contains("--download-archive"),
                    "Valid selection or protected suffix missing from command: " + safe[i, 0]);
            }

            string[,] hostile = {
                { "PlaylistItems", "1 --exec unwanted" },
                { "PlaylistItems", "1 --" },
                { "PlaylistItems", "1,\r\n--plugin-dirs rogue" },
                { "DateBefore", "20261106 --exec unwanted" },
                { "DateDuring", "20261106 --" },
                { "DateAfter", "20261106\r\n--config-locations rogue" },
                { "DateDuring", "20260230" }
            };
            for (int i = 0; i < hostile.GetLength(0); i++) {
                object item = NewProviderBoundaryDownloader(false);
                Set(item.GetType(), item, "PlaylistSelection", Enum.Parse(selection, hostile[i, 0]));
                Set(item.GetType(), item, "PlaylistSelectionArg", hostile[i, 1]);
                Require(!ProviderBoundaryGenerate(item, false),
                    "Selection field injected provider options: " + hostile[i, 0]);
                Equal(null, Get(item, "DownloadHistoryExecution"));
            }

            object bounded = NewProviderBoundaryDownloader(false);
            Set(bounded.GetType(), bounded, "PlaylistSelection", Enum.Parse(selection, "PlaylistStartPlaylistEnd"));
            Set(bounded.GetType(), bounded, "PlaylistSelectionIndexStart", 2);
            Set(bounded.GetType(), bounded, "PlaylistSelectionIndexEnd", 4);
            Require(ProviderBoundaryGenerate(bounded, false), "Integer playlist start/end path regressed");
            string boundedArgs = (string)Get(bounded, "Arguments");
            Require(boundedArgs.Contains("--playlist-start 2") && boundedArgs.Contains("--playlist-end 4"),
                "Integer playlist start/end controls were lost");

            Call(fixture.History, null, "CommitSettings", false, string.Empty, true, null);
            object disabled = NewProviderBoundaryDownloader(false);
            Set(disabled.GetType(), disabled, "PlaylistSelection", Enum.Parse(selection, "PlaylistItems"));
            Set(disabled.GetType(), disabled, "PlaylistSelectionArg", "1 --exec unwanted");
            Require(!ProviderBoundaryGenerate(disabled, false),
                "Malformed pasted selection changed command semantics while history was disabled");
        }
    }

    private static void ProviderSubtitleFormatCannotInjectArguments() {
        Type downloads = T("youtube_dl_gui.Downloads");
        FieldInfo enabled = downloads.GetField("fSaveSubtitles", All);
        FieldInfo format = downloads.GetField("fSubtitleFormat", All);
        object[] prior = { enabled.GetValue(null), format.GetValue(null) };
        try {
            enabled.SetValue(null, true);
            format.SetValue(null, "ass/srt/best");
            using (DownloadHistoryFixture fixture = new DownloadHistoryFixture(true)) {
                DownloadHistoryEnable(fixture, string.Empty);
                Type helper = T("youtube_dl_gui.DownloadHelper");
                object[] safe = { null, null };
                Equal(true, Call(helper, null, "TryBuildSubtitleFormatArgument", safe));
                Equal("--sub-format ass/srt/best", safe[0]);
                for (int i = 0; i < 2; i++) {
                    bool extended = i == 1;
                    object item = NewProviderBoundaryDownloader(extended);
                    Require(ProviderBoundaryGenerate(item, extended),
                        "Safe subtitle format was rejected by downloader " + i);
                    string args = (string)Get(item, "Arguments");
                    Require(args.Contains("--sub-format ass/srt/best") &&
                        args.Contains("--download-archive"),
                        "Safe subtitle format or protected suffix missing for downloader " + i);
                }

                string[] hostile = { "srt --exec whoami", "srt --", "srt\r\n--exec" };
                foreach (string invalid in hostile) {
                    format.SetValue(null, invalid);
                    object[] denied = { null, null };
                    Equal(false, Call(helper, null, "TryBuildSubtitleFormatArgument", denied));
                    Require(((string)denied[1]).IndexOf("subtitle", StringComparison.OrdinalIgnoreCase) >= 0,
                        "Malformed subtitle format was rejected without an explanation");
                    for (int i = 0; i < 2; i++) {
                        bool extended = i == 1;
                        object item = NewProviderBoundaryDownloader(extended);
                        Require(!ProviderBoundaryGenerate(item, extended),
                            "Persisted subtitle format injected a provider option in downloader " + i);
                        Equal(null, Get(item, "DownloadHistoryExecution"));
                    }
                }
            }
        }
        finally {
            enabled.SetValue(null, prior[0]);
            format.SetValue(null, prior[1]);
        }
    }
}

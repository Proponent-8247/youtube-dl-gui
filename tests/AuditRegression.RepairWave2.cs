using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static string RepositoryRoot() {
        return Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
    }

    private static Assembly LoadUpdaterAssembly() {
        return Assembly.LoadFrom(Path.Combine(RepositoryRoot(), "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe"));
    }

    private static string Constant(Type type, string name) {
        FieldInfo field = type.GetField(name, All);
        Require(field != null, "Missing constant " + type.FullName + "." + name);
        return (string)field.GetRawConstantValue();
    }

    private static object FormatWithId(string id) {
        object format = New("youtube_dl_gui.YoutubeDlSubdata+Format");
        Set(format.GetType(), format, "Identifier", id);
        return format;
    }

    static partial void RunRepairWave2Tests() {
        Test("P001.UpdaterReleaseUrlsUseFork", () => {
            Assembly updater = LoadUpdaterAssembly();
            Type github = updater.GetType("youtube_dl_gui_updater.Github", true);
            Type form = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
            string latest = Constant(github, "LatestRepo");
            string all = Constant(github, "AllReleaseRepo");
            string download = Constant(form, "ApplicationDownloadUrl");
            Require(latest.IndexOf("github.com/repos/Proponent-8247/youtube-dl-gui/", StringComparison.OrdinalIgnoreCase) >= 0, "Standalone updater latest-release metadata still targets upstream: " + latest);
            Require(all.IndexOf("github.com/repos/Proponent-8247/youtube-dl-gui/", StringComparison.OrdinalIgnoreCase) >= 0, "Standalone updater release metadata still targets upstream: " + all);
            Require(download.IndexOf("github.com/Proponent-8247/", StringComparison.OrdinalIgnoreCase) >= 0, "Updater payload still targets upstream: " + download);
        });

        Test("CR_O009.WarmVideoAudioPreferenceParity", () => {
            Type program = T("youtube_dl_gui.Program");
            MethodInfo policy = program.GetMethod("ShouldSkipVideoAudio", All);
            Require(policy != null, "Shared video-audio preference policy is missing");
            Type argumentType = T("youtube_dl_gui.ArgumentType");
            Type downloads = T("youtube_dl_gui.Downloads");
            bool original = (bool)downloads.GetProperty("VideoDownloadSound", All).GetValue(null, null);
            try {
                Set(downloads, null, "VideoDownloadSound", false);
                Equal(true, policy.Invoke(null, new object[] { Enum.Parse(argumentType, "DownloadVideo") }));
                Equal(true, policy.Invoke(null, new object[] { Enum.Parse(argumentType, "DownloadAuthenticateVideo") }));
                Set(downloads, null, "VideoDownloadSound", true);
                Equal(false, policy.Invoke(null, new object[] { Enum.Parse(argumentType, "DownloadVideo") }));
                Equal(false, policy.Invoke(null, new object[] { Enum.Parse(argumentType, "DownloadAuthenticateVideo") }));
                Equal(true, policy.Invoke(null, new object[] { Enum.Parse(argumentType, "DownloadVideoNoSound") }));
                Equal(true, policy.Invoke(null, new object[] { Enum.Parse(argumentType, "DownloadAuthenticateVideoNoSound") }));
            }
            finally { Set(downloads, null, "VideoDownloadSound", original); }
        });

        Test("CR_O013.ExtendedFallbackPreservesRequestedMediaKind", () => {
            Type mediaType = T("youtube_dl_gui.ExtendedMediaDetails");
            Type downloadType = T("youtube_dl_gui.DownloadType");

            object video = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/video");
            ListViewItem v = new ListViewItem("video");
            v.Tag = FormatWithId("v1");
            Set(mediaType, video, "SelectedType", Enum.Parse(downloadType, "Video"));
            Set(mediaType, video, "SelectedVideoItem", v);
            Set(mediaType, video, "VideoDownloadAudio", false);
            Require((bool)Call(mediaType, video, "GenerateArguments"), "Video argument generation failed");
            string videoArgs = (string)Get(video, "Arguments");
            Require(videoArgs.Contains("-f " + Escape("v1/bestvideo")), "No-audio video fallback can still select muxed best: " + videoArgs);

            object audio = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/audio");
            ListViewItem a = new ListViewItem("audio");
            a.Tag = FormatWithId("a1");
            Set(mediaType, audio, "SelectedType", Enum.Parse(downloadType, "Audio"));
            Set(mediaType, audio, "SelectedAudioItem", a);
            Require((bool)Call(mediaType, audio, "GenerateArguments"), "Audio argument generation failed");
            string audioArgs = (string)Get(audio, "Arguments");
            Require(audioArgs.Contains("-f " + Escape("a1/bestaudio")), "Audio-only fallback can still select muxed best: " + audioArgs);

            object separate = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/separate");
            ListViewItem sv = new ListViewItem("video"); sv.Tag = FormatWithId("v2");
            ListViewItem sa = new ListViewItem("audio"); sa.Tag = FormatWithId("a2");
            Set(mediaType, separate, "SelectedType", Enum.Parse(downloadType, "Video"));
            Set(mediaType, separate, "SelectedVideoItem", sv);
            Set(mediaType, separate, "SelectedAudioItem", sa);
            Set(mediaType, separate, "VideoDownloadAudio", true);
            Set(mediaType, separate, "VideoSeparateAudio", true);
            Require((bool)Call(mediaType, separate, "GenerateArguments"), "Separate-stream argument generation failed");
            string separateArgs = (string)Get(separate, "Arguments");
            Require(separateArgs.Contains("-f " + Escape("v2/bestvideo,a2/bestaudio")), "Separate-stream fallback crosses media kinds: " + separateArgs);
        });

        Test("CR_O017.LogPreferenceControlsFileGate", () => {
            Type errors = T("youtube_dl_gui.Errors");
            Type log = T("murrty.logging.Log");
            PropertyInfo option = errors.GetProperty("logErrors", All);
            PropertyInfo gate = log.GetProperty("AllowWritingToFile", All);
            bool original = (bool)option.GetValue(null, null);
            bool originalGate = (bool)gate.GetValue(null, null);
            try {
                option.SetValue(null, !original, null);
                Equal(!original, gate.GetValue(null, null));
                option.SetValue(null, original, null);
                Equal(original, gate.GetValue(null, null));
            }
            finally {
                try { option.SetValue(null, original, null); } catch { }
                gate.SetValue(null, originalGate, null);
            }
        });

        Test("CR_O018.ShippedAboutTranslationsMatchTwoArgumentContract", () => {
            foreach (string name in new[] { "Dutch.ini", "German.ini", "Spanish.ini" }) {
                string path = Path.Combine(RepositoryRoot(), "Languages", name);
                string line = Array.Find(File.ReadAllLines(path), x => x.StartsWith("lbAboutBody=", StringComparison.Ordinal));
                Require(line != null, "Missing lbAboutBody in " + name);
                string value = line.Substring("lbAboutBody=".Length).Replace("\\n", "\n").Replace("\\r", "\r");
                string formatted = string.Format(CultureInfo.InvariantCulture, value, "murrty", "2026-09-10");
                Require(formatted.IndexOf("{2}", StringComparison.Ordinal) < 0, name + " still references a third About argument");
            }
        });

        Test("CR_O029.LocalizedFormsApplyLanguageAtConstruction", () => {
            Type language = T("youtube_dl_gui.Language");
            using (Form misc = (Form)New("youtube_dl_gui.frmMiscTools")) {
                Equal(language.GetProperty("frmTools", All).GetValue(null, null), misc.Text);
                Button remove = (Button)Field(misc, "btnMiscToolsRemoveAudio");
                Equal(language.GetProperty("btnMiscToolsRemoveAudio", All).GetValue(null, null), remove.Text);
            }
            Type contentType = T("murrty.updater.GithubRepoContent");
            Array empty = Array.CreateInstance(contentType, 0);
            using (Form languages = (Form)New("youtube_dl_gui.frmDownloadLanguage", empty)) {
                Equal(language.GetProperty("frmDownloadLanguage", All).GetValue(null, null), languages.Text);
            }
            using (Form generic = (Form)New("youtube_dl_gui.frmGenericDownloadProgress", "https://example.invalid/file", Path.Combine(Environment.CurrentDirectory, "not-started.bin"))) {
                Equal(language.GetProperty("frmGenericDownloadProgress", All).GetValue(null, null), generic.Text);
            }
        });

        Test("CR_O030.UpdaterLanguageParserMatchesSharedGrammar", () => {
            Assembly updater = LoadUpdaterAssembly();
            Type language = updater.GetType("youtube_dl_gui_updater.Language", true);
            MethodInfo parser = language.GetMethod("GetControlInfo", All);
            Require(parser != null, "Updater language parser was not found");
            string[,] cases = new string[,] {
                { "key = https://example.invalid/help?a=1", "https://example.invalid/help?a=1" },
                { "key = value // comment", "value" },
                { "key = \"literal // content\" // comment", "\"literal // content\"" },
                { "key = left=right", "left=right" }
            };
            for (int i = 0; i < cases.GetLength(0); i++) {
                object[] args = new object[] { cases[i, 0], null, null };
                parser.Invoke(null, args);
                Equal("key", args[1]);
                Equal(cases[i, 1], args[2]);
            }
        });
    }
}

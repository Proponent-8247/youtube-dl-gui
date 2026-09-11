using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void VerifySingleLogWrite(bool updater) {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        string assembly = updater ? Path.Combine(root, "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe") : App.Location;
        string scratch = Path.Combine(Environment.CurrentDirectory, "log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string fixture = Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe");
        object result = RunOwned(new ProcessStartInfo(fixture, "--log-probe " + Escape(assembly)) { WorkingDirectory = scratch }, CancellationToken.None, 10000, 65536);
        Equal(0, Get(result, "ExitCode"));
        Require(((string)Get(result, "StandardOutput")).EndsWith("written", StringComparison.Ordinal), "Log writer did not return after writing");
        string[] logs = Directory.GetFiles(scratch, "ex_*.log");
        Equal(1, logs.Length);
        Require(File.ReadAllText(logs[0]).Contains("audit-log-fixture"), "Exception log content was not preserved");
    }
    private static object LanguageContent(Type type, string name) {
        object value = Activator.CreateInstance(type);
        Set(type, value, "name", name);
        Set(type, value, "download_url", "https://example.invalid/lang/" + Uri.EscapeDataString(name));
        Set(type, value, "size", 123L);
        return value;
    }
    private static void RunInputBoundaryTests() {
        Test("N014.ApplicationLogWriteReturnsAfterSuccess", () => VerifySingleLogWrite(false));
        Test("N014.UpdaterLogWriteReturnsAfterSuccess", () => VerifySingleLogWrite(true));
        Test("N013.LongClipboardTextCompletesWithinDeadline", () => {
            string fixture = Path.Combine(Path.GetDirectoryName(Self), "ThumbnailFixture.exe");
            object result = RunOwned(new ProcessStartInfo(fixture, "--url-probe " + Escape(App.Location)), CancellationToken.None, 5000, 65536);
            Equal(0, Get(result, "ExitCode"));
            Equal("False", Get(result, "StandardOutput"));
        });
        Test("N013.ClipboardHeuristicPreservesAcceptedInputs", () => {
            foreach (string text in new[] { "a.b", "https://example.invalid/playlist?a=1", "before\na.b\nafter", "a b.c d", "..." }) {
                Equal(true, Call(T("youtube_dl_gui.DownloadHelper"), null, "SupportedDownloadLink", text));
            }
            foreach (string text in new[] { "", "a", ".", "a.", ".b", "a\n.b", "no dot here" }) {
                Equal(false, Call(T("youtube_dl_gui.DownloadHelper"), null, "SupportedDownloadLink", text));
            }
        });
        Test("O041.RemoteLanguageNamesStayInsideLanguageDirectory", () => {
            Type contentType = T("murrty.updater.GithubRepoContent");
            string[] names = {
                "Spanish.ini",
                "日本語.ini",
                "..\\outside.ini",
                "nested/file.ini",
                "C:\\outside.ini",
                "CON.ini",
                "not-a-language.exe"
            };
            Array values = Array.CreateInstance(contentType, names.Length);
            for (int i = 0; i < names.Length; i++) values.SetValue(LanguageContent(contentType, names[i]), i);
            Type formType = T("youtube_dl_gui.frmDownloadLanguage");
            using (Form form = (Form)Activator.CreateInstance(formType, new object[] { values })) {
                Array accepted = (Array)Field(form, "EnumeratedLanguages");
                ListView list = (ListView)Field(form, "lvAvailableLanguages");
                Equal(2, accepted.Length);
                Equal(2, list.Items.Count);
                string displayed = list.Items[0].Text + "\n" + list.Items[1].Text;
                Require(displayed.Contains("Spanish") && displayed.Contains("日本語"), "Valid simple .ini language names were not retained");
                foreach (string rejected in new[] { "outside", "nested", "CON", "not-a-language" })
                    Require(!displayed.Contains(rejected), "Unsafe language metadata was offered in the UI: " + rejected);
            }
        });
        Test("CR_O001.TimePickerTimeSpanPreservesLongDurations", () => {
            using (Control picker = (Control)New("murrty.controls.TimePicker")) {
                Set(picker.GetType(), picker, "DateBasedTime", false);
                Set(picker.GetType(), picker, "TimeSpan", TimeSpan.FromHours(25));
                Equal(25d, ((TimeSpan)Get(picker, "TimeSpan")).TotalHours);
                Equal("25:00:00", ((TextBox)Field(picker, "TimeDisplay")).Text);
            }
        });
        Test("CR_O002.TimePickerSetValueUpdatesHourWidth", () => {
            using (Control picker = (Control)New("murrty.controls.TimePicker")) {
                Set(picker.GetType(), picker, "DateBasedTime", false);
                Call(picker.GetType(), picker, "SetValue", 100, 12, 34, 0);
                Equal(3, Get(picker, "HourToMinuteSeparator"));
                Equal("100:12:34", ((TextBox)Field(picker, "TimeDisplay")).Text);
            }
        });
        Test("CR_O003.TimePickerContainsMalformedAndOverflowEdits", () => {
            using (Control picker = (Control)New("murrty.controls.TimePicker")) {
                Set(picker.GetType(), picker, "DateBasedTime", false);
                Call(picker.GetType(), picker, "SetValue", 5, 6, 7, 0);
                TextBox display = (TextBox)Field(picker, "TimeDisplay");
                display.Text = "1";
                Call(picker.GetType(), picker, "UpdateControl");
                Equal("05:06:07", display.Text);

                object maximum = New("murrty.controls.Time", int.MaxValue, 0, 0, 0);
                Set(picker.GetType(), picker, "Value", maximum);
                Call(picker.GetType(), picker, "SelectHourPosition");
                Call(picker.GetType(), picker, "TimeDisplay_KeyDown", display, new KeyEventArgs(Keys.Up));
                Equal(int.MaxValue, picker.GetType().GetProperty("Hours", All).GetValue(picker, null));
            }
        });
        Test("CR_O010.BatchRejectsQuoteOnlySources", () => {
            using (Form form = (Form)New("youtube_dl_gui.frmBatchDownloader")) {
                ((ComboBox)Field(form, "cbBatchDownloadType")).SelectedIndex = 0;
                Call(form.GetType(), form, "AddItemToList", "\"\"");
                Equal(0, ((ListView)Field(form, "lvBatchDownloadQueue")).Items.Count);
                Call(form.GetType(), form, "AddItemToList", "https://example.invalid/video");
                Equal(1, ((ListView)Field(form, "lvBatchDownloadQueue")).Items.Count);
            }
        });
        Test("CR_O011.ExtendedSchemaKeepsConfiguredDefault", () => {
            Type downloads = T("youtube_dl_gui.Downloads");
            string previous = (string)downloads.GetProperty("fileNameSchema", All).GetValue(null, null);
            const string wanted = "AUDIT_%(id)s.%(ext)s";
            try {
                Set(downloads, null, "fileNameSchema", wanted);
                using (Form form = (Form)New("youtube_dl_gui.frmExtendedDownloader", "https://example.invalid/fixture", false)) {
                    object media = Get(form, "MediaDetails");
                    Equal(wanted, Get(media, "FileNameSchema"));
                    Set(media.GetType(), media, "InfoRetrieved", true);
                    Call(form.GetType(), form, "SelectedMediaChanged", media);
                    Equal(wanted, ((ComboBox)Field(form, "cbSchema")).Text);
                }
            }
            finally { Set(downloads, null, "fileNameSchema", previous); }
        });
        Test("CR_O012.FormatSelectorsAreSingleArguments", () => {
            object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/fixture");
            object format = New("youtube_dl_gui.YoutubeDlSubdata+Format");
            Set(format.GetType(), format, "Identifier", "fixture --print AUDIT_BOUNDARY");
            ListViewItem item = new ListViewItem("fixture");
            item.Tag = format;
            Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
            Set(media.GetType(), media, "SelectedVideoItem", item);
            Set(media.GetType(), media, "VideoDownloadAudio", false);
            Require((bool)Call(media.GetType(), media, "GenerateArguments"), "Extended argument generation failed");
            string command = (string)Get(media, "Arguments");
            int selectorStart = command.IndexOf("-f \"fixture --print AUDIT_BOUNDARY/", StringComparison.Ordinal);
            Require(selectorStart >= 0, "Format selector did not begin as one quoted operand: " + command);
            int selectorEnd = command.IndexOf("\" --ffmpeg-location", selectorStart, StringComparison.Ordinal);
            Require(selectorEnd > selectorStart, "Format selector was not escaped as one operand: " + command);
        });
        Test("E2E_O014.ExtendedRelativeWindowsRootResolvesFromProgramPath", () => {
            Type downloads = T("youtube_dl_gui.Downloads");
            string previous = (string)downloads.GetProperty("downloadPath", All).GetValue(null, null);
            try {
                Set(downloads, null, "downloadPath", ".\\audit-relative");
                object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/fixture");
                Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Custom"));
                Set(media.GetType(), media, "CustomArguments", "--simulate");
                Require((bool)Call(media.GetType(), media, "GenerateArguments"), "Extended argument generation failed");
                string command = (string)Get(media, "Arguments");
                string programPath = (string)T("youtube_dl_gui.Program").GetProperty("ProgramPath", All).GetValue(null, null);
                Require(command.Contains("-o \"" + programPath + "\\audit-relative\\"), "Windows relative root was not resolved from ProgramPath: " + command);
            }
            finally { Set(downloads, null, "downloadPath", previous); }
        });
        Test("CR_O015_E2E_O018.DurationFormattingRoundsAndScales", () => {
            object data = New("youtube_dl_gui.YoutubeDlData");
            Set(data.GetType(), data, "DurationTime", 59.9m);
            Equal("1:00", Get(data, "Duration"));
            Set(data.GetType(), data, "DurationTime", 1000000000m);
            Stopwatch watch = Stopwatch.StartNew();
            string duration = (string)Get(data, "Duration");
            watch.Stop();
            Require(!string.IsNullOrWhiteSpace(duration), "Large duration did not format");
            Require(watch.ElapsedMilliseconds < 250, "Duration formatting remained value-proportional: " + watch.ElapsedMilliseconds + " ms");
        });
        Test("E2E_O019.MediaSizeOverflowIsContained", () => {
            object data = New("youtube_dl_gui.YoutubeDlData");
            object format = New("youtube_dl_gui.YoutubeDlSubdata+Format");
            Set(data.GetType(), data, "DurationTime", decimal.MaxValue);
            Set(format.GetType(), format, "VideoBitrate", decimal.MaxValue);
            Equal("null", Call(data.GetType(), data, "GetApproximateVideoSize", format));
        });
        Test("CR_O019.ExtendedTextBoxNavigationDoesNotPoisonNextDigit", () => {
            using (Control box = (Control)New("murrty.controls.ExtendedTextBox")) {
                Set(box.GetType(), box, "TextType", Enum.Parse(T("murrty.controls.AllowedCharacters"), "NumericOnly"));
                Call(box.GetType(), box, "OnKeyDown", new KeyEventArgs(Keys.Left));
                Call(box.GetType(), box, "OnKeyDown", new KeyEventArgs(Keys.D1));
                KeyPressEventArgs press = new KeyPressEventArgs('1');
                Call(box.GetType(), box, "OnKeyPress", press);
                Require(!press.Handled, "A valid digit was suppressed after navigation");
            }
        });
        Test("CR_O020.VersionParserRejectsMalformedSeparators", () => {
            Type version = T("murrty.updater.Version");
            System.Reflection.MethodInfo parse = version.GetMethods(All).Single(m => m.Name == "TryParse" && m.GetParameters().Length == 2);
            foreach (string valid in new[] { "1", "1.2", "1.2.3", "1.2.3-4" }) {
                object[] args = { valid, Activator.CreateInstance(version) };
                Require((bool)parse.Invoke(null, args), "Valid version rejected: " + valid);
            }
            foreach (string invalid in new[] { "1-2-3", "1a2", "1/2", "1.2-3-4" }) {
                object[] args = { invalid, Activator.CreateInstance(version) };
                Require(!(bool)parse.Invoke(null, args), "Malformed version accepted: " + invalid);
            }
        });
        Test("E2E_O020.InvalidBooleanIniUsesCallerDefault", () => {
            Type iniType = T("youtube_dl_gui.IniProvider");
            System.Reflection.MethodInfo read = iniType.GetMethods(All).Single(m => m.Name == "Read" && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(bool));
            string ini = (string)iniType.GetField("IniPath", All).GetValue(null);
            string previous = File.ReadAllText(ini);
            try {
                File.WriteAllText(ini, "[youtube-dl-gui]\r\nAuditBool=not-a-bool\r\n");
                Equal(true, read.Invoke(null, new object[] { false, true, null, "AuditBool" }));
                File.WriteAllText(ini, "[youtube-dl-gui]\r\nAuditBool= off \r\n");
                Equal(false, read.Invoke(null, new object[] { true, true, null, "AuditBool" }));
                File.WriteAllText(ini, "[youtube-dl-gui]\r\nAuditBool= on \r\n");
                Equal(true, read.Invoke(null, new object[] { false, false, null, "AuditBool" }));
            }
            finally { File.WriteAllText(ini, previous); }
        });
        Test("E2E_O032.ProgressPercentageIsSafeForUiRange", () => {
            object[] high = { new[] { "[download]", "101%", "of", "10MiB", "at", "1MiB/s", "ETA", "00:10" }, 0f, "" };
            Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", high);
            Equal(100f, high[1]);
            object[] low = { new[] { "[download]", "-5%", "of", "10MiB", "at", "1MiB/s", "ETA", "00:10" }, 0f, "" };
            Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", low);
            Equal(0f, low[1]);
            object[] nan = { new[] { "[download]", "NaN%", "of", "10MiB", "at", "1MiB/s", "ETA", "00:10" }, 42f, "" };
            Equal("Could not parse line", Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", nan));
            Equal(42f, nan[1]);
        });
        Test("E2E_O044.BatchIdsAreUniqueAndReadable", () => {
            Type helper = T("youtube_dl_gui.BatchHelper");
            string first = (string)Call(helper, null, "CreateBatchId");
            string second = (string)Call(helper, null, "CreateBatchId");
            Require(first != second, "Independent batches received the same identifier");
            Require(first.Length > 19 && first[4] == '_' && first[10] == '-', "Batch identifier lost its readable timestamp prefix: " + first);
        });
        Test("P001.ApplicationUpdateUrlsUseForkReleases", () => {
            Type links = T("youtube_dl_gui.GithubLinks");
            Equal("https://api.github.com/repos/Proponent-8247/youtube-dl-gui/releases/latest", Call(links, null, "GetApplicationReleaseMetadataUrl", false));
            Equal("https://api.github.com/repos/Proponent-8247/youtube-dl-gui/releases", Call(links, null, "GetApplicationReleaseMetadataUrl", true));
            Equal("https://github.com/Proponent-8247/youtube-dl-gui/releases", links.GetField("ApplicationReleasesUrl", All).GetValue(null));
        });
        Test("Progress.MalformedRowsCannotThrow", () => {
            foreach (string percent in new[] { null, "", "%", "NaN%", "Infinity%", "1.0%", "12345678900000%", "0,5%" }) {
                for (int length = 0; length <= 12; length++) {
                    string[] parts = new string[length];
                    for (int i = 0; i < length; i++) parts[i] = "x";
                    if (length > 0) parts[0] = "[download]";
                    if (length > 1) parts[1] = percent;
                    if (length > 3) parts[3] = "~";
                    object[] args = { parts, 0f, "" };
                    Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", args);
                }
            }
            object[] empty = { null, 0f, "" };
            Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", empty);
        });
        Test("Progress.PercentageUsesInvariantCulture", () => {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                object[] args = { new[] { "[download]", "12.5%", "of", "10MiB", "at", "1MiB/s", "ETA", "00:10" }, 0f, "" };
                Call(T("youtube_dl_gui.DownloadHelper"), null, "GetTransferData", args);
                Equal(12.5f, args[1]);
                Equal("00:10", args[2]);
            }
            finally { Thread.CurrentThread.CurrentCulture = saved; }
        });
    }
}

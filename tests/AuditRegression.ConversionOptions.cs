using System;
using System.Collections;
using System.IO;
using System.Windows.Forms;

internal static partial class AuditRegression {
    static partial void RunConversionOptionTests() {
        Test("N001.ConverterPresetDropdownMatchesEnum", () => {
            using (Form form = (Form)New("youtube_dl_gui.frmExtendedConverter")) {
                object details = New("youtube_dl_gui.ExtendedConversionDetails", "input.mkv");
                Field(form, "SelectedConversion", details);
                ((CheckBox)Field(form, "chkVideoSetPreset")).Checked = true;
                ComboBox combo = (ComboBox)Field(form, "cbVideoPreset");
                for (int index = 0; index < combo.Items.Count; index++) {
                    combo.SelectedIndex = index;
                    Call(form.GetType(), form, "SaveMediaOptions");
                    Equal(index + 1, Convert.ToInt32(Get(details, "VideoPreset")));
                }
                combo.SelectedIndex = -1;
                Call(form.GetType(), form, "SaveMediaOptions");
                Equal(0, Convert.ToInt32(Get(details, "VideoPreset")));
            }
        });
        Test("N001.ConverterProfileDropdownMatchesEnum", () => {
            using (Form form = (Form)New("youtube_dl_gui.frmExtendedConverter")) {
                object details = New("youtube_dl_gui.ExtendedConversionDetails", "input.mkv");
                Field(form, "SelectedConversion", details);
                ((CheckBox)Field(form, "chkVideoSetProfile")).Checked = true;
                ComboBox combo = (ComboBox)Field(form, "cbVideoProfile");
                for (int index = 0; index < combo.Items.Count; index++) {
                    combo.SelectedIndex = index;
                    Call(form.GetType(), form, "SaveMediaOptions");
                    Equal(index + 1, Convert.ToInt32(Get(details, "VideoProfile")));
                }
                combo.SelectedIndex = -1;
                Call(form.GetType(), form, "SaveMediaOptions");
                Equal(0, Convert.ToInt32(Get(details, "VideoProfile")));
            }
        });
        Test("N001.ConverterOptionsRoundTrip", () => {
            using (Form form = (Form)New("youtube_dl_gui.frmExtendedConverter")) {
                object details = New("youtube_dl_gui.ExtendedConversionDetails", "input.mkv");
                Field(form, "SelectedConversion", details);
                Set(details.GetType(), details, "VideoPreset", Enum.ToObject(T("youtube_dl_gui.VideoPresets"), 9));
                Set(details.GetType(), details, "VideoProfile", Enum.ToObject(T("youtube_dl_gui.VideoProfiles"), 6));
                Set(details.GetType(), details, "AudioSampleRate", Enum.ToObject(T("youtube_dl_gui.AudioSampleRates"), 5));
                Call(form.GetType(), form, "LoadMediaOptions");
                Equal(8, ((ComboBox)Field(form, "cbVideoPreset")).SelectedIndex);
                Equal(5, ((ComboBox)Field(form, "cbVideoProfile")).SelectedIndex);
                Equal(5, ((ComboBox)Field(form, "cbAudioSampleRate")).SelectedIndex);
                Equal(true, ((CheckBox)Field(form, "chkVideoSetPreset")).Checked);
                Equal(true, ((CheckBox)Field(form, "chkVideoSetProfile")).Checked);
                Equal(true, ((CheckBox)Field(form, "chkAudioSampleRate")).Checked);
                Call(form.GetType(), form, "SaveMediaOptions");
                Equal(9, Convert.ToInt32(Get(details, "VideoPreset")));
                Equal(6, Convert.ToInt32(Get(details, "VideoProfile")));
                Equal(5, Convert.ToInt32(Get(details, "AudioSampleRate")));
            }
        });
        Test("N002.ConverterSlowPresetIsValid", () => {
            object details = New("youtube_dl_gui.ExtendedConversionDetails", "input.mkv");
            Set(details.GetType(), details, "OutputFilePath", "output.mkv");
            Set(details.GetType(), details, "InfoRetrieved", true);
            Set(details.GetType(), details, "VideoPreset", Enum.ToObject(T("youtube_dl_gui.VideoPresets"), 7));
            object stream = New("youtube_dl_gui.FfprobeSubdata+Stream");
            ((IList)Get(details, "VideoStreams")).Add(stream);
            ((IList)Get(details, "VideoItems")).Add(new ListViewItem("video") { Checked = true, Tag = stream });
            Require((bool)Call(details.GetType(), details, "GenerateArguments"), "Conversion arguments were not generated");
            string args = (string)Get(details, "Arguments");
            Require(args.Contains("-preset slow "), "The slow preset is not emitted correctly: " + args);
        });
    }
}

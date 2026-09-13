// Characterization probes, not regression-pass claims: exit 42 means the named defect was observed.
// Each case runs in a separate process and disposable directory. Never run against a user installation.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class ReviewObservations {
    static Assembly app;
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type T(string name) { return app.GetType("youtube_dl_gui." + name, true); }
    static object New(Type type, params object[] args) { return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null); }
    static object Get(object target, string name) { return target.GetType().GetProperty(name, Flags).GetValue(target, null); }
    static void Set(object target, string name, object value) { target.GetType().GetProperty(name, Flags).SetValue(target, value, null); }
    static void SetStatic(string type, string name, object value) { T(type).GetProperty(name, Flags).SetValue(null, value, null); }
    static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    static object Call(object target, string name, params object[] args) {
        return target.GetType().GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    }
    static int Observed(bool condition, string detail) { Console.WriteLine((condition ? "OBSERVED " : "NOT_OBSERVED ") + detail); return condition ? 42 : 43; }
    [STAThread]
    static int Main(string[] args) {
        if (args.Length != 2) return 44;
        try {
            // Subscribe before the application's diagnostic handler can open a modal dialog.
            if (args[1] == "batch-quotes") AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                Console.WriteLine("OBSERVED unhandled batch worker: " + e.ExceptionObject);
                Console.Out.Flush();
                Environment.Exit(e.ExceptionObject is ArgumentNullException ? 42 : 44);
            };
            app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            File.WriteAllText("youtube-dl-gui.ini", "[youtube-dl-gui]\r\nfirstTime=False\r\nCheckForUpdatesOnLaunch=False\r\nAutoUpdateYoutubeDl=False\r\n");
            T("Language").GetMethod("LoadInternalEnglish", Flags).Invoke(null, null);
            switch (args[1]) {
                case "batch-quotes": {
                    Form form = (Form)New(T("frmBatchDownloader"));
                    IntPtr handle = form.Handle;
                    ((ComboBox)Field(form, "cbBatchDownloadType")).SelectedIndex = 0;
                    Call(form, "AddItemToList", "\"\"");
                    Console.WriteLine("Queued rows: " + ((ListView)Field(form, "lvBatchDownloadQueue")).Items.Count);
                    Call(form, "btnBatchDownloadStartStopExit_Click", form, EventArgs.Empty);
                    for (int i = 0; i < 200; i++) { Application.DoEvents(); Thread.Sleep(10); }
                    return 43;
                }
                case "ini-retry": {
                    PropertyInfo option = T("General").GetProperty("UseStaticYtdl", Flags);
                    bool before = (bool)option.GetValue(null, null);
                    string ini = (string)T("IniProvider").GetField("IniPath", Flags).GetValue(null);
                    bool failed = false;
                    File.SetAttributes(ini, FileAttributes.ReadOnly);
                    try { option.SetValue(null, !before, null); }
                    catch (TargetInvocationException e) { failed = e.InnerException is IOException; }
                    finally { File.SetAttributes(ini, FileAttributes.Normal); }
                    option.SetValue(null, !before, null);
                    string text = File.ReadAllText(ini);
                    return Observed(failed && (bool)option.GetValue(null, null) != before && !text.Contains("UseStaticYtdl="), "Write failed=" + failed + "; retry content=" + text);
                }
                case "error-logging-preference": {
                    SetStatic("Errors", "logErrors", true);
                    bool enabled = (bool)app.GetType("murrty.logging.Log", true).GetProperty("AllowWritingToFile", Flags).GetValue(null, null);
                    return Observed(!enabled, "Errors.logErrors=true; Log.AllowWritingToFile=" + enabled);
                }
                case "numeric-after-arrow": {
                    using (Control box = (Control)New(app.GetType("murrty.controls.ExtendedTextBox", true))) {
                        Set(box, "TextType", Enum.Parse(app.GetType("murrty.controls.AllowedCharacters", true), "NumericOnly"));
                        Call(box, "OnKeyDown", new KeyEventArgs(Keys.Left));
                        Call(box, "OnKeyDown", new KeyEventArgs(Keys.D1));
                        KeyPressEventArgs press = new KeyPressEventArgs('1');
                        Call(box, "OnKeyPress", press);
                        return Observed(press.Handled, "Digit after navigation suppressed=" + press.Handled);
                    }
                }
                case "version-format": {
                    Type version = app.GetType("murrty.updater.Version", true);
                    object[] parse = { "1-2-3", Activator.CreateInstance(version) };
                    bool accepted = (bool)version.GetMethod("TryParse", Flags).Invoke(null, parse);
                    return Observed(accepted, "Malformed 1-2-3 accepted=" + accepted + "; value=" + parse[1]);
                }
                case "duration-rounding": {
                    object data = New(T("YoutubeDlData"));
                    Set(data, "DurationTime", 59.9m);
                    string duration = (string)Get(data, "Duration");
                    return Observed(duration == "0:60", "59.9 seconds rendered as " + duration);
                }
                case "duration-budget": {
                    object data = New(T("YoutubeDlData"));
                    Set(data, "DurationTime", 1000000000000m);
                    Console.WriteLine("ENTER_DURATION_GETTER"); Console.Out.Flush();
                    Console.WriteLine(Get(data, "Duration"));
                    return 43;
                }
                case "time-span": {
                    using (Control picker = (Control)New(app.GetType("murrty.controls.TimePicker", true))) {
                        Set(picker, "DateBasedTime", false);
                        Set(picker, "TimeSpan", TimeSpan.FromHours(25));
                        double hours = ((TimeSpan)Get(picker, "TimeSpan")).TotalHours;
                        return Observed(hours == 1, "25-hour TimeSpan round trip = " + hours);
                    }
                }
                case "time-width": {
                    using (Control picker = (Control)New(app.GetType("murrty.controls.TimePicker", true))) {
                        Set(picker, "DateBasedTime", false);
                        Call(picker, "SetValue", 100, 12, 34, 0);
                        int width = (int)Get(picker, "HourToMinuteSeparator");
                        return Observed(width == 2, "100-hour field separator = " + width);
                    }
                }
                case "time-malformed": {
                    using (Control picker = (Control)New(app.GetType("murrty.controls.TimePicker", true))) {
                        ((TextBox)Field(picker, "TimeDisplay")).Text = "1";
                        try { Call(picker, "UpdateControl"); }
                        catch (TargetInvocationException e) { return Observed(e.InnerException is IndexOutOfRangeException, e.InnerException.ToString()); }
                        return 43;
                    }
                }
                case "format-boundary": {
                    object media = New(T("ExtendedMediaDetails"), "https://example.invalid/fixture");
                    object format = New(T("YoutubeDlSubdata+Format"));
                    Set(format, "Identifier", "fixture --print AUDIT_BOUNDARY");
                    ListViewItem item = new ListViewItem("fixture"); item.Tag = format;
                    Set(media, "SelectedType", Enum.Parse(T("DownloadType"), "Video"));
                    Set(media, "SelectedVideoItem", item); Set(media, "VideoDownloadAudio", false);
                    if (!(bool)Call(media, "GenerateArguments")) return 44;
                    string command = (string)Get(media, "Arguments");
                    return Observed(command.Contains("-f fixture --print AUDIT_BOUNDARY/best"), command);
                }
                case "configured-schema": {
                    const string wanted = "AUDIT_%(id)s.%(ext)s";
                    SetStatic("Downloads", "fileNameSchema", wanted);
                    using (Form form = (Form)New(T("frmExtendedDownloader"), "https://example.invalid/fixture", false)) {
                        object media = Get(form, "MediaDetails");
                        Set(media, "InfoRetrieved", true);
                        Set(media, "SelectedType", Enum.Parse(T("DownloadType"), "Custom"));
                        string before = ((ComboBox)Field(form, "cbSchema")).Text;
                        Call(form, "SelectedMediaChanged", media);
                        string after = ((ComboBox)Field(form, "cbSchema")).Text;
                        return Observed(before == wanted && after != wanted, "Before=" + before + "; After=" + after);
                    }
                }
            }
            return 44;
        } catch (Exception e) { Console.WriteLine("HARNESS_ERROR " + e); return 44; }
    }
}

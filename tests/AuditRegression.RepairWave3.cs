using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void MergerUsesSelectedInputPath() {
        using (Form form = (Form)New("youtube_dl_gui.frmMerger")) {
            object data = New("youtube_dl_gui.FfprobeData");
            string intended = Path.Combine(Environment.CurrentDirectory, "selected-input.mp4");
            Set(data.GetType(), data, "FilePath", intended);
            object stream = New("youtube_dl_gui.FfprobeSubdata+Stream");
            Set(stream.GetType(), stream, "index", 0);
            object tag = New("youtube_dl_gui.FfprobeNodeTag", data, stream);
            TreeView selected = (TreeView)Field(form, "tvSelectedStreams");
            TreeNode node = new TreeNode("audit") { Tag = tag };
            selected.Nodes[0].Nodes.Add(node);
            string args = (string)Call(form.GetType(), form, "GenerateList");
            Require(args.Contains("-i \"" + intended + "\""), "Merge input did not use the selected file path: " + args);
        }
    }

    private static bool BrushIsDisposed(System.Drawing.Brush brush) {
        try {
            using (System.Drawing.Brush clone = (System.Drawing.Brush)brush.Clone()) { }
            return false;
        }
        catch { return true; }
    }

    private static void ProgressBrushesAreOwned() {
        System.Drawing.Brush systemText = System.Drawing.SystemBrushes.ControlText;
        System.Drawing.Brush systemShadow = System.Drawing.SystemBrushes.ControlDark;
        Control control = (Control)New("murrty.controls.ExtendedProgressBar");
        try {
            Set(control.GetType(), control, "ForeColor", System.Drawing.Color.Red);
            System.Drawing.Brush firstText = (System.Drawing.Brush)Field(control, "TextBrush");
            Set(control.GetType(), control, "ForeColor", System.Drawing.Color.Blue);
            Require(BrushIsDisposed(firstText), "Replaced text brush was not disposed");

            Set(control.GetType(), control, "DropShadowColor", System.Drawing.Color.Red);
            System.Drawing.Brush firstShadow = (System.Drawing.Brush)Field(control, "DropShadowBrush");
            Set(control.GetType(), control, "DropShadowColor", System.Drawing.Color.Blue);
            Require(BrushIsDisposed(firstShadow), "Replaced shadow brush was not disposed");

            System.Drawing.Brush lastText = (System.Drawing.Brush)Field(control, "TextBrush");
            System.Drawing.Brush lastShadow = (System.Drawing.Brush)Field(control, "DropShadowBrush");
            control.Dispose();
            Require(BrushIsDisposed(lastText), "Final owned text brush was not disposed with the control");
            Require(BrushIsDisposed(lastShadow), "Final owned shadow brush was not disposed with the control");
            Require(!BrushIsDisposed(systemText) && !BrushIsDisposed(systemShadow), "A shared SystemBrush was disposed");
        }
        finally { control.Dispose(); }
    }

    private static void FolderBrowserDisposesOwnedDialog() {
        IDisposable browser = (IDisposable)New("BetterFolderBrowserNS.BetterFolderBrowser");
        object helper = Field(browser, "_dialog");
        Require(helper is IDisposable, "Owned folder-dialog helper is not disposable");
        object ofd = helper.GetType().GetField("ofd", All).GetValue(helper);
        int disposed = 0;
        ((System.ComponentModel.Component)ofd).Disposed += delegate { disposed++; };
        browser.Dispose();
        Equal(1, disposed);
    }

    private static void UpdaterFailureReturnsNonZero() {
        string root = Directory.GetParent(Path.GetDirectoryName(App.Location)).Parent.Parent.FullName;
        Assembly assembly = Assembly.LoadFrom(Path.Combine(root, "youtube-dl-gui-updater", "bin", "Release", "youtube-dl-gui-updater.exe"));
        Type program = assembly.GetType("youtube_dl_gui_updater.Program", true);
        Type formType = assembly.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Type updateType = assembly.GetTypes().Single(t => t.Name == "UpdateData");
        Call(assembly.GetType("youtube_dl_gui_updater.Language", true), null, "LoadInternalEnglish");
        Set(program, null, "ExitCode", 0);
        Set(program, null, "DownloadClient", null);
        object data = Activator.CreateInstance(updateType);
        Set(updateType, data, "FileName", Path.Combine(Environment.CurrentDirectory, "audit-updater-target.exe"));
        Set(updateType, data, "NewVersion", new Version(99, 0));
        Set(updateType, data, "UpdateHash", new string('0', 64));
        using (Form form = (Form)Activator.CreateInstance(formType, true)) {
            Field(form, "UpdateData", data);
            Task task = (Task)Call(formType, form, "RunUpdate");
            PumpUntil(() => task.IsCompleted, 5000, "Updater failure path did not return");
            task.GetAwaiter().GetResult();
            Require((int)program.GetProperty("ExitCode", All).GetValue(null, null) != 0, "Failed updater run returned process success");
        }
    }

    private static void ProviderUpdateStartFailureIsContained() {
        Type verification = T("youtube_dl_gui.Verification");
        string previous = (string)verification.GetProperty("YoutubeDlPath", All).GetValue(null, null);
        string fake = Path.Combine(Environment.CurrentDirectory, "invalid-provider.exe");
        File.WriteAllText(fake, "not a Windows executable");
        try {
            Set(verification, null, "YoutubeDlPath", fake);
            Equal(false, Call(T("youtube_dl_gui.Updater"), null, "UpdateYoutubeDl", true, null));
        }
        finally {
            Set(verification, null, "YoutubeDlPath", previous);
            File.Delete(fake);
        }
    }

    private static void EmptyMergerDropIsIgnored() {
        using (Form form = (Form)New("youtube_dl_gui.frmMerger")) {
            DataObject data = new DataObject();
            data.SetData(DataFormats.FileDrop, new string[0]);
            DragEventArgs args = new DragEventArgs(data, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.Copy);
            Call(form.GetType(), form, "frmMerger_DragEnter", form, args);
            Equal(DragDropEffects.None, args.Effect);
        }
    }

    private static void InvalidConversionPathIsContained() {
        object info = New("youtube_dl_gui.ConvertInfo", "bad\0input", "output.mp4");
        using (Form form = (Form)New("youtube_dl_gui.frmConverter", info)) {
            Call(form.GetType(), form, "BeginConversion");
            Equal(Enum.Parse(T("youtube_dl_gui.ConversionStatus"), "ProgramError"), Get(info, "Status"));
        }
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();

    private static void ClipboardContentionIsContained() {
        ManualResetEvent opened = new ManualResetEvent(false);
        ManualResetEvent release = new ManualResetEvent(false);
        Thread holder = new Thread(delegate() {
            if (!OpenClipboard(IntPtr.Zero)) return;
            opened.Set();
            release.WaitOne();
            CloseClipboard();
        });
        holder.IsBackground = true;
        holder.SetApartmentState(ApartmentState.STA);
        holder.Start();
        Require(opened.WaitOne(2000), "Could not acquire clipboard for contention fixture");
        try {
            foreach (string kind in new[] { "NumericOnly", "AlphabeticalOnly", "AlphaNumericOnly" }) {
                using (Control box = (Control)New("murrty.controls.ExtendedTextBox")) {
                    Set(box.GetType(), box, "TextType", Enum.Parse(T("murrty.controls.AllowedCharacters"), kind));
                    Call(box.GetType(), box, "OnKeyDown", new KeyEventArgs(Keys.Control | Keys.V));
                }
            }
        }
        finally {
            release.Set();
            Require(holder.Join(2000), "Clipboard fixture did not release the clipboard");
            opened.Dispose();
            release.Dispose();
        }
    }

    static partial void RunRepairWave3Tests() {
        Test("E2E_O026.MergerUsesSelectedInputPath", MergerUsesSelectedInputPath);
        Test("E2E_O031.EmptyMergerDropIsIgnored", EmptyMergerDropIsIgnored);
        Test("E2E_O030.InvalidConversionPathIsContained", InvalidConversionPathIsContained);
        Test("E2E_O037.ClipboardContentionIsContained", ClipboardContentionIsContained);
        Test("E2E_O038.ProviderUpdateStartFailureIsContained", ProviderUpdateStartFailureIsContained);
        Test("CR_O023.UpdaterFailureReturnsNonZero", UpdaterFailureReturnsNonZero);
        Test("E2E_O039.ProgressBrushesAreOwned", ProgressBrushesAreOwned);
        Test("E2E_O040.FolderBrowserDisposesOwnedDialog", FolderBrowserDisposesOwnedDialog);
    }
}

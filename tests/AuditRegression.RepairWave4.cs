using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private sealed class SlowMessageForm : Form {
        internal const int SlowMessage = 0x5A11;
        protected override void WndProc(ref Message m) {
            if (m.Msg == SlowMessage) Thread.Sleep(1500);
            base.WndProc(ref m);
        }
    }

    private static void WebLaunchTargetsAreRestricted() {
        Type program = T("youtube_dl_gui.Program");
        foreach (string value in new[] { "https://example.invalid/path?q=1", "http://example.invalid/" })
            Equal(true, Call(program, null, "IsWebUrl", value));
        foreach (string value in new[] { "file:///C:/Windows/System32/calc.exe", "C:\\Windows\\notepad.exe", "\\\\server\\share\\file.txt", "javascript:alert(1)", "custom:payload", "ytsearch:a.b", "not a uri", "" })
            Equal(false, Call(program, null, "IsWebUrl", value));
    }

    private static void SecondInstanceMessageSendIsBounded() {
        SlowMessageForm form = null;
        Exception threadFailure = null;
        using (ManualResetEvent ready = new ManualResetEvent(false)) {
            Thread thread = new Thread(delegate() {
                try {
                    form = new SlowMessageForm();
                    IntPtr handle = form.Handle;
                    ready.Set();
                    Application.Run(form);
                }
                catch (Exception ex) {
                    threadFailure = ex;
                    ready.Set();
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Require(ready.WaitOne(3000), "Slow IPC window did not initialize");
            Require(threadFailure == null && form != null, "Slow IPC fixture failed to initialize");
            try {
                Stopwatch watch = Stopwatch.StartNew();
                object result = Call(T("youtube_dl_gui_shared.CopyData"), null, "TrySendMessage", form.Handle, SlowMessageForm.SlowMessage, IntPtr.Zero, IntPtr.Zero, 100u);
                watch.Stop();
                Equal(false, result);
                Require(watch.ElapsedMilliseconds < 1000, "Message timeout remained unbounded: " + watch.ElapsedMilliseconds + " ms");
            }
            finally {
                if (form != null && form.IsHandleCreated && !form.IsDisposed) {
                    try { form.BeginInvoke((Action)delegate { form.Close(); }); } catch { }
                }
                Require(thread.Join(4000), "Slow IPC fixture did not terminate");
                if (form != null) form.Dispose();
            }
        }
    }

    private static void ArgsFileIoIsContained() {
        string path = Path.Combine(Environment.CurrentDirectory, "args.txt");
        File.WriteAllLines(path, new[] { "--one", "--two value" });
        Type custom = T("youtube_dl_gui.CustomArguments");
        using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            object[] read = { null, null };
            Equal(false, Call(custom, null, "TryReadArgsFile", read));
            Require(((string)read[1]).IndexOf("args.txt", StringComparison.OrdinalIgnoreCase) >= 0, "Read failure did not identify args.txt");
            object[] write = { new[] { "--replacement" }, null };
            Equal(false, Call(custom, null, "TryWriteArgsFile", write));
            Require(((string)write[1]).IndexOf("args.txt", StringComparison.OrdinalIgnoreCase) >= 0, "Write failure did not identify args.txt");
        }
        object[] normalRead = { null, null };
        Equal(true, Call(custom, null, "TryReadArgsFile", normalRead));
        string[] values = (string[])normalRead[0];
        Equal(2, values.Length);
        Equal("--one", values[0]);
        Equal("--two value", values[1]);
    }

    private static void CookieHelpLinkHasAction() {
        MethodInfo handler = T("youtube_dl_gui.frmAuthentication").GetMethod("llCookiesFromBrowserHint_LinkClicked", All);
        Require(handler != null, "Cookies-from-browser help handler is missing");
        byte[] il = handler.GetMethodBody().GetILAsByteArray();
        Require(il != null && il.Length > 1, "Cookies-from-browser help handler is still empty");
    }

    private static void ArbitraryFileImportIsAtomic() {
        string path = Path.Combine(Environment.CurrentDirectory, "batch-import.txt");
        File.WriteAllText(path, "https://example.invalid/one\r\nhttps://example.invalid/two\r\n");
        using (Form form = (Form)New("youtube_dl_gui.frmBatchDownloader")) {
            ListView queue = (ListView)Field(form, "lvBatchDownloadQueue");
            ((ComboBox)Field(form, "cbBatchDownloadType")).SelectedIndex = 0;
            using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                object[] blocked = { path, null };
                Equal(false, Call(form.GetType(), form, "TryImportLinksFromFile", blocked));
                Equal(0, queue.Items.Count);
                Require(((string)blocked[1]).IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0, "Import failure did not identify the selected file");
            }
            object[] normal = { path, null };
            Equal(true, Call(form.GetType(), form, "TryImportLinksFromFile", normal));
            Equal(2, queue.Items.Count);
        }
    }

    private static void ProtocolPreservesNestedUrlEncoding() {
        string encodedUrl = "https://example.invalid/a%2Fb?q=one%25two&quoted=%22value%22&space=one%20two";
        string[] args = { "ytdlgui:v%20%22" + encodedUrl + "%22" };
        IList parsed = (IList)Call(T("youtube_dl_gui.Arguments"), null, "RetrieveArguments", (object)args);
        Equal(1, parsed.Count);
        object item = parsed[0];
        object type = item.GetType().GetField("Item1").GetValue(item);
        string data = (string)item.GetType().GetField("Item2").GetValue(item);
        Equal(Enum.Parse(T("youtube_dl_gui.ArgumentType"), "DownloadVideo"), type);
        Equal(encodedUrl, data);
    }

    static partial void RunRepairWave4Tests() {
        Test("E2E_O012_O042.WebLaunchTargetsAreRestricted", WebLaunchTargetsAreRestricted);
        Test("E2E_O013.SecondInstanceMessageSendIsBounded", SecondInstanceMessageSendIsBounded);
        Test("E2E_O025.ArgsFileIoIsContained", ArgsFileIoIsContained);
        Test("E2E_O028.CookieHelpLinkHasAction", CookieHelpLinkHasAction);
        Test("E2E_O033.ArbitraryFileImportIsAtomic", ArbitraryFileImportIsAtomic);
        Test("CURRENT_O036.ProtocolPreservesNestedUrlEncoding", ProtocolPreservesNestedUrlEncoding);
    }
}

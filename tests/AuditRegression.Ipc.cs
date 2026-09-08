using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object CopyHeader(long kind, int bytes, IntPtr pointer) {
        object header = New("youtube_dl_gui_shared.CopyDataStruct");
        Field(header, "dwData", new IntPtr(kind));
        Field(header, "cbData", bytes);
        Field(header, "lpData", pointer);
        return header;
    }
    private static bool HeaderAccepted(long kind, int bytes, IntPtr pointer) {
        return (bool)Call(T("youtube_dl_gui.Program"), null, "IsValidDownloadCopyData", CopyHeader(kind, bytes, pointer));
    }
    private static object ReadUpdatePacket(Type parameter, object data) {
        MethodInfo method = T("youtube_dl_gui_shared.CopyData").GetMethod("GetUpdateData", All, null, new[] { parameter }, null);
        try { return method.Invoke(null, new[] { data }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    private static object UpdatePacket(string name, string hash) {
        object version = New("murrty.updater.Version", (byte)3, (byte)3, (byte)0, (byte)2);
        return New("youtube_dl_gui_shared.UpdateData", name, version, hash);
    }
    private static void UpdatePacketRoundTrip(string name) {
        object original = UpdatePacket(name, new string('a', 64));
        byte[] bytes = (byte[])Call(T("youtube_dl_gui_shared.CopyData"), null, "GetUpdateBytes", original);
        IntPtr data = Marshal.AllocHGlobal(bytes.Length);
        IntPtr header = IntPtr.Zero;
        try {
            Marshal.Copy(bytes, 0, data, bytes.Length);
            object value = CopyHeader(1, bytes.Length, data);
            header = Marshal.AllocHGlobal(Marshal.SizeOf(value));
            Marshal.StructureToPtr(value, header, false);
            object roundTrip = ReadUpdatePacket(typeof(IntPtr), header);
            Equal(name, Get(roundTrip, "FileName"));
            Equal(Get(original, "UpdateHash"), Get(roundTrip, "UpdateHash"));
            Equal(Get(original, "NewVersion"), Get(roundTrip, "NewVersion"));
        }
        finally { if (header != IntPtr.Zero) Marshal.FreeHGlobal(header); Marshal.FreeHGlobal(data); }
    }
    static partial void RunIpcTests() {
        // Header checks never dereference this sentinel. No foreign process or unsafe read is used.
        Test("N006.DownloadIpcAcceptsSupportedUtf16Packets", () => {
            for (long kind = 1; kind <= 9; kind++) {
                Require(HeaderAccepted(kind, 2, new IntPtr(1)), "Supported IPC kind was rejected");
                Require(HeaderAccepted(kind, 1024 * 1024, new IntPtr(1)), "Boundary-sized IPC packet was rejected");
            }
        });
        Test("N006.DownloadIpcRejectsMalformedHeadersBeforeAllocation", () => {
            foreach (int length in new[] { -1, 0, 1, 3, 1024 * 1024 + 2, int.MaxValue })
                Require(!HeaderAccepted(1, length, new IntPtr(1)), "Malformed length accepted: " + length);
            Require(!HeaderAccepted(1, 2, IntPtr.Zero), "Null data pointer accepted");
            foreach (long kind in new long[] { -1, 0, 10, 12, int.MaxValue })
                Require(!HeaderAccepted(kind, 2, new IntPtr(1)), "Unsupported IPC kind accepted");
            if (IntPtr.Size == 8) Require(!HeaderAccepted(0x100000001L, 2, new IntPtr(1)), "Oversized IPC kind was truncated");
        });
        Test("N007.UpdatePacketPreservesLastFilenameByte", () => UpdatePacketRoundTrip("custom-name.exe"));
        Test("N007.UpdatePacketPreservesFinalUnicodeCharacter", () => UpdatePacketRoundTrip("custom-name-\u03bb"));
        Test("N007.UpdatePacketRejectsTruncatedLayout", () => {
            Throws<ArgumentException>(() => ReadUpdatePacket(typeof(byte[]), new byte[67]));
            Throws<ArgumentNullException>(() => ReadUpdatePacket(typeof(byte[]), null));
        });
        Test("N007.UpdatePacketWriterRequiresFixedHashWidth", () => {
            foreach (int length in new[] { 0, 63, 65 }) {
                object data = UpdatePacket("custom.exe", new string('a', length));
                Throws<ArgumentException>(() => Call(T("youtube_dl_gui_shared.CopyData"), null, "GetUpdateBytes", data));
            }
        });
    }
    private sealed class AcknowledgingUpdater : NativeWindow, IDisposable {
        private readonly Form parent;
        internal bool Received;
        internal bool GateWasArmed;
        internal AcknowledgingUpdater(Form parent) { this.parent = parent; CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x004A) {
                Received = true;
                GateWasArmed = (bool)Get(parent, "CanUpdate");
                // Reproduce the real stub's synchronous nested acknowledgement.
                SendMessage(parent.Handle, 0x1002, IntPtr.Zero, IntPtr.Zero);
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    static partial void RunUpdaterHandshakeTests() {
        Test("N008.UpdaterSynchronousAcknowledgementClosesMainForm", () => {
            object previous = T("youtube_dl_gui.Updater").GetProperty("LastChecked", All).GetValue(null, null);
            object mainBefore = T("youtube_dl_gui.Program").GetProperty("MainForm", All).GetValue(null, null);
            using (Form main = (Form)New("youtube_dl_gui.frmMain"))
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
            using (AcknowledgingUpdater updater = new AcknowledgingUpdater(handler)) {
                try {
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", main);
                    object release = New("murrty.updater.GithubData");
                    Set(release.GetType(), release, "Version", New("murrty.updater.Version", (byte)3, (byte)3, (byte)0, (byte)2));
                    Set(release.GetType(), release, "ExecutableHash", new string('a', 64));
                    Set(T("youtube_dl_gui.Updater"), null, "LastChecked", release);
                    SendMessage(handler.Handle, 0x1001, updater.Handle, IntPtr.Zero);
                    Require(updater.Received && updater.GateWasArmed, "Updater acknowledgement arrived before the receiver was ready");
                    Require(main.IsDisposed, "The update handshake did not close the main form");
                    Require(!(bool)Get(handler, "CanUpdate"), "Update acknowledgement gate remained armed");
                }
                finally {
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", mainBefore);
                    Set(T("youtube_dl_gui.Updater"), null, "LastChecked", previous);
                }
            }
        });
        Test("N008.UnsolicitedUpdaterAcknowledgementIsIgnored", () => {
            object previous = T("youtube_dl_gui.Program").GetProperty("MainForm", All).GetValue(null, null);
            using (Form main = (Form)New("youtube_dl_gui.frmMain"))
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler")) {
                try {
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", main);
                    SendMessage(handler.Handle, 0x1002, IntPtr.Zero, IntPtr.Zero);
                    Require(!main.IsDisposed, "An unsolicited updater acknowledgement closed the main form");
                }
                finally { Set(T("youtube_dl_gui.Program"), null, "MainForm", previous); }
            }
        });
    }
}

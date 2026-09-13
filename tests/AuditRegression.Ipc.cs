using System;
using System.Diagnostics;
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

    private static object AuditRelease() {
        object release = New("murrty.updater.GithubData");
        Set(release.GetType(), release, "Version", New("murrty.updater.Version", (byte)3, (byte)3, (byte)0, (byte)2));
        Set(release.GetType(), release, "ExecutableHash", new string('a', 64));
        return release;
    }

    private static PropertyInfo ExpectedUpdaterProcessIdProperty() {
        return T("youtube_dl_gui.Updater").GetProperty("ExpectedUpdaterProcessId", All);
    }

    private sealed class PassiveUpdater : NativeWindow, IDisposable {
        internal bool Received;
        internal PassiveUpdater() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x004A) {
                Received = true;
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
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
                // Reproduce the real stub's synchronous nested acknowledgement and identify this exact requester window.
                SendMessage(parent.Handle, 0x1002, Handle, IntPtr.Zero);
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }

    private sealed class HandleOnlyWindow : NativeWindow, IDisposable {
        internal HandleOnlyWindow() { CreateHandle(new CreateParams()); }
        public void Dispose() { DestroyHandle(); }
    }

    private sealed class SpoofingUpdater : NativeWindow, IDisposable {
        private readonly Form parent;
        private readonly Form main;
        private readonly IntPtr spoofHandle;
        internal bool Received;
        internal bool GateWasArmed;
        internal bool SpoofWasAccepted;
        internal SpoofingUpdater(Form parent, Form main, IntPtr spoofHandle) {
            this.parent = parent;
            this.main = main;
            this.spoofHandle = spoofHandle;
            CreateHandle(new CreateParams());
        }
        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x004A) {
                Received = true;
                GateWasArmed = (bool)Get(parent, "CanUpdate");
                SendMessage(parent.Handle, 0x1002, spoofHandle, IntPtr.Zero);
                SpoofWasAccepted = main.IsDisposed;
                SendMessage(parent.Handle, 0x1002, Handle, IntPtr.Zero);
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }

    private sealed class UpdaterReadySink : NativeWindow, IDisposable {
        internal bool Received;
        internal IntPtr RequesterWindow;
        internal UpdaterReadySink() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x1002) {
                Received = true;
                RequesterWindow = message.WParam;
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
        Test("CURRENT_O007.UpdaterAcknowledgementIdentifiesRequesterWindow", () => {
            Assembly updaterAssembly = LoadUpdaterAssembly();
            Type formType = updaterAssembly.GetType("youtube_dl_gui_updater.frmUpdater", true);
            Type handlesType = updaterAssembly.GetType("youtube_dl_gui_shared.ApplicationHandles", true);
            object packet = UpdatePacket("custom.exe", new string('a', 64));
            IntPtr packetPointer = IntPtr.Zero;
            IntPtr headerPointer = IntPtr.Zero;
            using (UpdaterReadySink sink = new UpdaterReadySink())
            using (Form updater = (Form)Activator.CreateInstance(formType, true)) {
                try {
                    object handles = Activator.CreateInstance(
                        handlesType, All, null, new object[] { sink.Handle, Process.GetCurrentProcess().Id },
                        System.Globalization.CultureInfo.InvariantCulture);
                    Field(updater, "ApplicationData", handles);

                    int packetSize = Marshal.SizeOf(packet);
                    packetPointer = Marshal.AllocHGlobal(packetSize);
                    Marshal.StructureToPtr(packet, packetPointer, false);
                    object header = CopyHeader(1, packetSize, packetPointer);
                    headerPointer = Marshal.AllocHGlobal(Marshal.SizeOf(header));
                    Marshal.StructureToPtr(header, headerPointer, false);

                    SendMessage(updater.Handle, 0x004A, sink.Handle, headerPointer);
                    Require(sink.Received, "The standalone updater did not acknowledge valid update data");
                    Equal(updater.Handle, sink.RequesterWindow);
                }
                finally {
                    if (headerPointer != IntPtr.Zero) Marshal.FreeHGlobal(headerPointer);
                    if (packetPointer != IntPtr.Zero) {
                        Marshal.DestroyStructure(packetPointer, packet.GetType());
                        Marshal.FreeHGlobal(packetPointer);
                    }
                }
            }
        });

        Test("CURRENT_O007.UnlaunchedUpdaterRequestIsRejected", () => {
            Type updaterType = T("youtube_dl_gui.Updater");
            object previousRelease = updaterType.GetProperty("LastChecked", All).GetValue(null, null);
            PropertyInfo expectedPid = ExpectedUpdaterProcessIdProperty();
            object previousExpectedPid = expectedPid == null ? null : expectedPid.GetValue(null, null);
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
            using (PassiveUpdater fake = new PassiveUpdater()) {
                try {
                    if (expectedPid != null) expectedPid.SetValue(null, 0, null);
                    Set(updaterType, null, "LastChecked", AuditRelease());
                    SendMessage(handler.Handle, 0x1001, fake.Handle, IntPtr.Zero);
                    Require(!fake.Received, "An updater request was accepted even though the application had not launched an updater");
                    Require(!(bool)Get(handler, "CanUpdate"), "Rejected updater request left the acknowledgement gate armed");
                }
                finally {
                    Set(updaterType, null, "LastChecked", previousRelease);
                    if (expectedPid != null) expectedPid.SetValue(null, previousExpectedPid, null);
                }
            }
        });

        Test("CURRENT_O007.UnlaunchedRequestWithoutMetadataIsRejected", () => {
            Type updaterType = T("youtube_dl_gui.Updater");
            object previousRelease = updaterType.GetProperty("LastChecked", All).GetValue(null, null);
            PropertyInfo expectedPid = ExpectedUpdaterProcessIdProperty();
            object previousExpectedPid = expectedPid == null ? null : expectedPid.GetValue(null, null);
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
            using (PassiveUpdater fake = new PassiveUpdater()) {
                try {
                    if (expectedPid != null) expectedPid.SetValue(null, 0, null);
                    Set(updaterType, null, "LastChecked", null);
                    SendMessage(handler.Handle, 0x1001, fake.Handle, IntPtr.Zero);
                    Require(!fake.Received, "An unauthorized updater request was accepted without cached release metadata");
                    Require(!(bool)Get(handler, "CanUpdate"), "Unauthorized metadata-free request left the acknowledgement gate armed");
                }
                finally {
                    Set(updaterType, null, "LastChecked", previousRelease);
                    if (expectedPid != null) expectedPid.SetValue(null, previousExpectedPid, null);
                }
            }
        });

        Test("CURRENT_O007.WrongProcessUpdaterRequestIsRejected", () => {
            Type updaterType = T("youtube_dl_gui.Updater");
            object previousRelease = updaterType.GetProperty("LastChecked", All).GetValue(null, null);
            PropertyInfo expectedPid = ExpectedUpdaterProcessIdProperty();
            object previousExpectedPid = expectedPid == null ? null : expectedPid.GetValue(null, null);
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
            using (PassiveUpdater fake = new PassiveUpdater()) {
                try {
                    if (expectedPid != null) expectedPid.SetValue(null, int.MaxValue, null);
                    Set(updaterType, null, "LastChecked", AuditRelease());
                    SendMessage(handler.Handle, 0x1001, fake.Handle, IntPtr.Zero);
                    Require(!fake.Received, "An updater request from a window owned by the wrong process was accepted");
                    Require(!(bool)Get(handler, "CanUpdate"), "Wrong-process updater request left the acknowledgement gate armed");
                }
                finally {
                    Set(updaterType, null, "LastChecked", previousRelease);
                    if (expectedPid != null) expectedPid.SetValue(null, previousExpectedPid, null);
                }
            }
        });

        Test("CURRENT_O007.AcknowledgementIsBoundToRequesterWindow", () => {
            Type updaterType = T("youtube_dl_gui.Updater");
            object previousRelease = updaterType.GetProperty("LastChecked", All).GetValue(null, null);
            object mainBefore = T("youtube_dl_gui.Program").GetProperty("MainForm", All).GetValue(null, null);
            PropertyInfo expectedPid = ExpectedUpdaterProcessIdProperty();
            object previousExpectedPid = expectedPid == null ? null : expectedPid.GetValue(null, null);
            using (Form main = (Form)New("youtube_dl_gui.frmMain"))
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
            using (HandleOnlyWindow spoof = new HandleOnlyWindow())
            using (SpoofingUpdater updater = new SpoofingUpdater(handler, main, spoof.Handle)) {
                try {
                    if (expectedPid != null) expectedPid.SetValue(null, Process.GetCurrentProcess().Id, null);
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", main);
                    Set(updaterType, null, "LastChecked", AuditRelease());
                    SendMessage(handler.Handle, 0x1001, updater.Handle, IntPtr.Zero);
                    Require(updater.Received && updater.GateWasArmed, "Updater response did not enter the synchronous acknowledgement window");
                    Require(!updater.SpoofWasAccepted, "A different window could acknowledge another updater window's request");
                    Require(main.IsDisposed, "The bound updater requester could not acknowledge its own request");
                    Require(!(bool)Get(handler, "CanUpdate"), "Update acknowledgement gate remained armed");
                }
                finally {
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", mainBefore);
                    Set(updaterType, null, "LastChecked", previousRelease);
                    if (expectedPid != null) expectedPid.SetValue(null, previousExpectedPid, null);
                }
            }
        });

        Test("N008.UpdaterSynchronousAcknowledgementClosesMainForm", () => {
            Type updaterType = T("youtube_dl_gui.Updater");
            object previous = updaterType.GetProperty("LastChecked", All).GetValue(null, null);
            object mainBefore = T("youtube_dl_gui.Program").GetProperty("MainForm", All).GetValue(null, null);
            PropertyInfo expectedPid = ExpectedUpdaterProcessIdProperty();
            object previousExpectedPid = expectedPid == null ? null : expectedPid.GetValue(null, null);
            using (Form main = (Form)New("youtube_dl_gui.frmMain"))
            using (Form handler = (Form)New("youtube_dl_gui.MessageHandler"))
            using (AcknowledgingUpdater updater = new AcknowledgingUpdater(handler)) {
                try {
                    if (expectedPid != null) expectedPid.SetValue(null, Process.GetCurrentProcess().Id, null);
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", main);
                    Set(updaterType, null, "LastChecked", AuditRelease());
                    SendMessage(handler.Handle, 0x1001, updater.Handle, IntPtr.Zero);
                    Require(updater.Received && updater.GateWasArmed, "Updater acknowledgement arrived before the receiver was ready");
                    Require(main.IsDisposed, "The update handshake did not close the main form");
                    Require(!(bool)Get(handler, "CanUpdate"), "Update acknowledgement gate remained armed");
                }
                finally {
                    Set(T("youtube_dl_gui.Program"), null, "MainForm", mainBefore);
                    Set(updaterType, null, "LastChecked", previous);
                    if (expectedPid != null) expectedPid.SetValue(null, previousExpectedPid, null);
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

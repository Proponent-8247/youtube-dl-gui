using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private sealed class Wave14UpdaterSink : NativeWindow, IDisposable {
        internal int ReadyCount;
        internal Wave14UpdaterSink() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x1002) {
                ReadyCount++;
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }

    private static object Wave14UpdateData(Assembly updater, string fileName, string hash) {
        Type dataType = updater.GetType("youtube_dl_gui_updater.UpdateData", false);
        if (dataType == null) {
            foreach (Type candidate in updater.GetTypes()) {
                if (candidate.Name == "UpdateData") { dataType = candidate; break; }
            }
        }
        Require(dataType != null, "Updater UpdateData type was not found");
        object data = Activator.CreateInstance(dataType);
        Set(dataType, data, "FileName", fileName);
        Set(dataType, data, "UpdateHash", hash);
        Type versionType = updater.GetType("murrty.updater.Version", true);
        Set(dataType, data, "NewVersion", Activator.CreateInstance(versionType, new object[] { (byte)99, (byte)0 }));
        return data;
    }

    private static void SendWave14UpdatePacket(Form updater, IntPtr sender, object data) {
        IntPtr dataPointer = IntPtr.Zero;
        IntPtr headerPointer = IntPtr.Zero;
        try {
            int size = Marshal.SizeOf(data);
            dataPointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(data, dataPointer, false);
            object header = New("youtube_dl_gui_shared.CopyDataStruct");
            Field(header, "dwData", new IntPtr(1));
            Field(header, "cbData", size);
            Field(header, "lpData", dataPointer);
            headerPointer = Marshal.AllocHGlobal(Marshal.SizeOf(header));
            Marshal.StructureToPtr(header, headerPointer, false);
            SendMessage(updater.Handle, 0x004A, sender, headerPointer);
        }
        finally {
            if (headerPointer != IntPtr.Zero) Marshal.FreeHGlobal(headerPointer);
            if (dataPointer != IntPtr.Zero) {
                Marshal.DestroyStructure(dataPointer, data.GetType());
                Marshal.FreeHGlobal(dataPointer);
            }
        }
    }

    private static Form Wave14UpdaterForm(Assembly updater, Wave14UpdaterSink sink, int expectedProcessId) {
        Type formType = updater.GetType("youtube_dl_gui_updater.frmUpdater", true);
        Type handlesType = updater.GetType("youtube_dl_gui_shared.ApplicationHandles", true);
        Form form = (Form)Activator.CreateInstance(formType, true);
        object handles = Activator.CreateInstance(
            handlesType, All, null, new object[] { sink.Handle, expectedProcessId },
            System.Globalization.CultureInfo.InvariantCulture);
        Field(form, "ApplicationData", handles);
        return form;
    }

    private static void UpdaterCloseCancelsEveryPhase() {
        Assembly updater = LoadUpdaterAssembly();
        Type program = updater.GetType("youtube_dl_gui_updater.Program", true);
        object previous = program.GetProperty("CancelToken", All).GetValue(null, null);
        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        using (Form form = (Form)Activator.CreateInstance(updater.GetType("youtube_dl_gui_updater.frmUpdater", true), true)) {
            try {
                Set(program, null, "CancelToken", cancellation);
                Field(form, "WaitingForApplication", false);
                Call(form.GetType(), form, "OnFormClosing", new FormClosingEventArgs(CloseReason.UserClosing, false));
                Require(cancellation.IsCancellationRequested,
                    "Closing the updater outside the parent-wait phase did not cancel the update operation");
            }
            finally { Set(program, null, "CancelToken", previous); }
        }
    }

    private static void UpdaterUpdateDataIsOneShotAndTargetIsFrozen() {
        Assembly updater = LoadUpdaterAssembly();
        using (Wave14UpdaterSink sink = new Wave14UpdaterSink())
        using (Form form = Wave14UpdaterForm(updater, sink, Process.GetCurrentProcess().Id)) {
            string firstHash = new string('a', 64);
            string secondHash = new string('b', 64);
            SendWave14UpdatePacket(form, sink.Handle, Wave14UpdateData(updater, "custom.exe", firstHash));
            Equal(1, sink.ReadyCount);

            object accepted = Field(form, "UpdateData");
            string frozenTarget = (string)Get(accepted, "FileName");
            string frozenHash = (string)Get(accepted, "UpdateHash");
            Equal(firstHash, frozenHash);
            Require(Path.GetFullPath(frozenTarget) == Path.GetFullPath(Process.GetCurrentProcess().MainModule.FileName),
                "Updater did not freeze replacement to the expected parent executable: " + frozenTarget);

            SendWave14UpdatePacket(form, sink.Handle, Wave14UpdateData(updater, "other.exe", secondHash));
            Equal(1, sink.ReadyCount);
            accepted = Field(form, "UpdateData");
            Equal(frozenTarget, Get(accepted, "FileName"));
            Equal(frozenHash, Get(accepted, "UpdateHash"));
        }
    }

    private static void UpdaterRejectsPathLikeAndWrongProcessPackets() {
        Assembly updater = LoadUpdaterAssembly();
        using (Wave14UpdaterSink sink = new Wave14UpdaterSink()) {
            using (Form wrongProcess = Wave14UpdaterForm(updater, sink, int.MaxValue)) {
                SendWave14UpdatePacket(wrongProcess, sink.Handle,
                    Wave14UpdateData(updater, "custom.exe", new string('c', 64)));
                Equal(0, sink.ReadyCount);
            }

            using (Form pathLike = Wave14UpdaterForm(updater, sink, Process.GetCurrentProcess().Id)) {
                foreach (string name in new[] { "..\\other.exe", "C:\\temp\\other.exe", "nested/other.exe" }) {
                    SendWave14UpdatePacket(pathLike, sink.Handle,
                        Wave14UpdateData(updater, name, new string('d', 64)));
                }
                Equal(0, sink.ReadyCount);
            }
        }
    }

    private static void RunRepairWave14Tests() {
        Test("CURRENT_O011.UpdaterCloseCancelsEveryPhase", UpdaterCloseCancelsEveryPhase);
        Test("CURRENT_O021.UpdaterUpdateDataIsOneShotAndTargetIsFrozen", UpdaterUpdateDataIsOneShotAndTargetIsFrozen);
        Test("CURRENT_O021.UpdaterRejectsPathLikeAndWrongProcessPackets", UpdaterRejectsPathLikeAndWrongProcessPackets);
    }
}

#nullable enable
namespace youtube_dl_gui;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
public partial class MessageHandler : Form {
    public bool AcceptMessages { get; private set;} = false;
    public bool AwaitingExit { get; private set; } = false;
    private bool CanUpdate { get; set; } = false;
    private nint ExpectedUpdaterWindow { get; set; } = 0;

    public MessageHandler() {
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new(1, 1);
        this.ControlBox = false;
        this.Font = new("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
        this.FormBorderStyle = FormBorderStyle.None;
        this.Icon = Properties.Resources.ProgramIcon;
        this.Location = new(-31999, -31999);
        this.MaximumSize = new(1, 1);
        this.Name = Program.ProgramGUID;
        this.Opacity = 0D;
        this.ShowIcon = false;
        this.ShowInTaskbar = false;
        this.Text = Program.ProgramGUID;
        this.WindowState = FormWindowState.Minimized;

        this.Load += (s, e) => {
            AcceptMessages = true;
            Log.Write("Message queue handler loaded.");
            //Log.Write($"Handle: {Handle} (0x{(int)Handle:X})");
            //Log.Write($"ProcID: {Process.GetCurrentProcess().Id}");
        };
        this.Shown +=(s, e) => this.Hide();
    }
    public void AwaitExit() {
        AwaitingExit = true;
    }

    public void CheckExit() {
        if (this.IsDisposed) {
            return;
        }
        if (this.InvokeRequired) {
            try {
                this.BeginInvoke((Action)CheckExit);
            }
            catch (InvalidOperationException) { }
            return;
        }
        if (!AwaitingExit || Program.HasRunningActions)
            return;
        AcceptMessages = false;
        AwaitingExit = false;
        this.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint WindowHandle, out uint ProcessId);

    private static bool IsExpectedUpdaterWindow(nint WindowHandle) {
        if (WindowHandle == 0 || GetWindowThreadProcessId(WindowHandle, out uint ProcessId) == 0
        || ProcessId == 0 || ProcessId > int.MaxValue) {
            return false;
        }
        return Updater.IsExpectedUpdaterProcess((int)ProcessId);
    }

    [DebuggerStepThrough]
    protected override void WndProc(ref Message m) {
        switch (m.Msg) {
            case CopyData.WM_COPYDATA when AcceptMessages: {
                nint wp = m.WParam;
                // wParam should be the handle to the Window that sent the message.
                // Since WM_COPYDATA is overridden, I can DO WHAT I WANT.
                switch (wp) {
                    // 0x1 = The data was sent from another instance.
                    case 0x1: {
                        Program.ParseCopyData(ref m, null); // TODO: Extract the last selected custom argument from the saved config.
                        m.Result = IntPtr.Zero;
                    } break;

                    // No associated processing for this instance.
                    default: {
                        base.WndProc(ref m);
                    } break;
                }
            } break;

            // WM_SHOWFORM is a custom message.
            case CopyData.WM_SHOWFORM when AcceptMessages: {
                if (Program.MainForm is not null) {
                    Program.MainForm.Activate();
                }
                else if (Program.RunningActions.TryPeek(out Form RunningForm)) {
                    RunningForm.Show();
                }
                m.Result = IntPtr.Zero;
            } break;

            // WM_UPDATEDATAREQUEST is a custom message.
            // Only allowed when the updater is launched from youtube-dl-gui.
            case CopyData.WM_UPDATEDATAREQUEST: {
                if (!IsExpectedUpdaterWindow(m.WParam)) {
                    m.Result = IntPtr.Zero;
                    break;
                }
                if (Updater.LastChecked is null) {
                    Updater.ClearExpectedUpdaterProcess();
                    Log.MessageBox("LastChecked is null.");
                    m.Result = IntPtr.Zero;
                    break;
                }
                UpdateData Data = new() {
                    FileName = AppDomain.CurrentDomain.FriendlyName,
                    NewVersion = Updater.LastChecked.Version,
                    UpdateHash = Updater.LastChecked.ExecutableHash ?? new string('0', 64)
                };
                CopyDataStruct DataStruct = new();
                nint CopyDataBuffer = 0;
                nint DataBuffer = 0;
                ExpectedUpdaterWindow = m.WParam;
                try {
                    DataBuffer = CopyData.NintAlloc(Data);
                    DataStruct.cbData = Marshal.SizeOf(Data);
                    DataStruct.dwData = 1;
                    DataStruct.lpData = DataBuffer;
                    CopyDataBuffer = CopyData.NintAlloc(DataStruct);
                    // SendMessage is synchronous: the updater can acknowledge inside this call.
                    CanUpdate = true;
                    CopyData.SendMessage(m.WParam, CopyData.WM_COPYDATA, Handle, CopyDataBuffer);
                }
                finally {
                    CanUpdate = false;
                    ExpectedUpdaterWindow = 0;
                    Updater.ClearExpectedUpdaterProcess();
                    CopyData.NintFree(ref CopyDataBuffer);
                    CopyData.NintFree(ref DataBuffer);
                }
                m.Result = IntPtr.Zero;
            } break;

            // WM_UPDATERREADY is a custom message.
            // Only ran when WM_UPDATEDATAREQUEST has been called at least once.
            // Other cases, this is not a valid request.
            case CopyData.WM_UPDATERREADY: {
                if (CanUpdate && m.WParam == ExpectedUpdaterWindow) {
                    CanUpdate = false;
                    Updater.ClearExpectedUpdaterProcess();
                    Program.KillForUpdate();
                }
                m.Result = IntPtr.Zero;
            } break;

            default: {
                base.WndProc(ref m);
            } break;
        }
    }
}
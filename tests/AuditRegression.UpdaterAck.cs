using System;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void UnsolicitedUpdaterReadyCannotClaimParentAcceptance() {
        Type handler = T("youtube_dl_gui.MessageHandler");
        using (Form form = (Form)New("youtube_dl_gui.MessageHandler")) {
            Message message = Message.Create(form.Handle, 0x1002, new IntPtr(0x1234), IntPtr.Zero);
            object[] args = { message };
            Call(handler, form, "WndProc", args);
            Equal(IntPtr.Zero, ((Message)args[0]).Result);
            Require(!form.IsDisposed, "Unsolicited updater READY terminated the message handler");
        }
    }
}

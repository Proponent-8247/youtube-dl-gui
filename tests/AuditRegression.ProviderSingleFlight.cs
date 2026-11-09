using System;
using System.Reflection;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    private static void ProviderChecksAndInstallShareOneGate() {
        Type updater = T("youtube_dl_gui.Updater");
        FieldInfo gate = updater.GetField("ProviderOperationRunning", All);
        Require(gate != null && gate.IsStatic, "Provider check/install gate is unavailable");
        int previous = (int)gate.GetValue(null);
        Require(previous == 0, "Another regression left the provider transaction gate occupied");
        try {
            gate.SetValue(null, 1); // Simulates an already-running metadata check or modal update.

            Task<bool> concurrentCheck = (Task<bool>)Call(updater, null,
                "CheckForYoutubeDlUpdate", true);
            Require(concurrentCheck.IsCompleted,
                "Second provider check blocked the UI instead of reporting busy");
            Equal(false, concurrentCheck.GetAwaiter().GetResult());
            Equal(false, Call(updater, null, "UpdateYoutubeDl", false, null));
            Equal(1, gate.GetValue(null));
        }
        finally { gate.SetValue(null, previous); }

        // Rejection of unsupported self-update must release the same gate.
        Equal(false, Call(updater, null, "UpdateYoutubeDl", true, null));
        Equal(0, gate.GetValue(null));

        // A provider operation should never mutate shared release metadata while
        // another owns the lease; the real Download/Check entry points were used,
        // not just the gate primitive.
    }
}

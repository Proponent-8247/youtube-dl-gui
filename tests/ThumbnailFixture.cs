using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
internal static class ThumbnailFixture {
    private static int Main(string[] args) {
        string pid = Environment.GetEnvironmentVariable("YTDL_AUDIT_PID_FILE");
        string record = Environment.GetEnvironmentVariable("YTDL_AUDIT_THUMB_INPUT");
        int input = Array.IndexOf(args, "-i") + 1;
        if (string.IsNullOrEmpty(pid) || string.IsNullOrEmpty(record) || input == 0 || input >= args.Length) return 2;
        File.WriteAllText(record, args[input]);
        File.WriteAllText(pid, Process.GetCurrentProcess().Id.ToString());
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }
}

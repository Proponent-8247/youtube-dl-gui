#nullable enable
namespace youtube_dl_gui;
using Microsoft.Win32;
internal static class SystemRegistry {
    /// <summary>
    /// Checks the registry for the protocol.
    /// </summary>
    /// <returns><see langword="true"/> if the registry for the protocol exists and points to the current application path; otherwise, <see langword="false"/>.</returns>
    public static bool CheckRegistry() {
        using RegistryKey? ProtocolKey = Registry.ClassesRoot.OpenSubKey("ytdlgui", false);
        using RegistryKey? CommandKey = ProtocolKey?.OpenSubKey("shell\\open\\command");

        string? Command = CommandKey?.GetValue("")?.ToString();
        return ProtocolKey?.GetValue("URL Protocol") is not null &&
            Command?.Equals($"\"{Program.FullProgramPath}\" \"%1\"", StringComparison.InvariantCultureIgnoreCase) == true;
    }
    /// <summary>
    /// Creates or modifies the registry to the current application path.
    /// </summary>
    /// <returns>An int value based on the success, with 0 being successful and non-zero indicating an error.</returns>
    public static int SetRegistry() {
        if (!Program.IsAdmin)
            return 2;

        try {
            using RegistryKey ProtocolKey = Registry.ClassesRoot.CreateSubKey("ytdlgui", true);
            ProtocolKey.SetValue("URL Protocol", "");

            using (RegistryKey CommandKey = ProtocolKey.CreateSubKey("shell\\open\\command")) {
                CommandKey.SetValue("", $"\"{Program.FullProgramPath}\" \"%1\"");
            }

            using (RegistryKey IconKey = ProtocolKey.CreateSubKey("DefaultIcon", true)) {
                IconKey.SetValue("", $"\"{Program.FullProgramPath}\",0");
            }

            return 0;
        }
        catch (Exception ex) {
            Log.ReportException(ex);
            return 1;
        }
    }
}
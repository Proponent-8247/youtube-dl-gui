using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

internal static partial class AuditRegression {
    private static void UpdatesNeverTargetDiscoveredExternalTools() {
        Type verification = T("youtube_dl_gui.Verification");
        Type general = T("youtube_dl_gui.General");
        Type program = T("youtube_dl_gui.Program");
        string programRoot = (string)program.GetProperty("ProgramPath", All).GetValue(null, null);
        string providerName = (string)Call(verification, null, "GetYoutubeDlProvider", true);
        string expectedProvider = Path.Combine(programRoot, providerName);
        string expectedFfmpeg = Path.Combine(programRoot, "ffmpeg.exe");
        string foreign = Path.Combine(Path.GetTempPath(), "unowned-path-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(foreign);

        FieldInfo provider = verification.GetField("<YoutubeDlPath>k__BackingField", All);
        FieldInfo ffmpeg = verification.GetField("<FFmpegPath>k__BackingField", All);
        FieldInfo staticProvider = general.GetField("fUseStaticYtdl", All);
        FieldInfo staticFfmpeg = general.GetField("fUseStaticFFmpeg", All);
        FieldInfo configuredProvider = general.GetField("fytdlPath", All);
        FieldInfo configuredFfmpeg = general.GetField("fffmpegPath", All);
        Require(provider != null && ffmpeg != null && staticProvider != null && staticFfmpeg != null &&
            configuredProvider != null && configuredFfmpeg != null,
            "Could not inspect discovered vs explicitly configured tool paths");
        object[] old = {
            provider.GetValue(null), ffmpeg.GetValue(null), staticProvider.GetValue(null),
            staticFfmpeg.GetValue(null), configuredProvider.GetValue(null), configuredFfmpeg.GetValue(null)
        };
        string oldPath = Environment.GetEnvironmentVariable("PATH");
        try {
            string outsideProvider = Path.Combine(foreign, providerName);
            string outsideFfmpeg = Path.Combine(foreign, "ffmpeg.exe");
            byte[] bytes = new UTF8Encoding(false).GetBytes("USER MANAGED DO NOT OVERWRITE");
            File.WriteAllBytes(outsideProvider, bytes);
            File.WriteAllBytes(outsideFfmpeg, bytes);
            File.WriteAllBytes(Path.Combine(foreign, "ffprobe.exe"), bytes);
            Environment.SetEnvironmentVariable("PATH", foreign + ";" + oldPath);
            object[] lookup = { providerName, null };
            Equal(true, Call(verification, null, "ProgramInSystemPath", lookup));
            Equal(Path.GetFullPath(foreign), Path.GetFullPath((string)lookup[1]));

            provider.SetValue(null, outsideProvider);
            ffmpeg.SetValue(null, outsideFfmpeg);
            staticProvider.SetValue(null, false);
            staticFfmpeg.SetValue(null, false);
            Equal(expectedProvider, Call(verification, null, "GetManagedYoutubeDlUpdatePath"));
            Equal(expectedFfmpeg, Call(verification, null, "GetManagedFfmpegUpdatePath"));
            Equal(true, Call(verification, null, "CanManageConfiguredYoutubeDlPath"));
            Equal(true, Call(verification, null, "CanManageConfiguredFfmpegPath"));
            Require(!string.Equals(outsideProvider, expectedProvider, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(outsideFfmpeg, expectedFfmpeg, StringComparison.OrdinalIgnoreCase),
                "Fixture did not establish unowned external execution paths");

            staticProvider.SetValue(null, true);
            configuredProvider.SetValue(null, outsideProvider);
            staticFfmpeg.SetValue(null, true);
            configuredFfmpeg.SetValue(null, outsideFfmpeg);
            Equal(false, Call(verification, null, "CanManageConfiguredYoutubeDlPath"));
            Equal(false, Call(verification, null, "CanManageConfiguredFfmpegPath"));
            Equal(null, Call(verification, null, "GetExplicitFirstRunYoutubeDlInstallPath"));
            Equal(null, Call(verification, null, "GetExplicitFirstRunFfmpegInstallPath"));

            // An explicit first-run consent may CREATE a new custom location, but
            // must not replace an already occupied external executable or companion.
            string fresh = Path.Combine(foreign, "explicit-new-install");
            Directory.CreateDirectory(fresh);
            string newProvider = Path.Combine(fresh, providerName);
            string newFfmpeg = Path.Combine(fresh, "ffmpeg.exe");
            configuredProvider.SetValue(null, newProvider);
            configuredFfmpeg.SetValue(null, newFfmpeg);
            Equal(newProvider, Call(verification, null, "GetExplicitFirstRunYoutubeDlInstallPath"));
            Equal(newFfmpeg, Call(verification, null, "GetExplicitFirstRunFfmpegInstallPath"));
            File.WriteAllBytes(newProvider, bytes);
            File.WriteAllBytes(Path.Combine(fresh, "ffprobe.exe"), bytes);
            Equal(null, Call(verification, null, "GetExplicitFirstRunYoutubeDlInstallPath"));
            Equal(null, Call(verification, null, "GetExplicitFirstRunFfmpegInstallPath"));
            File.Delete(newProvider);
            File.Delete(Path.Combine(fresh, "ffprobe.exe"));
            File.WriteAllBytes(newFfmpeg + ".update", bytes);
            Equal(null, Call(verification, null, "GetExplicitFirstRunFfmpegInstallPath"));
            File.Delete(newFfmpeg + ".update");

            Equal(expectedProvider, Call(verification, null, "GetManagedYoutubeDlUpdatePath"));
            Equal(expectedFfmpeg, Call(verification, null, "GetManagedFfmpegUpdatePath"));

            configuredProvider.SetValue(null, expectedProvider);
            configuredFfmpeg.SetValue(null, expectedFfmpeg);
            Equal(true, Call(verification, null, "CanManageConfiguredYoutubeDlPath"));
            Equal(true, Call(verification, null, "CanManageConfiguredFfmpegPath"));

            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(root, "youtube-dl-gui", "Updater", "Updater.cs"));
            int providerBegin = source.IndexOf("public static bool UpdateYoutubeDl(", StringComparison.Ordinal);
            int providerEnd = source.IndexOf("private static bool TryDeleteFfmpegArchive(", providerBegin);
            Require(providerBegin >= 0 && providerEnd > providerBegin, "Provider update source missing");
            string providerCode = source.Substring(providerBegin, providerEnd - providerBegin);
            Require(providerCode.Contains("Verification.GetManagedYoutubeDlUpdatePath()") &&
                providerCode.Contains("Verification.CanManageConfiguredYoutubeDlPath()") &&
                !providerCode.Contains("Verification.YoutubeDlPath ??"),
                "Updater still treats discovery or external static path as a writable provider target");

            int ffmpegBegin = source.IndexOf("public static async Task<bool> UpdateFfmpeg(", StringComparison.Ordinal);
            int ffmpegEnd = source.IndexOf("Retrieves the available youtube-dl-gui languages.", ffmpegBegin);
            Require(ffmpegBegin >= 0 && ffmpegEnd > ffmpegBegin, "FFmpeg update source missing");
            string ffmpegCode = source.Substring(ffmpegBegin, ffmpegEnd - ffmpegBegin);
            Require(ffmpegCode.Contains("Verification.GetManagedFfmpegUpdatePath()") &&
                ffmpegCode.Contains("Verification.CanManageConfiguredFfmpegPath()") &&
                !ffmpegCode.Contains("Verification.FFmpegPath ??"),
                "Updater still treats discovery or external static path as a writable FFmpeg target");
            Require(bytes.SequenceEqual(File.ReadAllBytes(outsideProvider)) &&
                bytes.SequenceEqual(File.ReadAllBytes(outsideFfmpeg)) &&
                bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(foreign, "ffprobe.exe"))),
                "Update destination selection modified external tool fixtures");
        }
        finally {
            provider.SetValue(null, old[0]);
            ffmpeg.SetValue(null, old[1]);
            staticProvider.SetValue(null, old[2]);
            staticFfmpeg.SetValue(null, old[3]);
            configuredProvider.SetValue(null, old[4]);
            configuredFfmpeg.SetValue(null, old[5]);
            Environment.SetEnvironmentVariable("PATH", oldPath);
            try { Directory.Delete(foreign, true); } catch { }
        }
    }
}

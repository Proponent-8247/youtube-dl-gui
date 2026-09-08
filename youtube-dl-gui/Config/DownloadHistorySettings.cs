#nullable disable
namespace youtube_dl_gui;
using System;
using System.Globalization;
using System.IO;
using youtube_dl_gui.History;

internal sealed class DownloadHistoryPreferences {
    public bool Enabled;
    public string ConfigurationError = "";
    public bool EverEnabled;
    public bool CustomArchive;
    public string ArchivePath = "";
    public bool KeepBackup = true;
    public bool StopWhenMissing;
    public bool LegacyYoutube;
    public string LastSynchronized = "";
    public int LastEntryCount;
    public DownloadHistoryPreferences Copy() { return (DownloadHistoryPreferences)MemberwiseClone(); }
}

internal static class DownloadHistorySettings {
    private const string Section = "Downloads";
    private static readonly object Gate = new object();
    private static DownloadHistoryPreferences current = Load();
    public static DownloadHistoryPreferences Current { get { lock (Gate) return current.Copy(); } }
    public static bool Enabled { get { lock (Gate) return current.Enabled; } }
    public static string SessionStatus { get; private set; } = "Not validated in this session";

    private static DownloadHistoryPreferences Load() {
        var value = new DownloadHistoryPreferences {
            Enabled = IniProvider.Read(false, false, Section, "UseDownloadArchive"),
            EverEnabled = IniProvider.Read(false, false, Section, "DownloadArchiveEverEnabled"),
            CustomArchive = !string.Equals(IniProvider.Read("", "Library", Section, "DownloadArchiveScope"), "Library", StringComparison.OrdinalIgnoreCase),
            ArchivePath = IniProvider.Read("", "", Section, "DownloadArchivePath"),
            KeepBackup = IniProvider.Read(false, true, Section, "DownloadArchiveBackup"),
            StopWhenMissing = !string.Equals(IniProvider.Read("", "Rebuild", Section, "DownloadArchiveMissingBehavior"), "Rebuild", StringComparison.OrdinalIgnoreCase),
            LegacyYoutube = IniProvider.Read(false, false, Section, "DownloadArchiveLegacyYoutubeIds"),
            LastSynchronized = IniProvider.Read("", "", Section, "DownloadArchiveLastSynchronized"),
            LastEntryCount = Math.Max(0, IniProvider.Read(0, 0, Section, "DownloadArchiveLastEntryCount"))
        };
        string rawEnabled = IniProvider.Read("", "False", Section, "UseDownloadArchive").Trim().ToLowerInvariant();
        if (rawEnabled != "true" && rawEnabled != "on" && rawEnabled != "1"
            && rawEnabled != "false" && rawEnabled != "off" && rawEnabled != "0") {
            value.Enabled = true;
            value.ConfigurationError = "Invalid UseDownloadArchive setting. Review Download History settings and Apply an explicit enabled/disabled choice.";
        }
        value.EverEnabled |= value.Enabled;
        return value;
    }
    public static string LibraryPath {
        get {
            string value = Downloads.downloadPath;
            if (value.StartsWith(".\\", StringComparison.Ordinal) || value.StartsWith("./", StringComparison.Ordinal))
                value = Path.Combine(Program.ProgramPath, value.Substring(2));
            return HistoryStore.Canonical(value);
        }
    }
    public static HistoryOptions Options(DownloadHistoryPreferences preferences, string template) {
        if (!string.IsNullOrEmpty(preferences.ConfigurationError)) throw new HistoryException(preferences.ConfigurationError);
        string library = LibraryPath;
        if (preferences.CustomArchive && string.IsNullOrWhiteSpace(preferences.ArchivePath))
            throw new HistoryException("Select an absolute custom archive path.");
        return new HistoryOptions {
            LibraryPath = library,
            ArchivePath = preferences.CustomArchive ? HistoryStore.Canonical(preferences.ArchivePath) : Path.Combine(library, "yt-dlp-archive.txt"),
            Template = template,
            KeepBackup = preferences.KeepBackup,
            StopWhenMissing = preferences.StopWhenMissing,
            LegacyExtractor = preferences.LegacyYoutube ? "youtube" : "",
            UseInfoJson = true
        };
    }
    public static void RequireIdle() {
        if (HistoryProcessGuard.ActiveCount != 0 || !Program.RunningActions.IsEmpty)
            throw new HistoryException("Close the downloader/processing windows before changing Download History settings or running library maintenance.");
    }
    private static void Write(string key, string value) {
        if (NativeMethods.WritePrivateProfileString(Section, key, value, IniProvider.IniPath) == 0
            || IniProvider.Read("", "", Section, key) != value)
            throw new HistoryException("Could not save and verify Download History setting: " + key + ".");
    }
    // Persist disabled first and enabled last. A partial INI write must never enable an
    // unverified mixture of the old and new archive settings.
    public static void Save(DownloadHistoryPreferences value, string template) {
        lock (Gate) {
            RequireIdle();
            if (value.Enabled) HistoryTemplate.Validate(template);
            Write("UseDownloadArchive", "False");
            current.Enabled = false;
            SessionStatus = current.EverEnabled ? "Dormant / potentially stale" : "Disabled";
            Write("DownloadArchiveScope", value.CustomArchive ? "Custom" : "Library");
            Write("DownloadArchivePath", value.ArchivePath ?? "");
            Write("DownloadArchiveBackup", value.KeepBackup ? "True" : "False");
            Write("DownloadArchiveMissingBehavior", value.StopWhenMissing ? "Stop" : "Rebuild");
            Write("DownloadArchiveLegacyYoutubeIds", value.LegacyYoutube ? "True" : "False");
            Write("DownloadArchiveUseFilenameIds", "True");
            Write("DownloadArchiveUseInfoJson", "True");
            Write("DownloadArchiveFailIfUnavailable", "True");
            value.EverEnabled |= current.EverEnabled || value.Enabled;
            Write("DownloadArchiveEverEnabled", value.EverEnabled ? "True" : "False");
            if (template != Downloads.fileNameSchema) {
                Write("fileNameSchema", template);
                Downloads.fileNameSchema = template;
            }
            Write("UseDownloadArchive", value.Enabled ? "True" : "False");
            current = value.Copy();
            SessionStatus = value.Enabled ? "Preflight validated; every download is checked again" : value.EverEnabled ? "Dormant / potentially stale" : "Disabled";
        }
    }
    public static void Record(HistoryReport report) {
        lock (Gate) {
            current.LastEntryCount = report.Entries.Count;
            current.LastSynchronized = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            SessionStatus = report.State;
            // These are display caches, not validation evidence. An INI failure cannot
            // turn a valid library checkpoint into a false claim of synchronization.
            try {
                Write("DownloadArchiveLastEntryCount", current.LastEntryCount.ToString(CultureInfo.InvariantCulture));
                Write("DownloadArchiveLastSynchronized", current.LastSynchronized);
            }
            catch (HistoryException ex) { Log.Write(ex.Message); }
        }
    }
    public static void RecordFailure(string message) {
        lock (Gate) SessionStatus = "Unsafe / requires attention: " + message;
    }
}

namespace youtube_dl_gui_updater;

using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using murrty.controls;
using murrty.updater;

static class Program {
    internal static string ApplicationName => fApplicationName;
    internal static string ApplicationPath => fApplicationPath;
    internal static string FullApplicationPath => fFullApplicationPath;

    private static readonly string fApplicationName = AppDomain.CurrentDomain.FriendlyName;
    private static readonly string fApplicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    private static readonly string fFullApplicationPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

    internal static ManagedHttpClient DownloadClient { get; private set; }
    private static CancellationTokenSource fCancelToken = new();
    internal static CancellationTokenSource CancelToken {
        get {
            while (true) {
                CancellationTokenSource Current = Volatile.Read(ref fCancelToken);
                try { _ = Current.Token; return Current; }
                catch (ObjectDisposedException) {
                    CancellationTokenSource Replacement = new();
                    if (ReferenceEquals(Interlocked.CompareExchange(ref fCancelToken, Replacement, Current), Current)) return Replacement;
                    Replacement.Dispose();
                }
            }
        }
        private set => Volatile.Write(ref fCancelToken, value ?? new CancellationTokenSource());
    }

    public static Version CurrentVersion { get; } = new(1, 6, 0);
    internal static string UserAgent { get; } = $"youtube_dl_gui-updater/{CurrentVersion}";
    internal static DownloadType Type { get; private set; } = DownloadType.None;

    public static int ExitCode { get; set; } = 0;

    [STAThread]
    static int Main(string[] args) {
#if DEBUG
        Language.LoadInternalEnglish();
#else
        var Value = new StringBuilder(65535);
        NativeMethods.GetPrivateProfileString("youtube-dl-gui", "LanguageFile", "${empty}", Value, 65535, Path.Combine(ApplicationPath, "youtube-dl-gui.ini"));
        string LanguageFile = Value.ToString() == "${empty}" ? null : Value.ToString();
        Language.LoadLanguage(string.IsNullOrWhiteSpace(LanguageFile) ? null : Path.Combine(ApplicationPath, "lang", LanguageFile));
#endif

        nint Handle = 0;
        int ProcessID = 0;
        if (args.Length > 0) {
            bool BreakLoop = false;
            for (int i = 0; i < args.Length; i++) {
                switch (args[i].ToLowerInvariant()) {
                    case "-hwnd": {
                        if (++i >= args.Length) {
                            BreakLoop = true;
                            break;
                        }
                        if (long.TryParse(args[i], out long hwnd))
                            Handle = (nint)hwnd;
                    }
                    break;
                    case "-pid": {
                        if (++i >= args.Length) {
                            BreakLoop = true;
                            break;
                        }
                        if (int.TryParse(args[i], out int pid))
                            ProcessID = pid;
                    }
                    break;
                }
                if (BreakLoop) break;
            }
        }

        ApplicationHandles? ApplicationData = null;
        UpdateData? UpdateData = null;

        ManagedHttpClient.UpdateDownloadClient(UserAgent);
        DownloadClient = new();
        CancelToken = new();

        if (Handle != 0 && ProcessID != 0) {
            ApplicationData = new(Handle, ProcessID);
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        murrty.controls.natives.Consts.UpdateHand();

        if (Environment.CurrentDirectory != ApplicationPath)
            Environment.CurrentDirectory = ApplicationPath;

        if (ApplicationData is null && UpdateData is null && !InvalidData())
            return 1;

        SetTls();
        Application.Run(ApplicationData is not null ? new frmUpdater(ApplicationData) : new frmUpdater(UpdateData));
        return ExitCode;
    }
    
    private static bool InvalidData() {
        using frmUpdaterInvalidData Invalid = new();
        Type = Invalid.ShowDialog() switch {
            DialogResult.Yes => DownloadType.PreRelease,
            DialogResult.No => DownloadType.Latest,
            _ => DownloadType.None
        };
        return Type != DownloadType.None;
    }
    internal static bool IsWebUrl(string Value) {
        if (!Uri.TryCreate(Value, UriKind.Absolute, out Uri ParsedUri)) {
            return false;
        }
        return ParsedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || ParsedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryOpenWebUrl(string Value) {
        if (!IsWebUrl(Value)) {
            return false;
        }
        try {
            using System.Diagnostics.Process Browser = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(Value) { UseShellExecute = true });
            return Browser is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                or InvalidOperationException
                                or NotSupportedException
                                or ArgumentException) {
            return false;
        }
    }

    internal static void SetTls() {
        try { //try TLS 1.3
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)12288
                                                            | (System.Net.SecurityProtocolType)3072;
        }
        catch (NotSupportedException) {
            // Updates require TLS 1.2 or newer. Do not fall back to obsolete protocols.
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;
        }
    }
}
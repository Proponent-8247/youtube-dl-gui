#nullable enable
namespace youtube_dl_gui;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using murrty.controls;
internal static class Program {
    /// <summary>
    /// Gets the curent version of the program.
    /// </summary>
    public static Version CurrentVersion { get; } = new(3, 3, 0, 2);
    /// <summary>
    /// Gets whether the program is running in debug mode.
    /// </summary>
    internal static bool DebugMode { get; private set; }
    /// <summary>
    /// Gets whether the program is running as administrator.
    /// </summary>
    public static bool IsAdmin { get; private set; }
    /// <summary>
    /// Gets or sets the exit code of the application.
    /// </summary>
    public static int ExitCode { get; internal set; }
    /// <summary>
    /// Gets or sets whether the update was checked this run.
    /// </summary>
    internal static bool UpdateChecked { get; set; }
    /// <summary>
    /// Gets or sets whether the program is starting an update.
    /// </summary>
    internal static bool IsUpdating { get; set; }

    /// <summary>
    /// Represents the GUID of the program. Used for enforcing the mutex.
    /// </summary>
    public static string ProgramGUID { get; } = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value;
    /// <summary>
    /// The full path of the program.
    /// </summary>
    public static string FullProgramPath { get; } = Process.GetCurrentProcess().MainModule.FileName;
    /// <summary>
    /// The path of the program, not inculding file name.
    /// </summary>
    public static string ProgramPath { get; } = Path.GetDirectoryName(FullProgramPath);
    /// <summary>
    /// The user-agent used for web client calls.
    /// </summary>
    public static string UserAgent { get; } = "youtube-dl-gui/" + CurrentVersion;

    /// <summary>
    /// The list of running downloads or conversions.
    /// </summary>
    internal static QueueList<Form> RunningActions { get; } = [];
    /// <summary>
    /// The image list used for batch actions.
    /// </summary>
    internal static ImageList BatchStatusImages { get; private set; } = null!;
    /// <summary>
    /// The image list used for the extended downloader.
    /// </summary>
    internal static ImageList ExtendedDownloaderSelectedImages { get; private set; } = null!;

    /// <summary>
    /// The mutex used for enforcing the applications' single instance.
    /// </summary>
    private static Mutex Instance { get; set; } = null!;
    /// <summary>
    /// The main form used for the application.
    /// </summary>
    public static frmMain? MainForm { get; private set; }
    /// <summary>
    /// The argument handler for sent arguments.
    /// </summary>
    private static MessageHandler QueueHandler { get; set; } = null!;
    /// <summary>
    /// Represents the HttpClient used through the applications' life.
    /// </summary>
    internal static ManagedHttpClient HttpClient { get; private set; } = null!;
    internal static bool UpdaterEnabled { get; private set; } = true;

    [STAThread]
    private static int Main(string[] args) {
        if (args.Length == 1 && args[0].TrimStart('-').Equals("installprotocol", StringComparison.OrdinalIgnoreCase)) {
            IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            return SystemRegistry.SetRegistry();
        }

#if DEBUG
        DebugMode = true;
        Instance = new(true, ProgramGUID);
#else
        if (!(Instance = new(true, ProgramGUID)).WaitOne(TimeSpan.Zero, true)) {
            nint hwnd = CopyData.FindWindow(null, ProgramGUID);

            if (hwnd != 0) {
                List<(ArgumentType Type, string? Data)> Arguments;
                if (args.Length > 0 && (Arguments = youtube_dl_gui.Arguments.RetrieveArguments(args)).Count > 0) {
                    for (int i = 0; i < Arguments.Count; i++) {
                        if (Arguments[i].Data is not string Data) {
                            continue;
                        }

                        nint valPointer = 0;
                        nint cdsPointer = 0;
                        try {
                            byte[] bytes = Encoding.Unicode.GetBytes(Data);
                            valPointer = Marshal.AllocHGlobal(bytes.Length);
                            Marshal.Copy(bytes, 0, valPointer, bytes.Length);

                            CopyDataStruct copyData = new() {
                                dwData = (nint)Arguments[i].Type,
                                cbData = bytes.Length,
                                lpData = valPointer
                            };

                            cdsPointer = CopyData.NintAlloc(copyData);
                            if (!CopyData.TrySendMessage(
                                hWnd: hwnd,
                                Msg: CopyData.WM_COPYDATA,
                                wParam: 0x1,
                                lParam: cdsPointer,
                                TimeoutMilliseconds: 2000)) {
                                Log.Write("Timed out while forwarding an argument to the existing application instance.");
                            }

                            // wParam should be the handle to the Window that sent the message.
                            // Since WM_COPYDATA is overridden, I can DO WHAT I WANT.
                            // Regardless, wParam is unused in this program, so.
                            // 0x1 = The data was sent from another instance.

                            Marshal.FreeHGlobal(cdsPointer);
                            cdsPointer = 0;
                            Marshal.FreeHGlobal(valPointer);
                            valPointer = 0;
                        }
                        finally {
                            if (cdsPointer != 0) {
                                Marshal.FreeHGlobal(cdsPointer);
                            }
                            if (valPointer != 0) {
                                Marshal.FreeHGlobal(valPointer);
                            }
                        }
                    }
                }
                else if (!CopyData.TrySendMessage(hwnd, CopyData.WM_SHOWFORM, 0, 0, 2000)) {
                    Log.Write("Timed out while asking the existing application instance to show itself.");
                }
            }

            return 1152;
        }
#endif

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Log.Write("Initializing application");

        IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        if (Environment.CurrentDirectory != ProgramPath) {
            Log.Write("The current directory is wrong.");
            Environment.CurrentDirectory = ProgramPath;
        }

        Thread.CurrentThread.Name = "Main application thread";
        ManagedHttpClient.UpdateDownloadClient(UserAgent);
        ManagedHttpClient.UpdateSyncContext(SynchronizationContext.Current);
        HttpClient = new();

        if (Initialization.firstTime) {
            if (!RunFirstTimeSetup()) {
                return 1;
            }
        }
        else {
            Language.LoadLanguage($"{Environment.CurrentDirectory}\\lang\\{Initialization.LanguageFile}.ini");
        }

        BatchStatusImages = new() {
            ColorDepth = ColorDepth.Depth32Bit,
            TransparentColor = System.Drawing.Color.Transparent
        };
        BatchStatusImages.Images.Add(Properties.Resources.waiting);  // 0
        BatchStatusImages.Images.Add(Properties.Resources.download); // 1
        BatchStatusImages.Images.Add(Properties.Resources.finished); // 2
        BatchStatusImages.Images.Add(Properties.Resources.error);    // 3

        ExtendedDownloaderSelectedImages = new() {
            ColorDepth = ColorDepth.Depth32Bit,
            TransparentColor = System.Drawing.Color.Transparent
        };
        ExtendedDownloaderSelectedImages.Images.Add(Properties.Resources.best);             // 0
        ExtendedDownloaderSelectedImages.Images.Add(Properties.Resources.selected);         // 1
        ExtendedDownloaderSelectedImages.Images.Add(Properties.Resources.best_disabled);    // 2
        ExtendedDownloaderSelectedImages.Images.Add(Properties.Resources.selected_disabled);// 3

        //throw new Exception("test");
        murrty.controls.natives.Consts.UpdateHand();
        Formats.LoadCustomFormats();
        SetTls();

        (QueueHandler = new()).Show();

        Arguments.ParseArguments(args);
        if (CheckArgs()) {
            AwaitActions();
            return ExitCode;
        }

        // Etc.
        (MainForm = new frmMain()).ShowDialog();
        MainForm = null;

        if (!RunningActions.IsEmpty) {
            AwaitActions();
        }

        Instance?.ReleaseMutex();

        return ExitCode;
    }

    private static bool RunFirstTimeSetup() {
        using ApplicationContext SetupContext = new();
        bool SetupSucceeded = false;
        ExceptionDispatchInfo? SetupException = null;

        EventHandler? BeginSetup = null;
        BeginSetup = async (_, _) => {
            Application.Idle -= BeginSetup;
            try {
                SetupSucceeded = await FirstTimeSetup();
            }
            catch (Exception ex) {
                SetupException = ExceptionDispatchInfo.Capture(ex);
            }
            finally {
                SetupContext.ExitThread();
            }
        };

        Application.Idle += BeginSetup;
        Application.Run(SetupContext);
        SetupException?.Throw();
        return SetupSucceeded;
    }

    private static async Task<bool> FirstTimeSetup() {
        Log.Write("Initiating first time setup.");
        Language.LoadInternalEnglish();

        // Select a language first
        using frmLanguage LangPicker = new();
        if (LangPicker.ShowDialog() != DialogResult.OK) {
            return false;
        }

        if (Log.MessageBox(Language.dlgFirstTimeInitialMessage, MessageBoxButtons.YesNo) != DialogResult.Yes) {
            return false;
        }

        Initialization.firstTime = false;
        Downloads.downloadPath = Downloads.DefaultDownloadPath;

        if (Log.MessageBox(Language.dlgFirstTimeDownloadFolder, MessageBoxButtons.YesNo) == DialogResult.Yes) {
            using BetterFolderBrowserNS.BetterFolderBrowser fbd = new() {
                RootFolder = Downloads.downloadPath,
                Title = Language.dlgFindDownloadFolder
            };

            if (fbd.ShowDialog() == DialogResult.OK) {
                Downloads.downloadPath = fbd.SelectedPath;
            }
        }

        if (!Verification.YoutubeDlAvailable && Log.MessageBox(Language.dlgFirstTimeDownloadYoutubeDl, MessageBoxButtons.YesNo) == DialogResult.Yes) {
            if (await Updater.CheckForYoutubeDlUpdate()) {
                Updater.UpdateYoutubeDl(false, null);
            }
        }

        if (!Verification.FfmpegAvailable && Log.MessageBox(Language.dlgFirstTimeDownloadFfmpeg, MessageBoxButtons.YesNo) == DialogResult.Yes) {
            await Updater.UpdateFfmpeg(null);
        }

        Log.Write("First time setup has concluded.");
        return true;
    }

    private static void AwaitActions() {
        QueueHandler.AwaitExit();
        Application.Run(QueueHandler);
    }

    internal static void KillProcessTree(uint ProcessId) {
        try {
            using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_Process WHERE ParentProcessId=" + ProcessId);
            using ManagementObjectCollection collection = searcher.Get();
            if (collection.Count > 0) {
                foreach (var proc in collection) {
                    uint id = (uint)proc["ProcessID"];
                    if ((int)id != ProcessId) {
                        try {
                            KillProcessTree(id);
                            using Process procInstance = Process.GetProcessById((int)id);
                            if (!procInstance.HasExited)
                                procInstance.Kill();
                        }
                        catch (Exception ex) {
                            Log.ReportException(ex);
                        }
                    }
                }
            }
        }
        catch (Exception ex) {
            Log.ReportException(ex);
        }
    }

    internal static bool CheckArgs(List<(ArgumentType Type, string? Data)>? args = null) {
        args ??= Arguments.ParsedArguments;
        if (args.Count > 0) {
            int PassedCount = 0;
            for (int i = 0; i < args.Count; i++) {
                (ArgumentType Type, string? Data) Arg = args[i];
                if (Arg.Data.IsNullEmptyWhitespace()) {
                    continue;
                }

                PassedCount++;
                AuthenticationDetails? Auth = null;
                if (Arg.Type == ArgumentType.DownloadAuthenticateVideo || Arg.Type == ArgumentType.DownloadAuthenticateAudio || Arg.Type == ArgumentType.DownloadAuthenticateCustom) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        PassedCount--;
                        continue;
                    }
                }

                if (Arg.Type == ArgumentType.DownloadArchived) {
                    string ArchivedUrl = Arg.Data;
                    if (DownloadHelper.IsYoutubeLink(ArchivedUrl)) {
                        Log.Write("YouTube link given for archival download.");
                        ArchivedUrl = DownloadHelper.GetYoutubeVideoKey(ArchivedUrl);
                    }

                    if (!DownloadHelper.IsYoutubeKey(ArchivedUrl)) {
                        Log.Write("The YouTube key given for archival download is not a valid video key.");
                        PassedCount--;
                        continue;
                    }

                    if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                        new frmExtendedDownloader($"ytarchive:{ArchivedUrl}", true).Show();
                    }
                    else {
                        DownloadInfo NewArchived = new($"https://archived.youtube.com/watch?v={ArchivedUrl}") {
                            CustomArguments = $"ytarchive:{ArchivedUrl}",
                            MostlyCustomArguments = true,
                            Type = DownloadType.Custom
                        };
                        new frmDownloader(NewArchived).Show();
                    }
                    continue;
                }

                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    new frmExtendedDownloader(Arg.Data, Arg.Type is ArgumentType.DownloadCustom or ArgumentType.DownloadAuthenticateCustom ? youtube_dl_gui.CustomArguments.LastUsedYtdlArgument : null, false, Auth, Arg.Type).Show();
                    continue;
                }

                // TODO: Implement the rest of the argument types
                switch (Arg.Type) {
                    case ArgumentType.DownloadVideo:
                    case ArgumentType.DownloadAuthenticateVideo: {
                        DownloadInfo NewVideo = new(Arg.Data) {
                            Type = DownloadType.Video,
                            VideoQuality = (VideoQualityType)Saved.videoQuality,
                            VideoFormat = (VideoFormatType)Saved.VideoFormat,
                            SkipAudioForVideos = !Downloads.VideoDownloadSound,
                            Authentication = Auth,
                        };
                        new frmDownloader(NewVideo).Show();
                    } break;
                    case ArgumentType.DownloadAudio:
                    case ArgumentType.DownloadAuthenticateAudio: {
                        DownloadInfo NewAudio = new(Arg.Data) {
                            Type = DownloadType.Audio,
                            UseVBR = Downloads.AudioDownloadAsVBR,
                            AudioFormat = (AudioFormatType)Saved.AudioFormat,
                            Authentication = Auth,
                        };
                        if (Downloads.AudioDownloadAsVBR)
                            NewAudio.AudioVBRQuality = (AudioVBRQualityType)Saved.AudioVBRQuality;
                        else
                            NewAudio.AudioCBRQuality = (AudioCBRQualityType)Saved.audioQuality;
                        new frmDownloader(NewAudio).Show();
                    } break;
                    case ArgumentType.DownloadCustom:
                    case ArgumentType.DownloadAuthenticateCustom: {
                        DownloadInfo NewCustom = new(Arg.Data) {
                            Type = DownloadType.Custom,
                            CustomArguments = youtube_dl_gui.CustomArguments.LastUsedYtdlArgument,
                            Authentication = Auth,
                        };
                        new frmDownloader(NewCustom).Show();
                    } break;
                    default: {
                        PassedCount--;
                    } break;
                }
            }
            return PassedCount > 0;
        }
        return false;
    }

    internal static bool IsValidDownloadCopyData(CopyDataStruct Data) {
        long Kind = (long)Data.dwData;
        return Data.lpData != IntPtr.Zero && Data.cbData > 0 && Data.cbData <= 1024 * 1024
            && (Data.cbData & 1) == 0
            && Kind >= (long)ArgumentType.DownloadVideo && Kind <= (long)ArgumentType.DownloadArchived;
    }

    internal static bool ShouldSkipVideoAudio(ArgumentType Type) =>
        Type == ArgumentType.DownloadVideoNoSound ||
        Type == ArgumentType.DownloadAuthenticateVideoNoSound ||
        ((Type == ArgumentType.DownloadVideo || Type == ArgumentType.DownloadAuthenticateVideo) && !Downloads.VideoDownloadSound);

    internal static void ParseCopyData(ref Message m, string CustomArguments) {
        if (m.LParam == IntPtr.Zero) {
            m.Result = IntPtr.Zero;
            return;
        }

        CopyDataStruct cds = Marshal.PtrToStructure<CopyDataStruct>(m.LParam);
        if (!IsValidDownloadCopyData(cds)) {
            m.Result = IntPtr.Zero;
            return;
        }

        byte[] bytes = new byte[cds.cbData];
        Marshal.Copy(cds.lpData, bytes, 0, cds.cbData);
        string URL = Encoding.Unicode.GetString(bytes);
        ArgumentType Type = (ArgumentType)cds.dwData;
        switch (Type) {
            case ArgumentType.DownloadVideo:
            case ArgumentType.DownloadAuthenticateVideo:
            case ArgumentType.DownloadVideoNoSound:
            case ArgumentType.DownloadAuthenticateVideoNoSound: {
                AuthenticationDetails? Auth = null;
                if (Type == ArgumentType.DownloadAuthenticateVideo || Type == ArgumentType.DownloadAuthenticateVideoNoSound) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        Log.Write("Authentication required, but the user cancelled the dialog.");
                        return;
                    }
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader(
                        URL: URL,
                        CustomArguments: CustomArguments,
                        Archived: false,
                        Auth: Auth,
                        InitialArgumentType: Type);
                }
                else {
                    DownloadInfo NewInfo = new(URL: URL) {
                        Type = DownloadType.Video,
                        VideoQuality = (VideoQualityType)Saved.videoQuality,
                        VideoFormat = (VideoFormatType)Saved.VideoFormat,
                        SkipAudioForVideos = ShouldSkipVideoAudio(Type),
                        CustomArguments = CustomArguments,
                        Authentication = Auth,
                    };
                    DownloadForm = new frmDownloader(Info: NewInfo);
                }
                DownloadForm.Show();
            } break;

            case ArgumentType.DownloadAudio:
            case ArgumentType.DownloadAuthenticateAudio: {
                AuthenticationDetails? Auth = null;
                if (Type == ArgumentType.DownloadAuthenticateAudio) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        Log.Write("Authentication required, but the user cancelled the dialog.");
                        return;
                    }
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader(
                        URL: URL,
                        CustomArguments: CustomArguments,
                        Archived: false,
                        Auth: Auth,
                        InitialArgumentType: Type);
                }
                else {
                    DownloadInfo NewInfo = new(URL: URL) {
                        Type = DownloadType.Audio,
                        UseVBR = Downloads.AudioDownloadAsVBR,
                        AudioFormat = (AudioFormatType)Saved.AudioFormat,
                        CustomArguments = CustomArguments,
                        Authentication = Auth,
                    };

                    if (Downloads.AudioDownloadAsVBR)
                        NewInfo.AudioVBRQuality = (AudioVBRQualityType)Saved.AudioVBRQuality;
                    else
                        NewInfo.AudioCBRQuality = (AudioCBRQualityType)Saved.audioQuality;

                    DownloadForm = new frmDownloader(Info: NewInfo);
                }
                DownloadForm.Show();
            } break;

            case ArgumentType.DownloadCustom:
            case ArgumentType.DownloadAuthenticateCustom: {
                CustomArguments = CustomArguments.IsNullEmptyWhitespace() ? youtube_dl_gui.CustomArguments.LastUsedYtdlArgument : CustomArguments;
                AuthenticationDetails? Auth = null;
                if (Type == ArgumentType.DownloadAuthenticateCustom) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        Log.Write("Authentication required, but the user cancelled the dialog.");
                        return;
                    }
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader(
                        URL: URL,
                        CustomArguments: CustomArguments,
                        Archived: false,
                        Auth: Auth,
                        InitialArgumentType: Type);
                }
                else {
                    DownloadInfo NewInfo = new(URL: URL) {
                        Type = DownloadType.Custom,
                        CustomArguments = CustomArguments,
                        Authentication = Auth,
                    };
                    DownloadForm = new frmDownloader(Info: NewInfo);
                }
                DownloadForm.Show();
            } break;

            case ArgumentType.DownloadArchived: {
                if (DownloadHelper.IsYoutubeLink(URL)) {
                    Log.Write("YouTube link given for archival download.");
                    URL = DownloadHelper.GetYoutubeVideoKey(URL);
                }

                if (!DownloadHelper.IsYoutubeKey(URL)) {
                    Log.Write("The YouTube key given for archival download is not a valid video key.");
                    return;
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader($"ytarchive:{URL}", true);
                }
                else {
                    DownloadInfo NewInfo = new($"https://archived.youtube.com/watch?v={URL}") {
                        CustomArguments = $"ytarchive:{URL}",
                        MostlyCustomArguments = true,
                        Type = DownloadType.Custom
                    };
                    DownloadForm = new frmDownloader(NewInfo);
                }
                DownloadForm.Show();
            } break;
        }
    }
    internal static void ProcessCopyData(string? URL, ArgumentType Type, string CustomArguments) {
        if (URL.IsNullEmptyWhitespace()) {
            return;
        }

        Log.Write($"ProcessCopyData called: {Type} > {URL}");

        switch (Type) {
            case ArgumentType.DownloadVideo:
            case ArgumentType.DownloadAuthenticateVideo: {
                AuthenticationDetails? Auth = null;
                if (Type == ArgumentType.DownloadAuthenticateVideo) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        Log.Write("Authentication required, but the user cancelled the dialog.");
                        return;
                    }
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader(
                        URL: URL,
                        CustomArguments: CustomArguments,
                        Archived: false,
                        Auth: Auth,
                        InitialArgumentType: Type);
                }
                else {
                    DownloadInfo NewInfo = new(URL: URL) {
                        Type = DownloadType.Video,
                        VideoQuality = (VideoQualityType)Saved.videoQuality,
                        VideoFormat = (VideoFormatType)Saved.VideoFormat,
                        SkipAudioForVideos = !Downloads.VideoDownloadSound,
                        CustomArguments = CustomArguments,
                        Authentication = Auth,
                    };
                    DownloadForm = new frmDownloader(Info: NewInfo);
                }
                DownloadForm.Show();
            } break;

            case ArgumentType.DownloadAudio:
            case ArgumentType.DownloadAuthenticateAudio: {
                AuthenticationDetails? Auth = null;
                if (Type == ArgumentType.DownloadAuthenticateAudio) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        Log.Write("Authentication required, but the user cancelled the dialog.");
                        return;
                    }
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader(
                        URL: URL,
                        CustomArguments: CustomArguments,
                        Archived: false,
                        Auth: Auth,
                        InitialArgumentType: Type);
                }
                else {
                    DownloadInfo NewInfo = new(URL: URL) {
                        Type = DownloadType.Audio,
                        UseVBR = Downloads.AudioDownloadAsVBR,
                        AudioFormat = (AudioFormatType)Saved.AudioFormat,
                        CustomArguments = CustomArguments,
                        Authentication = Auth,
                    };

                    if (Downloads.AudioDownloadAsVBR)
                        NewInfo.AudioVBRQuality = (AudioVBRQualityType)Saved.AudioVBRQuality;
                    else
                        NewInfo.AudioCBRQuality = (AudioCBRQualityType)Saved.audioQuality;

                    DownloadForm = new frmDownloader(Info: NewInfo);
                }
                DownloadForm.Show();
            } break;

            case ArgumentType.DownloadCustom:
            case ArgumentType.DownloadAuthenticateCustom: {
                CustomArguments = CustomArguments.IsNullEmptyWhitespace() ? youtube_dl_gui.CustomArguments.LastUsedYtdlArgument : CustomArguments;
                AuthenticationDetails? Auth = null;
                if (Type == ArgumentType.DownloadAuthenticateCustom) {
                    Auth = AuthenticationDetails.GetAuthentication();
                    if (Auth is null) {
                        Log.Write("Authentication required, but the user cancelled the dialog.");
                        return;
                    }
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader(
                        URL: URL,
                        CustomArguments: CustomArguments,
                        Archived: false,
                        Auth: Auth,
                        InitialArgumentType: Type);
                }
                else {
                    DownloadInfo NewInfo = new(URL: URL) {
                        Type = DownloadType.Custom,
                        CustomArguments = CustomArguments,
                    Authentication = Auth,
                    };
                    DownloadForm = new frmDownloader(Info: NewInfo);
                }
               DownloadForm.Show();
            } break;

            case ArgumentType.DownloadArchived: {
                if (DownloadHelper.IsYoutubeLink(URL)) {
                    Log.Write("YouTube link given for archival download.");
                    URL = DownloadHelper.GetYoutubeVideoKey(URL);
                }

                if (!DownloadHelper.IsYoutubeKey(URL)) {
                    Log.Write("The YouTube key given for archival download is not a valid video key.");
                    return;
                }

                Form DownloadForm;
                if (Downloads.ExtendedDownloaderPreferExtendedForm) {
                    DownloadForm = new frmExtendedDownloader($"ytarchive:{URL}", true);
                }
                else {
                    DownloadInfo NewInfo = new($"https://archived.youtube.com/watch?v={URL}") {
                        CustomArguments = $"ytarchive:{URL}",
                        MostlyCustomArguments = true,
                        Type = DownloadType.Custom
                    };
                    DownloadForm = new frmDownloader(NewInfo);
                }
                DownloadForm.Show();
            } break;
        }
    }

    internal static bool IsWebUrl(string? Value) {
        if (!Uri.TryCreate(Value, UriKind.Absolute, out Uri? ParsedUri)) {
            return false;
        }
        return ParsedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || ParsedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryOpenWebUrl(string? Value) {
        if (!IsWebUrl(Value)) {
            return false;
        }
        try {
            using Process? Browser = Process.Start(new ProcessStartInfo(Value!) { UseShellExecute = true });
            return Browser is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                or InvalidOperationException
                                or NotSupportedException
                                or ArgumentException) {
            Log.Write($"Unable to open a web link in the default browser: {ex.Message}");
            return false;
        }
    }

    internal static string CalculateSha256Hash(string File) {
        using SHA256 ComputeUpdaterHash = SHA256.Create();
        using FileStream UpdaterStream = System.IO.File.OpenRead(File);
        string UpdaterHash = BitConverter.ToString(ComputeUpdaterHash.ComputeHash(UpdaterStream)).Replace("-", "").ToLowerInvariant();
        UpdaterStream.Close();
        return UpdaterHash;
    }

    internal static void KillForUpdate() {
        // Form diposes
        // Any downloads/conversion/merges in progress will finish before fully closing for updates.
        MainForm?.RemoveTrayIcon();
        MainForm?.Dispose();
    }

    internal static nint GetMessagesHandle() => QueueHandler.Handle;

    internal static void SetTls() {
        try { //try TLS 1.3
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)12288
                                                            | System.Net.SecurityProtocolType.Tls12;
            Log.Write("TLS 1.3 and TLS 1.2 are enabled.");
        }
        catch (NotSupportedException) {
            try { //try TLS 1.2
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                Log.Write("TLS 1.2 will be used.");
            }
            catch (NotSupportedException) {
                UpdaterEnabled = false;
                Log.Write("TLS 1.2+ is unavailable; Github updating is disabled.");
            }
        }
    }

    internal static void AddProcessingForm(Form form) {
        RunningActions.TryAdd(form);
    }

    internal static void RemoveProcessingForm(Form form) {
        if (RunningActions.TryRemove(form))
            QueueHandler.CheckExit();
    }
}
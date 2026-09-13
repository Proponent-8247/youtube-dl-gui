namespace youtube_dl_gui_updater;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using murrty.controls;

internal partial class frmUpdater : Form {
    private const int MaxRetries = 5;
    private const int RetryDelay = 1_000;
    private const uint ParentMessageTimeoutMilliseconds = 2_000;
    private const string ApplicationDownloadUrl = "https://github.com/Proponent-8247/{0}/releases/download/{1}/{0}.exe";

    private UpdateData UpdateData;
    private Process ProgramProcess;
    private readonly ApplicationHandles ApplicationData;
    private readonly bool DownloadLatest = false;
    private bool WaitingForApplication;
    private bool UpdateDataAccepted;
#if DEBUG
    private static bool AuditIpcMode => Environment.GetEnvironmentVariable("YTDL_AUDIT_IPC") == "1";
    private static string AuditScenario => Environment.GetEnvironmentVariable("YTDL_AUDIT_IPC_SCENARIO") ?? string.Empty;
    private static void AuditWrite(string Value) {
        string PathValue = Environment.GetEnvironmentVariable("YTDL_AUDIT_IPC_UPDATER_RESULT");
        if (string.IsNullOrWhiteSpace(PathValue)) return;
        try { File.AppendAllText(PathValue, Value + Environment.NewLine); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
#endif


    private frmUpdater() {
        InitializeComponent();
        LoadLanguage();

        ManagedHttpClient.UpdateSyncContext(SynchronizationContext.Current);
        lbUpdaterVersion.Text = Program.CurrentVersion.ToString();
        pbDownloadProgress.Style = ProgressBarStyle.Marquee;
    }
    public frmUpdater(ApplicationHandles? ApplicationData) : this() {
        if (ApplicationData is null || !ApplicationData.HasValue) {
            DownloadLatest = true;
            pbDownloadProgress.Text = "getting latest version...";
        }
        else {
            this.ApplicationData = ApplicationData.Value;
            ProgramProcess = Process.GetProcessById(this.ApplicationData.ProcessID);
            pbDownloadProgress.Text = "waiting for update data...";
        }
    }
    public frmUpdater(UpdateData? UpdateData) : this() {
        if (UpdateData is null || !UpdateData.HasValue) {
            DownloadLatest = true;
            pbDownloadProgress.Text = "getting latest version...";
        }
        else this.UpdateData = UpdateData.Value;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint WindowHandle, out uint ProcessId);

    private bool IsExpectedApplicationWindow(nint WindowHandle) {
        if (WindowHandle == 0 || ApplicationData.ProcessID <= 0
        || GetWindowThreadProcessId(WindowHandle, out uint ProcessId) == 0
        || ProcessId != (uint)ApplicationData.ProcessID) return false;
        return true;
    }

    private static bool IsSimpleExecutableName(string FileName) {
        if (string.IsNullOrWhiteSpace(FileName) || Path.IsPathRooted(FileName)
        || FileName.IndexOf('\\') >= 0 || FileName.IndexOf('/') >= 0
        || FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        return string.Equals(Path.GetFileName(FileName), FileName, StringComparison.Ordinal);
    }

    private static bool IsSha256(string Hash) => Hash?.Length == 64 && Hash.All(Uri.IsHexDigit);

    private bool TryGetExpectedApplicationPath(out string FileName) {
        FileName = null;
        if (ApplicationData.ProcessID <= 0) return false;
        try {
            using Process ExpectedProcess = Process.GetProcessById(ApplicationData.ProcessID);
            if (ExpectedProcess.HasExited) return false;
            string Candidate = ExpectedProcess.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(Candidate)) return false;
            FileName = Path.GetFullPath(Candidate);
            return FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                or System.ComponentModel.Win32Exception
                                or NotSupportedException or IOException) {
            return false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        CancellationTokenSource Cancellation = Program.CancelToken;
        if (!Cancellation.IsCancellationRequested) Cancellation.Cancel();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m) {
        switch (m.Msg) {
            case CopyData.WM_COPYDATA: {
                if (UpdateDataAccepted || ApplicationData.MessageHandle == 0
                || m.WParam != ApplicationData.MessageHandle || m.LParam == IntPtr.Zero
                || !IsExpectedApplicationWindow(m.WParam)) {
                    m.Result = IntPtr.Zero;
                    break;
                }
                CopyDataStruct DataStruct;
                try { DataStruct = m.GetCopyDataStructure(); }
                catch { m.Result = IntPtr.Zero; break; }
                if (DataStruct.lpData == IntPtr.Zero || DataStruct.cbData != Marshal.SizeOf<UpdateData>()) {
                    m.Result = IntPtr.Zero;
                    break;
                }
                UpdateData Candidate = CopyData.GetParam<UpdateData>(m.LParam);
                if (!IsSimpleExecutableName(Candidate.FileName) || !IsSha256(Candidate.UpdateHash)
                || !TryGetExpectedApplicationPath(out string ExpectedApplicationPath)) {
                    m.Result = IntPtr.Zero;
                    break;
                }
                Candidate.UpdateHash = Candidate.UpdateHash.ToLowerInvariant();
                Candidate.FileName = ExpectedApplicationPath;
                UpdateData = Candidate;
                UpdateDataAccepted = true;
#if DEBUG
                if (AuditIpcMode) {
                    AuditWrite("accepted=1");
                    AuditWrite($"target={Candidate.FileName}");
                    AuditWrite($"version={Candidate.NewVersion}");
                    AuditWrite($"hash={Candidate.UpdateHash}");
                    AuditWrite($"app-pid={ApplicationData.ProcessID}");
                    AuditWrite($"updater-pid={Process.GetCurrentProcess().Id}");
                    AuditWrite($"ptr-size={IntPtr.Size}");
                    AuditWrite($"update-size={Marshal.SizeOf<UpdateData>()}");
                    AuditWrite($"copydata-size={Marshal.SizeOf<CopyDataStruct>()}");
                }
#endif
                _ = CopyData.TrySendMessage(ApplicationData.MessageHandle, CopyData.WM_UPDATERREADY, this.Handle, 0, ParentMessageTimeoutMilliseconds);
#if DEBUG
                if (AuditIpcMode) {
                    AuditWrite("ack-sent=1");
                    if (AuditScenario.Equals("cancel", StringComparison.OrdinalIgnoreCase)) {
                        BeginInvoke((Action)(() => Program.CancelToken.Cancel()));
                    }
                }
#endif
                m.Result = IntPtr.Zero;
            } break;
            default: base.WndProc(ref m); break;
        }
    }

    private async void frmUpdater_Shown(object sender, EventArgs e) {
        await RunUpdate();
    }

    private static string CreateUpdateDestinationPath() =>
        Path.Combine(Environment.CurrentDirectory, $"update.{Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}.part");

    private void LoadLanguage() {
        this.Text = Language.frmUpdater;
        lbUpdaterHeader.Text = Language.lbUpdaterHeader;
        lbUpdaterDetails.Text = Language.lbUpdaterDetails;
        pbDownloadProgress.Text = Language.pbDownloadProgressPreparing;
    }
    private async Task RunUpdate() {
        // Fail closed unless the successful launch path explicitly clears the exit code.
        Program.ExitCode = 1;
        CancellationToken Cancellation = Program.CancelToken.Token;
        Cancellation.ThrowIfCancellationRequested();

        // Check if the latest version needs to be downloaded.
        if (DownloadLatest) {
            await GetVersionFromGithub();
            Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(UpdateData.FileName)) {
                Program.ExitCode = 1;
                return;
            }
        }

        // Wait for the main application to exit.
        if (ProgramProcess is not null) {
            try {
                await WaitForApplication();
            }
            catch (OperationCanceledException) {
#if DEBUG
                if (AuditIpcMode) {
                    AuditWrite("cancelled=1");
                    BeginInvoke((Action)Dispose);
                }
#endif
                return;
            }
            catch (Exception ex) {
                Log.ReportException(ex, "The updater could not wait for the main application to exit.", this);
                return;
            }
        }

        Cancellation.ThrowIfCancellationRequested();

        // This will be the backup location for the current version.
        string BackupLocation = UpdateData.FileName + ".old";

        // The temp path of the file.
        string UpdateDestination = CreateUpdateDestinationPath();

        // The URL that will be downloaded using the client
        string FileUrl = string.Format(ApplicationDownloadUrl, Language.ApplicationName, UpdateData.NewVersion.ToString());
#if DEBUG
        if (AuditIpcMode) {
            string AuditUrl = Environment.GetEnvironmentVariable("YTDL_AUDIT_UPDATE_URL");
            if (!string.IsNullOrWhiteSpace(AuditUrl)) FileUrl = AuditUrl;
        }
#endif

        // Whethre the old and new version have been moved.
        bool MovedOldVersion = false, MovedNewVersion = false;

        try {
            Cancellation.ThrowIfCancellationRequested();
            // Delete the old backup, if it exists, since it's clearly unused.
            if (File.Exists(BackupLocation))
                File.Delete(BackupLocation);

            // Add the events.
            Program.DownloadClient.ProgressChanged += DownloadProgressChanged;
            Program.DownloadClient.DownloadComplete += DownloadCompleted;

            // Download the update.
            await GetUpdate(FileUrl, UpdateDestination);
            Cancellation.ThrowIfCancellationRequested();

            // Check the file size.
            if (new FileInfo(UpdateDestination).Length <= 512) {
                File.Delete(UpdateDestination);
                tmrForm.Stop();
                this.Text = this.Text.Trim('.');
                pbDownloadProgress.Style = ProgressBarStyle.Blocks;
                pbDownloadProgress.ProgressState = ProgressState.Error;
                pbDownloadProgress.Text = Language.pbDownloadProgressDownloadTooSmall;
                return;
            }

            if (string.IsNullOrWhiteSpace(UpdateData.UpdateHash)) {
                throw new CryptographicException("The selected release does not include a valid executable SHA-256 hash.");
            }

            // Verify the file hash.
            pbDownloadProgress.Text = "calculating new update hash...";
            await VerifyHash(FileUrl, UpdateDestination);
            Cancellation.ThrowIfCancellationRequested();

            // Move the old file to the backup path.
            if (File.Exists(UpdateData.FileName)) {
                Cancellation.ThrowIfCancellationRequested();
                File.Move(UpdateData.FileName, BackupLocation);
                MovedOldVersion = true;
            }

            // Move the new update to the last location.
            Cancellation.ThrowIfCancellationRequested();
            File.Move(UpdateDestination, UpdateData.FileName);
            MovedNewVersion = true;
#if DEBUG
            if (AuditIpcMode && AuditScenario.Equals("rollback", StringComparison.OrdinalIgnoreCase)) {
                AuditWrite("rollback-injected=1");
                throw new InvalidOperationException("Audit-only rollback injection.");
            }
            if (AuditIpcMode) {
                AuditWrite("replacement=1");
                Program.ExitCode = 0;
                this.DialogResult = DialogResult.OK;
                this.Dispose();
                return;
            }
#endif

            // Finally, run it.
            Cancellation.ThrowIfCancellationRequested();
            pbDownloadProgress.Value = pbDownloadProgress.Maximum;
            pbDownloadProgress.Style = ProgressBarStyle.Blocks;
            pbDownloadProgress.Text = Language.pbDownloadProgressDownloadFinishedLaunching;
            Process.Start(UpdateData.FileName);

            // Kill this process.
            Program.ExitCode = 0;
            this.DialogResult = DialogResult.OK;
            this.Dispose();
        }
        catch (OperationCanceledException) {
            try {
                if (MovedNewVersion) {
                    if (File.Exists(UpdateData.FileName)) File.Delete(UpdateData.FileName);
                    if (MovedOldVersion && File.Exists(BackupLocation)) File.Move(BackupLocation, UpdateData.FileName);
                }
                else if (MovedOldVersion && File.Exists(BackupLocation)) File.Move(BackupLocation, UpdateData.FileName);
                if (File.Exists(UpdateDestination)) File.Delete(UpdateDestination);
            }
            catch (Exception rollbackEx) { Debug.WriteLine($"Updater cancellation rollback failed: {rollbackEx}"); }
#if DEBUG
            if (AuditIpcMode) {
                AuditWrite("cancelled=1");
                this.Dispose();
            }
#endif
        }
        catch {
            tmrForm.Stop();
            this.Text = this.Text.Trim('.');
            pbDownloadProgress.Style = ProgressBarStyle.Blocks;
            pbDownloadProgress.ProgressState = ProgressState.Error;
            pbDownloadProgress.Text = Language.pbDownloadProgressErrorProcessingDownload;

            try {
                if (MovedNewVersion) {
                    File.Delete(UpdateData.FileName);
                    if (MovedOldVersion && File.Exists(BackupLocation)) {
                        File.Move(BackupLocation, UpdateData.FileName);
                    }
                }
                else if (MovedOldVersion && File.Exists(BackupLocation)) {
                    File.Move(BackupLocation, UpdateData.FileName);
                }
                else if (File.Exists(UpdateDestination)) {
                    File.Delete(UpdateDestination);
                    pbDownloadProgress.Text = Language.pbDownloadProgressErrorDownloading;
                }
            }
            catch (Exception rollbackEx) {
                Log.ReportException(rollbackEx, "The update failed and the previous application version could not be restored.", this);
            }
#if DEBUG
            if (AuditIpcMode) {
                AuditWrite("rollback=1");
                this.Dispose();
            }
#endif
        }
    }
    private async Task GetVersionFromGithub() {
        try {
            Program.CancelToken.Token.ThrowIfCancellationRequested();
            UpdateData = await Github.GetUpdateData();
            Program.CancelToken.Token.ThrowIfCancellationRequested();
            Process RunningProcess = Process.GetProcessesByName(Language.ApplicationName).FirstOrDefault();
            if (RunningProcess != default)
                ProgramProcess = RunningProcess;
        }
        catch (OperationCanceledException) when (Program.CancelToken.IsCancellationRequested) { throw; }
        catch {
            if (!this.IsDisposed) pbDownloadProgress.Text = "could not get latest version.";
        }
    }
    private async Task WaitForApplication() {
        WaitingForApplication = true;
        try {
            Program.CancelToken.Token.ThrowIfCancellationRequested();

            // We are gonna gather the update data from the running process.
            // WM_UPDATEREADY is a non-standard message that tells youtube-dl-gui to send the updater is ready and that it should close.
            // The updater is going to wait for the main program to exit, allowing the user to finish any in-progress downloads.
            if (!CopyData.TrySendMessage(
                ApplicationData.MessageHandle,
                CopyData.WM_UPDATEDATAREQUEST,
                this.Handle,
                0,
                ParentMessageTimeoutMilliseconds)) {
                throw new TimeoutException("The main application did not accept the updater request within the allowed time.");
            }
            pbDownloadProgress.Text = Language.pbDownloadProgressWaitingForClose;

            // Wait for the exit without terminating the main application when updater cancellation is requested.
            await Task.Run(() => {
                while (!ProgramProcess.WaitForExit(100)) {
                    Program.CancelToken.Token.ThrowIfCancellationRequested();
                }
            }, Program.CancelToken.Token);
        }
        finally {
            WaitingForApplication = false;
        }
    }
    private async Task GetUpdate(string FileUrl, string FileDestination) {
        // The progress bar has a max of 200. Half of it is used for the download progress.
        pbDownloadProgress.Invoke(() => {
            pbDownloadProgress.Style = ProgressBarStyle.Blocks;
            pbDownloadProgress.Value = 50;
        });

        // Delete the previous download part file, if it exists.
        if (File.Exists(FileDestination))
            File.Delete(FileDestination);

        // Set the style to blocks so progress can be reported.
        pbDownloadProgress.Invoke(() => {
            pbDownloadProgress.Style = ProgressBarStyle.Blocks;
            pbDownloadProgress.Text = "0%";
        });

        bool CanRetry;
        int Retries = 0;

        // Going to attempt 5 times to download the update.
        // Additionally allows retrying in case it errors.
        do {
            try {
                // zzz
                await Program.DownloadClient.DownloadFileTaskAsync(new Uri(FileUrl, UriKind.Absolute), FileDestination, Program.CancelToken.Token);
                CanRetry = false;
            }

            // zzz
            catch (Exception ex) {
                while (ex.InnerException is not null)
                    ex = ex.InnerException;

                if (ex is ThreadAbortException or TaskCanceledException or OperationCanceledException)
                    throw ex;

                if (Retries != MaxRetries && (ex is not HttpException hex || (int)hex.StatusCode > 499)) {
                    await Task.Delay(RetryDelay, Program.CancelToken.Token);
                    Retries++;
                    CanRetry = true;
                    continue;
                }

                switch ((DialogResult)this.Invoke(() => Log.ReportException(ex, true, true, this))) {
                    case DialogResult.Abort: throw new OperationCanceledException("Abort requested");
                    case DialogResult.Retry: {
                        CanRetry = true;
                    } break;
                    default: throw;
                }
            }
        } while (CanRetry);

        // zzz
    }
    private async Task VerifyHash(string Url, string FileName) {
        while (true) {
            Program.CancelToken.Token.ThrowIfCancellationRequested();
            pbDownloadProgress.Text = Language.pbDownloadProgressCalculatingHash;

            byte[] Data;
            using (SHA256 CNG = SHA256.Create())
            using (FileStream UpdateFileStream = File.OpenRead(FileName)) {
                Data = await Task.Run(() => CNG.ComputeHash(UpdateFileStream), Program.CancelToken.Token);
            }
            Program.CancelToken.Token.ThrowIfCancellationRequested();

            string ReceivedHash = BitConverter.ToString(Data).Replace("-", "").ToLowerInvariant();
            string ExpectedHash = UpdateData.UpdateHash.ToLowerInvariant();
            if (ReceivedHash == ExpectedHash) {
                return;
            }

            pbDownloadProgress.Invoke(() => {
                pbDownloadProgress.Text = Language.pbDownloadProgressHashNoMatch;
                pbDownloadProgress.ProgressState = ProgressState.Paused;
            });

            switch ((DialogResult)this.Invoke(() => MessageBox.Show(this, string.Format(Language.dlgUpdaterUpdatedVersionHashNoMatch, ExpectedHash, ReceivedHash), Language.ApplicationName, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning))) {
                case DialogResult.Retry:
                    Program.CancelToken.Token.ThrowIfCancellationRequested();
                    File.Delete(FileName);
                    this.Invoke(() => {
                        tmrForm.Start();
                        pbDownloadProgress.Value = 50;
                        pbDownloadProgress.ProgressState = ProgressState.Normal;
                    });
                    await GetUpdate(Url, FileName);
                    continue;

                case DialogResult.Cancel:
                    throw new CryptographicException("The known hash of the file does not match the hash calculated by the updater.");
            }
        }
    }

    private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e) {
        if (this.IsDisposed || !this.IsHandleCreated) {
            return;
        }

        try {
            this.Invoke(() => {
                pbDownloadProgress.Value = (int)e.Percentage + 50;
                pbDownloadProgress.Text = e.Percentage.ToString();
            });
        }
        catch (InvalidOperationException) {
            // The updater can close after the handle check while a timer callback is being marshalled.
        }
    }
    private void DownloadCompleted(object sender, DownloadFinishedEventArgs e) {
        if (this.IsDisposed || !this.IsHandleCreated) {
            return;
        }

        try {
            pbDownloadProgress.Invoke(() => pbDownloadProgress.Text = "100%");
        }
        catch (InvalidOperationException) {
            // The updater can close after the handle check while completion is being marshalled.
        }
    }
    private void tmrForm_Tick(object sender, EventArgs e) {
        // This really is just for appearance.
        this.Text = this.Text.EndsWith("...") ? this.Text.Trim('.') : this.Text + ".";
    }
}
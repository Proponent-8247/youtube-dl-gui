namespace youtube_dl_gui_updater;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
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
    //private bool Received = false;


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

    protected override void OnFormClosing(FormClosingEventArgs e) {
        if (WaitingForApplication && !Program.CancelToken.IsCancellationRequested) {
            Program.CancelToken.Cancel();
        }
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m) {
        switch (m.Msg) {
            case CopyData.WM_COPYDATA: {
                if (ApplicationData.MessageHandle == 0 || m.WParam != ApplicationData.MessageHandle || m.LParam == IntPtr.Zero) {
                    m.Result = IntPtr.Zero;
                    break;
                }

                CopyDataStruct DataStruct;
                try {
                    DataStruct = m.GetCopyDataStructure();
                }
                catch {
                    m.Result = IntPtr.Zero;
                    break;
                }

                if (DataStruct.lpData == IntPtr.Zero || DataStruct.cbData != System.Runtime.InteropServices.Marshal.SizeOf<UpdateData>()) {
                    m.Result = IntPtr.Zero;
                    break;
                }

                UpdateData = CopyData.GetParam<UpdateData>(m.LParam);
                if (string.IsNullOrWhiteSpace(UpdateData.FileName) || string.IsNullOrWhiteSpace(UpdateData.UpdateHash)) {
                    m.Result = IntPtr.Zero;
                    break;
                }

                UpdateData.UpdateHash = UpdateData.UpdateHash.ToLowerInvariant();
                if (!UpdateData.FileName.ToLowerInvariant().EndsWith(".exe"))
                    UpdateData.FileName += ".exe";
                CopyData.SendMessage(ApplicationData.MessageHandle, CopyData.WM_UPDATERREADY, this.Handle, 0);
                //Received = true;
                m.Result = IntPtr.Zero;
            } break;
            default: {
                base.WndProc(ref m);
            } break;
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

        // Check if the latest version needs to be downloaded.
        if (DownloadLatest) {
            await GetVersionFromGithub();
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
                return;
            }
            catch (Exception ex) {
                Log.ReportException(ex, "The updater could not wait for the main application to exit.", this);
                return;
            }
        }

        // This will be the backup location for the current version.
        string BackupLocation = UpdateData.FileName + ".old";

        // The temp path of the file.
        string UpdateDestination = CreateUpdateDestinationPath();

        // The URL that will be downloaded using the client
        string FileUrl = string.Format(ApplicationDownloadUrl, Language.ApplicationName, UpdateData.NewVersion.ToString());

        // Whethre the old and new version have been moved.
        bool MovedOldVersion = false, MovedNewVersion = false;

        try {
            // Delete the old backup, if it exists, since it's clearly unused.
            if (File.Exists(BackupLocation))
                File.Delete(BackupLocation);

            // Add the events.
            Program.DownloadClient.ProgressChanged += DownloadProgressChanged;
            Program.DownloadClient.DownloadComplete += DownloadCompleted;

            // Download the update.
            await GetUpdate(FileUrl, UpdateDestination);

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

            // Move the old file to the backup path.
            if (File.Exists(UpdateData.FileName)) {
                File.Move(UpdateData.FileName, BackupLocation);
                MovedOldVersion = true;
            }

            // Move the new update to the last location.
            File.Move(UpdateDestination, UpdateData.FileName);
            MovedNewVersion = true;

            // Finally, run it.
            pbDownloadProgress.Value = pbDownloadProgress.Maximum;
            pbDownloadProgress.Style = ProgressBarStyle.Blocks;
            pbDownloadProgress.Text = Language.pbDownloadProgressDownloadFinishedLaunching;
            Process.Start(UpdateData.FileName);

            // Kill this process.
            Program.ExitCode = 0;
            this.DialogResult = DialogResult.OK;
            this.Dispose();
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
        }
    }
    private async Task GetVersionFromGithub() {
        try {
            UpdateData = await Github.GetUpdateData();
            Process RunningProcess = Process.GetProcessesByName(Language.ApplicationName).FirstOrDefault();
            if (RunningProcess != default)
                ProgramProcess = RunningProcess;
        }
        catch {
            pbDownloadProgress.Text = "could not get latest version.";
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
                    await Task.Delay(RetryDelay);
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
            pbDownloadProgress.Text = Language.pbDownloadProgressCalculatingHash;

            byte[] Data;
            using (SHA256 CNG = SHA256.Create())
            using (FileStream UpdateFileStream = File.OpenRead(FileName)) {
                Data = await Task.Run(() => CNG.ComputeHash(UpdateFileStream));
            }

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
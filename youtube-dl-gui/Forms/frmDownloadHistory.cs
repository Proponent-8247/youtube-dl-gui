#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class frmDownloadHistory : Form {
    private readonly CheckBox chkEnabled = new();
    private readonly TextBox txtArchive = new();
    private readonly Button btnBrowse = new();
    private readonly CheckBox chkBackup = new();
    private readonly CheckBox chkFailUnavailable = new();
    private readonly TextBox txtInventoryRoots = new();
    private readonly Button btnAddInventoryRoot = new();
    private readonly Label lbStatus = new();
    private readonly Label lbCounts = new();
    private readonly Button btnValidate = new();
    private readonly Button btnRebuild = new();
    private readonly Button btnOpen = new();
    private readonly Button btnReset = new();
    private readonly Button btnSave = new();
    private readonly Button btnCancel = new();
    private string pendingFileNameSchema = Downloads.fileNameSchema;
    private bool managementOperationInProgress;

    public frmDownloadHistory() {
        Text = "Download History / Duplicate Prevention";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new(660, 466);

        chkEnabled.Text = "Track previously downloaded media";
        chkEnabled.AutoSize = true;
        chkEnabled.Location = new(18, 18);
        chkEnabled.Checked = DownloadHistory.Enabled;

        Label pathLabel = new() { Text = "Download archive:", AutoSize = true, Location = new(18, 58) };
        txtArchive.Location = new(18, 78);
        txtArchive.Size = new(540, 23);
        txtArchive.Text = DownloadHistory.ArchivePath.IsNullEmptyWhitespace() ? DownloadHistory.EffectiveArchivePath : DownloadHistory.ArchivePath;
        btnBrowse.Text = "Browse...";
        btnBrowse.Location = new(566, 76);
        btnBrowse.Size = new(76, 27);
        btnBrowse.Click += BrowseArchive;

        chkBackup.Text = "Keep backup archive";
        chkBackup.AutoSize = true;
        chkBackup.Location = new(18, 118);
        chkBackup.Checked = DownloadHistory.KeepBackup;

        chkFailUnavailable.Text = "Stop if archive location is inaccessible (required)";
        chkFailUnavailable.AutoSize = true;
        chkFailUnavailable.Location = new(18, 143);
        chkFailUnavailable.Checked = true;
        chkFailUnavailable.Enabled = false;

        Label inventoryLabel = new() { Text = "Additional existing library folders (scan-only; one per line):", AutoSize = true, Location = new(18, 173) };
        txtInventoryRoots.Location = new(18, 193);
        txtInventoryRoots.Size = new(540, 66);
        txtInventoryRoots.Multiline = true;
        txtInventoryRoots.ScrollBars = ScrollBars.Vertical;
        txtInventoryRoots.Text = DownloadHistory.InventoryRoots.Replace("|", Environment.NewLine);
        btnAddInventoryRoot.Text = "Add Folder...";
        btnAddInventoryRoot.Location = new(566, 193);
        btnAddInventoryRoot.Size = new(76, 27);
        btnAddInventoryRoot.Click += AddInventoryRoot;

        Label recovery = new() {
            Text = "The archive tracks native provider identities and remains valid if media moves. Additional libraries are scan-only; existing media and sidecars are never renamed, moved, or rewritten.",
            AutoSize = false,
            Location = new(18, 273),
            Size = new(624, 48)
        };

        lbStatus.Location = new(18, 322);
        lbStatus.Size = new(624, 38);
        lbStatus.AutoEllipsis = true;
        lbCounts.Location = new(18, 361);
        lbCounts.Size = new(624, 20);
        lbCounts.AutoEllipsis = true;

        btnValidate.Text = "Validate Archive";
        btnValidate.Location = new(18, 414);
        btnValidate.Size = new(105, 28);
        btnValidate.Click += (_, _) => ValidateArchive();
        btnRebuild.Text = "Rebuild Archive";
        btnRebuild.Location = new(129, 414);
        btnRebuild.Size = new(105, 28);
        btnRebuild.Click += (_, _) => RebuildArchive();
        btnOpen.Text = "Open Location";
        btnOpen.Location = new(240, 414);
        btnOpen.Size = new(95, 28);
        btnOpen.Click += OpenLocation;
        btnReset.Text = "Reset History";
        btnReset.Location = new(341, 414);
        btnReset.Size = new(95, 28);
        btnReset.Click += ResetHistory;
        btnSave.Text = "Save";
        btnSave.Location = new(472, 414);
        btnSave.Size = new(82, 28);
        btnSave.Click += SaveAndClose;
        btnCancel.Text = "Cancel";
        btnCancel.Location = new(560, 414);
        btnCancel.Size = new(82, 28);
        btnCancel.Click += (_, _) => Close();

        Controls.AddRange([chkEnabled, pathLabel, txtArchive, btnBrowse, chkBackup, chkFailUnavailable, inventoryLabel, txtInventoryRoots, btnAddInventoryRoot, recovery, lbStatus, lbCounts,
            btnValidate, btnRebuild, btnOpen, btnReset, btnSave, btnCancel]);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        FormClosing += (_, e) => {
            if (managementOperationInProgress) e.Cancel = true;
        };
        RefreshStatus(DownloadHistory.LastReport);
    }

    private void BrowseArchive(object? sender, EventArgs e) {
        string currentPath = txtArchive.Text.IsNullEmptyWhitespace() ? DownloadHistory.EffectiveArchivePath : txtArchive.Text;
        string? initialDirectory = null;
        try {
            string? candidate = Path.GetDirectoryName(Path.GetFullPath(Environment.ExpandEnvironmentVariables(currentPath)));
            if (candidate is not null && Directory.Exists(candidate)) initialDirectory = candidate;
        }
        catch { }

        string fileName = "yt-dlp-archive.txt";
        try {
            string candidateName = Path.GetFileName(currentPath);
            if (!candidateName.IsNullEmptyWhitespace()) fileName = candidateName;
        }
        catch { }

        using SaveFileDialog dialog = new() {
            Title = "Select yt-dlp download archive",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = fileName
        };
        if (initialDirectory is not null) dialog.InitialDirectory = initialDirectory;
        if (dialog.ShowDialog(this) == DialogResult.OK) txtArchive.Text = dialog.FileName;
    }

    private void AddInventoryRoot(object? sender, EventArgs e) {
        using FolderBrowserDialog dialog = new() { Description = "Add an existing media library to scan for Download History identities" };
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedPath.IsNullEmptyWhitespace()) return;
        List<string> roots = txtInventoryRoots.Lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (!roots.Any(root => string.Equals(root, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))) roots.Add(dialog.SelectedPath);
        txtInventoryRoots.Lines = roots.ToArray();
    }

    private string GetConfiguredInventoryRoots() => string.Join("|",
        txtInventoryRoots.Lines.Select(line => line.Trim()).Where(line => line.Length > 0));

    private bool TryGetCandidate(bool showPrompts, out string configuredArchivePath) {
        configuredArchivePath = NormalizeConfiguredPath(txtArchive.Text);
        if (!chkEnabled.Checked || DownloadHistory.HasRequiredIdTemplate(pendingFileNameSchema)) return true;
        if (!showPrompts) return false;

        string recommended = DownloadHistory.AddRequiredIdTemplate(pendingFileNameSchema);
        DialogResult answer = MessageBox.Show(this,
            "Cannot enable Download History because the output filename does not contain %(id)s.\r\n\r\n" +
            "IDs are required so the archive can be validated or reconstructed if it is lost or damaged.\r\n\r\n" +
            "Current:\r\n" + pendingFileNameSchema + "\r\n\r\n" +
            "Recommended:\r\n" + recommended + "\r\n\r\n" +
            "Update the filename format automatically?",
            "Download History requires media IDs", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return false;
        pendingFileNameSchema = recommended;
        return true;
    }

    private static string NormalizeConfiguredPath(string value) {
        if (value.IsNullEmptyWhitespace()) return string.Empty;
        try {
            string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
            string implicitPath = DownloadHistory.EverEnabled && !DownloadHistory.BoundArchivePath.IsNullEmptyWhitespace()
                ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(DownloadHistory.BoundArchivePath))
                : Path.GetFullPath(DownloadHistory.DefaultArchivePath);
            return string.Equals(full, implicitPath, StringComparison.OrdinalIgnoreCase) ? string.Empty : value.Trim();
        }
        catch { return value.Trim(); }
    }

    private void SetManagementOperationInProgress(bool inProgress) {
        managementOperationInProgress = inProgress;
        chkEnabled.Enabled = !inProgress;
        txtArchive.Enabled = !inProgress;
        btnBrowse.Enabled = !inProgress;
        chkBackup.Enabled = !inProgress;
        txtInventoryRoots.Enabled = !inProgress;
        btnAddInventoryRoot.Enabled = !inProgress;
        btnValidate.Enabled = !inProgress;
        btnRebuild.Enabled = !inProgress;
        btnOpen.Enabled = !inProgress;
        btnReset.Enabled = !inProgress;
        btnSave.Enabled = !inProgress;
        btnCancel.Enabled = !inProgress;
        chkFailUnavailable.Enabled = false;
        ControlBox = !inProgress;
        UseWaitCursor = inProgress;
    }

    private async Task<DownloadHistoryReport?> RunManagementOperationAsync(Func<DownloadHistoryReport> operation, string title) {
        if (managementOperationInProgress) return null;
        SetManagementOperationInProgress(true);
        try {
            return await Task.Run(operation);
        }
        catch (Exception ex) {
            MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally {
            SetManagementOperationInProgress(false);
        }
    }

    private async void ValidateArchive() {
        if (!TryGetCandidate(false, out string configuredArchivePath)) {
            RefreshStatus(new DownloadHistoryReport {
                State = DownloadHistoryState.Unsafe,
                Message = "The candidate filename format does not contain %(id)s. Save/enable is blocked until the required media ID is added."
            });
            return;
        }

        string inventoryRoots = GetConfiguredInventoryRoots();
        DownloadHistoryReport? report = await RunManagementOperationAsync(
            () => DownloadHistory.AnalyzeLibrary(configuredArchivePath, inventoryRoots),
            "Download History validation");
        if (report is null) return;
        RefreshStatus(report);
        if (report.State is DownloadHistoryState.Partial or DownloadHistoryState.Unsafe or DownloadHistoryState.Unavailable or DownloadHistoryState.Invalid) {
            MessageBox.Show(this, report.Message, "Download History validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void RebuildArchive() {
        if (!TryGetCandidate(chkEnabled.Checked, out string configuredArchivePath)) return;
        string inventoryRoots = GetConfiguredInventoryRoots();
        bool keepBackup = chkBackup.Checked;
        DownloadHistoryReport? report = await RunManagementOperationAsync(
            () => DownloadHistory.RebuildLibrary(configuredArchivePath, keepBackup, false, inventoryRoots),
            "Download History rebuild");
        if (report is null) return;
        RefreshStatus(report);
        if (report.State != DownloadHistoryState.Healthy) {
            MessageBox.Show(this, report.Message, "Download History rebuild", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshStatus(DownloadHistoryReport report) {
        lbStatus.Text = "Status: " + report.State + (report.Message.IsNullEmptyWhitespace() ? string.Empty : " - " + report.Message);
        lbCounts.Text = $"Archive: {report.ArchiveEntries:N0}   Media: {report.CompletedMedia:N0}   Metadata: {report.MetadataRecovered:N0}   Filename IDs: {report.FilenameRecovered:N0}   Unresolved: {report.UnresolvedMedia:N0}";
    }

    private void OpenLocation(object? sender, EventArgs e) {
        try {
            string path = txtArchive.Text.IsNullEmptyWhitespace() ? DownloadHistory.EffectiveArchivePath : txtArchive.Text;
            string? directory = Path.GetDirectoryName(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)));
            if (directory is not null && Directory.Exists(directory)) {
                Process.Start(new ProcessStartInfo("explorer.exe", ArgumentList.EscapeArgument(directory)) { UseShellExecute = true });
            }
        }
        catch (Exception ex) {
            MessageBox.Show(this, ex.Message, "Open archive location", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetHistory(object? sender, EventArgs e) {
        if (DownloadHistory.Enabled) {
            MessageBox.Show(this,
                "Reset History is separate from disabling protection. Disable Download History and save that change first; then reopen this dialog to reset the preserved archive deliberately.",
                "Disable before reset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string savedArchive = DownloadHistory.EffectiveArchivePath;
        string candidateArchive;
        try {
            string configured = NormalizeConfiguredPath(txtArchive.Text);
            candidateArchive = configured.IsNullEmptyWhitespace()
                ? DownloadHistory.DefaultArchivePath
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        }
        catch { candidateArchive = string.Empty; }
        if (!string.Equals(candidateArchive, savedArchive, StringComparison.OrdinalIgnoreCase)) {
            MessageBox.Show(this,
                "Reset History operates only on the currently saved archive. The archive-path field contains an unsaved change. Save or revert that path before resetting history.\r\n\r\nSaved archive:\r\n" + savedArchive,
                "Unsaved archive path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(this,
            "Reset Download History deletes the saved archive and backup. Existing media files are not deleted.\r\n\r\n" +
            "Saved archive:\r\n" + savedArchive + "\r\n\r\n" +
            "If you enable protection again, the existing library must be reconstructed from authoritative IDs/metadata before any protected download can start.\r\n\r\nReset the saved history now?",
            "Reset Download History", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try {
            DownloadHistory.ResetHistory();
            RefreshStatus(DownloadHistory.LastReport);
        }
        catch (Exception ex) {
            MessageBox.Show(this, ex.Message, "Reset Download History", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void SaveAndClose(object? sender, EventArgs e) {
        if (!TryGetCandidate(true, out string configuredArchivePath)) return;
        string inventoryRoots = GetConfiguredInventoryRoots();
        bool keepBackup = chkBackup.Checked;

        if (!chkEnabled.Checked) {
            try {
                DownloadHistory.CommitSettings(false, configuredArchivePath, keepBackup, null, inventoryRoots);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "Download History settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        DownloadHistoryReport? report = await RunManagementOperationAsync(
            () => DownloadHistory.ReconcileLibrary(configuredArchivePath, keepBackup, false, inventoryRoots),
            "Download History settings");
        if (report is null) return;
        RefreshStatus(report);
        if (report.State != DownloadHistoryState.Healthy) {
            MessageBox.Show(this, report.Message, "Download History not enabled safely", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string oldSchema = Downloads.fileNameSchema;
        try {
            Downloads.fileNameSchema = pendingFileNameSchema;
            DownloadHistory.CommitSettings(true, configuredArchivePath, keepBackup, report, inventoryRoots);
        }
        catch (Exception ex) {
            Downloads.fileNameSchema = oldSchema;
            MessageBox.Show(this, ex.Message, "Download History settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}

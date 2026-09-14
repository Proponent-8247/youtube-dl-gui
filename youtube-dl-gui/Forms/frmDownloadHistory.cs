#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal sealed class frmDownloadHistory : Form {
    private readonly CheckBox chkEnabled = new();
    private readonly TextBox txtArchive = new();
    private readonly Button btnBrowse = new();
    private readonly CheckBox chkBackup = new();
    private readonly CheckBox chkFailUnavailable = new();
    private readonly Label lbStatus = new();
    private readonly Label lbCounts = new();
    private readonly Button btnValidate = new();
    private readonly Button btnRebuild = new();
    private readonly Button btnOpen = new();
    private readonly Button btnSave = new();
    private readonly Button btnCancel = new();

    public frmDownloadHistory() {
        Text = "Download History / Duplicate Prevention";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new(620, 330);

        chkEnabled.Text = "Track previously downloaded media";
        chkEnabled.AutoSize = true;
        chkEnabled.Location = new(18, 18);
        chkEnabled.Checked = DownloadHistory.Enabled;

        Label pathLabel = new() { Text = "Download archive:", AutoSize = true, Location = new(18, 58) };
        txtArchive.Location = new(18, 78);
        txtArchive.Size = new(500, 23);
        txtArchive.Text = DownloadHistory.ArchivePath.IsNullEmptyWhitespace() ? DownloadHistory.EffectiveArchivePath : DownloadHistory.ArchivePath;
        btnBrowse.Text = "Browse...";
        btnBrowse.Location = new(526, 76);
        btnBrowse.Size = new(76, 27);
        btnBrowse.Click += BrowseArchive;

        chkBackup.Text = "Keep backup archive";
        chkBackup.AutoSize = true;
        chkBackup.Location = new(18, 118);
        chkBackup.Checked = DownloadHistory.KeepBackup;

        chkFailUnavailable.Text = "Stop if archive location is inaccessible";
        chkFailUnavailable.AutoSize = true;
        chkFailUnavailable.Location = new(18, 143);
        chkFailUnavailable.Checked = DownloadHistory.FailIfUnavailable;

        Label recovery = new() {
            Text = "Recovery uses authoritative .info.json metadata and IDs embedded in completed filenames. IDs are mandatory while protection is enabled.",
            AutoSize = false,
            Location = new(18, 175),
            Size = new(584, 36)
        };

        lbStatus.Location = new(18, 216);
        lbStatus.Size = new(584, 20);
        lbCounts.Location = new(18, 239);
        lbCounts.Size = new(584, 20);

        btnValidate.Text = "Validate Archive";
        btnValidate.Location = new(18, 272);
        btnValidate.Size = new(105, 28);
        btnValidate.Click += (_, _) => ValidateArchive(false);
        btnRebuild.Text = "Rebuild Archive";
        btnRebuild.Location = new(129, 272);
        btnRebuild.Size = new(105, 28);
        btnRebuild.Click += (_, _) => ValidateArchive(true);
        btnOpen.Text = "Open Location";
        btnOpen.Location = new(240, 272);
        btnOpen.Size = new(95, 28);
        btnOpen.Click += OpenLocation;
        btnSave.Text = "Save";
        btnSave.Location = new(424, 272);
        btnSave.Size = new(82, 28);
        btnSave.Click += SaveAndClose;
        btnCancel.Text = "Cancel";
        btnCancel.Location = new(520, 272);
        btnCancel.Size = new(82, 28);
        btnCancel.Click += (_, _) => Close();

        Controls.AddRange([chkEnabled, pathLabel, txtArchive, btnBrowse, chkBackup, chkFailUnavailable, recovery, lbStatus, lbCounts, btnValidate, btnRebuild, btnOpen, btnSave, btnCancel]);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        RefreshStatus(DownloadHistory.LastReport);
    }

    private void BrowseArchive(object? sender, EventArgs e) {
        using SaveFileDialog dialog = new() {
            Title = "Select yt-dlp download archive",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = Path.GetFileName(txtArchive.Text.IsNullEmptyWhitespace() ? "yt-dlp-archive.txt" : txtArchive.Text),
            InitialDirectory = Path.GetDirectoryName(txtArchive.Text)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) txtArchive.Text = dialog.FileName;
    }

    private bool ApplySettings(bool showPrompts) {
        if (chkEnabled.Checked && !DownloadHistory.HasRequiredIdTemplate(Downloads.fileNameSchema)) {
            if (!showPrompts) return false;
            DialogResult answer = MessageBox.Show(this,
                "Cannot enable Download History because the filename format does not contain %(id)s.\r\n\r\n" +
                "IDs are required so the archive can be validated or reconstructed if it is lost or damaged.\r\n\r\n" +
                "Update the filename format automatically?",
                "Download History requires media IDs", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return false;
            Downloads.fileNameSchema = DownloadHistory.AddRequiredIdTemplate(Downloads.fileNameSchema);
        }

        DownloadHistory.ArchivePath = NormalizeConfiguredPath(txtArchive.Text);
        DownloadHistory.KeepBackup = chkBackup.Checked;
        DownloadHistory.FailIfUnavailable = chkFailUnavailable.Checked;
        DownloadHistory.Enabled = chkEnabled.Checked;
        return true;
    }

    private static string NormalizeConfiguredPath(string value) {
        if (value.IsNullEmptyWhitespace()) return string.Empty;
        try {
            string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
            string defaultPath = Path.GetFullPath(DownloadHistory.DefaultArchivePath);
            return string.Equals(full, defaultPath, StringComparison.OrdinalIgnoreCase) ? string.Empty : value.Trim();
        }
        catch { return value.Trim(); }
    }

    private void ValidateArchive(bool force) {
        if (!ApplySettings(true)) return;
        DownloadHistoryReport report = DownloadHistory.ValidateAndReconcile(force);
        RefreshStatus(report);
        if (report.State is DownloadHistoryState.Partial or DownloadHistoryState.Unsafe or DownloadHistoryState.Unavailable or DownloadHistoryState.Invalid) {
            MessageBox.Show(this, report.Message, "Download History validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshStatus(DownloadHistoryReport report) {
        lbStatus.Text = "Status: " + report.State + (report.Message.IsNullEmptyWhitespace() ? string.Empty : " - " + report.Message);
        lbCounts.Text = $"Archive entries: {report.ArchiveEntries:N0}   Completed media: {report.CompletedMedia:N0}   Identified: {report.IdentifiedMedia:N0}   Unresolved: {report.UnresolvedMedia:N0}";
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

    private void SaveAndClose(object? sender, EventArgs e) {
        if (!ApplySettings(true)) return;
        if (DownloadHistory.Enabled) {
            DownloadHistoryReport report = DownloadHistory.ValidateAndReconcile(true);
            RefreshStatus(report);
            if (report.State != DownloadHistoryState.Healthy) {
                MessageBox.Show(this, report.Message, "Download History not enabled safely", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}

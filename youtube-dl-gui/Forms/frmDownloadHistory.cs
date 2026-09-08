#nullable disable
namespace youtube_dl_gui;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using youtube_dl_gui.History;

// English fallback is intentional: existing language files remain compatible.
internal sealed class frmDownloadHistory : Form {
    private readonly CheckBox enabled = new CheckBox { Text = "Track previously downloaded media", AutoSize = true };
    private readonly RadioButton libraryScope = new RadioButton { Text = "One archive for this library", AutoSize = true };
    private readonly RadioButton customScope = new RadioButton { Text = "Custom archive", AutoSize = true };
    private readonly TextBox archivePath = new TextBox { Dock = DockStyle.Fill };
    private readonly Button browse = new Button { Text = "Browse...", AutoSize = true };
    private readonly ComboBox missing = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly CheckBox backup = new CheckBox { Text = "Keep a backup archive", AutoSize = true };
    private readonly CheckBox legacyYoutube = new CheckBox { Text = "Legacy filename-only IDs in this library are YouTube IDs (explicit declaration)", AutoSize = true };
    private readonly Label templateLabel = new Label { AutoSize = true };
    private readonly Label policy = new Label { AutoSize = true };
    private readonly Label libraryLabel = new Label { AutoSize = true };
    private readonly TextBox reportBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, MaxLength = int.MaxValue };
    private readonly Button apply = new Button { Text = "Apply", AutoSize = true };
    private readonly Button close = new Button { Text = "Close", AutoSize = true };
    private readonly Button cancel = new Button { Text = "Cancel scan", AutoSize = true, Enabled = false };
    private readonly TableLayoutPanel header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1 };
    private readonly FlowLayoutPanel actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
    private CancellationTokenSource cancellation;
    private bool busy;
    private string draftTemplate = Downloads.fileNameSchema;
    private string lastReportText = "";
    private string draftCustomPath = "";
    private bool previousCustomScope;

    public frmDownloadHistory() {
        Text = "Download History / Duplicate Prevention";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;
        MinimumSize = new System.Drawing.Size(640, 600);
        Size = new System.Drawing.Size(780, 700);
        ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(reportBox, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(close); footer.Controls.Add(apply); footer.Controls.Add(cancel);
        layout.Controls.Add(footer, 0, 3);
        header.Controls.Add(enabled);
        header.Controls.Add(libraryLabel);
        header.Controls.Add(templateLabel);
        var scope = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        scope.Controls.Add(libraryScope); scope.Controls.Add(customScope); header.Controls.Add(scope);
        var pathRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(archivePath, 0, 0); pathRow.Controls.Add(browse, 1, 0); header.Controls.Add(pathRow);
        var missingRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        missingRow.Controls.Add(new Label { Text = "When archive is missing:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        missing.Items.AddRange(new object[] { "Rebuild from the existing library", "Stop and ask for an explicit rebuild" });
        missingRow.Controls.Add(missing); header.Controls.Add(missingRow);
        header.Controls.Add(backup); header.Controls.Add(legacyYoutube);
        policy.Text = "Protected runs require IDs in filenames, save .info.json identity metadata, ignore external yt-dlp configuration files, and abort missing fragments. Jobs sharing a library/archive are serialized. One source ID is shared across audio/video and quality choices. Maintenance actions take effect immediately; Apply saves settings.";
        header.Controls.Add(policy);
        AddAction("Validate", async () => { await Scan(store => store.Inspect()); });
        AddAction("Rebuild", async () => { await Scan(store => store.Reconcile(true)); });
        AddAction("Migrate filenames", Migrate);
        AddAction("Add filename IDs", () => { EnsureTemplate(); return Task.CompletedTask; });
        AddAction("Open location", () => { OpenLocation(); return Task.CompletedTask; });
        AddAction("Recover interrupted run", RecoverRun);
        AddAction("Undo interrupted migration", RecoverMigration);
        AddAction("Reset history", ResetHistory);
        AddAction("Export report", () => { ExportReport(); return Task.CompletedTask; });
        var preferences = DownloadHistorySettings.Current;
        enabled.Checked = preferences.Enabled;
        customScope.Checked = preferences.CustomArchive;
        libraryScope.Checked = !preferences.CustomArchive;
        draftCustomPath = preferences.ArchivePath;
        previousCustomScope = preferences.CustomArchive;
        archivePath.Text = preferences.ArchivePath;
        backup.Checked = preferences.KeepBackup;
        legacyYoutube.Checked = preferences.LegacyYoutube;
        missing.SelectedIndex = preferences.StopWhenMissing ? 1 : 0;
        RefreshScope(); RefreshLabels();
        reportBox.Text = (preferences.Enabled ? "Enabled - not validated by this dialog" : preferences.EverEnabled ? "Disabled - archive preserved, dormant / potentially stale" : "Disabled - never enabled")
            + Environment.NewLine + "Cached entry count: " + preferences.LastEntryCount
            + Environment.NewLine + "Last synchronized: " + (string.IsNullOrEmpty(preferences.LastSynchronized) ? "Never" : preferences.LastSynchronized)
            + Environment.NewLine + "An existing archive is not proof of a validated library.";
        customScope.CheckedChanged += (s, e) => RefreshScope();
        browse.Click += Browse;
        apply.Click += async (s, e) => await ApplySettings();
        close.Click += (s, e) => Close();
        cancel.Click += (s, e) => { cancellation?.Cancel(); cancel.Enabled = false; };
        FormClosing += (s, e) => { if (busy) { cancellation?.Cancel(); e.Cancel = true; } };
        SizeChanged += (s, e) => RefreshLabels();
    }
    private void AddAction(string text, Func<Task> work) {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += async (s, e) => {
            try { await work(); }
            catch (Exception ex) { ShowError(ex); }
        };
        actions.Controls.Add(button);
    }
    private void RefreshLabels() {
        int width = Math.Max(520, ClientSize.Width - 48);
        policy.MaximumSize = templateLabel.MaximumSize = libraryLabel.MaximumSize = new System.Drawing.Size(width, 0);
        libraryLabel.Text = "Library: " + Downloads.downloadPath;
        templateLabel.Text = "Filename format: " + draftTemplate;
    }
    private void RefreshScope() {
        if (previousCustomScope && !customScope.Checked) draftCustomPath = archivePath.Text;
        if (!previousCustomScope && customScope.Checked) archivePath.Text = draftCustomPath;
        previousCustomScope = customScope.Checked;
        archivePath.Enabled = browse.Enabled = customScope.Checked && !busy;
        if (!customScope.Checked) {
            try { archivePath.Text = Path.Combine(DownloadHistorySettings.LibraryPath, "yt-dlp-archive.txt"); }
            catch (Exception) { archivePath.Text = "Select a valid download library in Settings first."; }
        }
    }
    private DownloadHistoryPreferences Draft() {
        var value = DownloadHistorySettings.Current;
        value.ConfigurationError = "";
        value.Enabled = enabled.Checked;
        value.CustomArchive = customScope.Checked;
        value.ArchivePath = value.CustomArchive ? archivePath.Text.Trim() : "";
        value.KeepBackup = backup.Checked;
        value.StopWhenMissing = missing.SelectedIndex == 1;
        value.LegacyYoutube = legacyYoutube.Checked;
        return value;
    }
    private bool EnsureTemplate() {
        try { HistoryTemplate.Validate(draftTemplate); return true; }
        catch (HistoryException ex) {
            string suggestion = HistoryTemplate.Suggest(draftTemplate);
            if (MessageBox.Show(this, ex.Message + "\r\n\r\nSuggested filename format:\r\n" + suggestion + "\r\n\r\nUse this format when you apply settings?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
            try { HistoryTemplate.Validate(suggestion); }
            catch (HistoryException invalid) { ShowError(invalid); return false; }
            draftTemplate = suggestion; RefreshLabels(); return true;
        }
    }
    private void SetBusy(bool value) {
        busy = value; header.Enabled = actions.Enabled = apply.Enabled = close.Enabled = !value;
        cancel.Enabled = value;
        if (!value) RefreshScope();
    }
    private async Task<HistoryReport> Scan(Func<HistoryStore, HistoryReport> operation, bool checkProvider = false) {
        if (busy || !EnsureTemplate()) return null;
        DownloadHistorySettings.RequireIdle();
        var options = DownloadHistorySettings.Options(Draft(), draftTemplate);
        cancellation = new CancellationTokenSource();
        options.Cancelled = () => cancellation.IsCancellationRequested;
        SetBusy(true);
        reportBox.Text = "Scanning the library and checking authoritative source identities...";
        try {
            HistoryReport report = await Task.Run(() => {
                if (checkProvider) DownloadHistoryRuntime.CheckProvider(Verification.YoutubeDlPath);
                using (var store = HistoryStore.Open(options)) return operation(store);
            });
            if (cancellation.IsCancellationRequested) { reportBox.Text = "Cancelled. Any atomic operation already in progress was allowed to finish safely; settings were not enabled."; return null; }
            lastReportText = report.Summary;
            if (report.Unresolved.Count > 0) lastReportText += "\r\n\r\nAll unresolved files:\r\n" + string.Join("\r\n", report.Unresolved);
            if (report.Migrations.Count > 0) {
                lastReportText += "\r\n\r\nFiles needing ID migration:\r\n";
                lastReportText += string.Join("\r\n", report.Migrations.Select(m => m.MediaPath + " -> " + m.Identity.Entry));
            }
            if (report.Incomplete.Count > 0) lastReportText += "\r\n\r\nExcluded incomplete/temporary files:\r\n" + string.Join("\r\n", report.Incomplete);
            reportBox.Text = lastReportText;
            return report;
        }
        catch (OperationCanceledException) { reportBox.Text = "Scan cancelled. Protected downloading was not enabled."; return null; }
        catch (Exception ex) { ShowError(ex); return null; }
        finally { SetBusy(false); cancellation.Dispose(); cancellation = null; }
    }
    private async Task ApplySettings() {
        try {
            DownloadHistorySettings.RequireIdle();
            var value = Draft();
            HistoryReport report = null;
            if (value.Enabled) {
                if (!EnsureTemplate()) return;
                if (!DownloadHistorySettings.Enabled && MessageBox.Show(this,
                    "Enable source-ID duplicate prevention for this library?\r\n\r\nIDs are mandatory. Source metadata will be saved, external yt-dlp configs ignored, incomplete fragments rejected, and protected jobs serialized. Intentional second audio/video/quality copies need a separate library or disabled protection.\r\n\r\nUnidentified legacy files will block enabling; no empty archive will be created over them.",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                report = await Scan(store => {
                    var candidate = store.Inspect();
                    return candidate.Recoverable ? store.Reconcile() : candidate;
                }, true);
                if (report == null) return;
                if (!report.Recoverable) {
                    MessageBox.Show(this, "Protection was not enabled. Migrate metadata-backed filenames and resolve the listed unknown files first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            DownloadHistorySettings.Save(value, draftTemplate);
            if (report != null) DownloadHistorySettings.Record(report);
            if (!value.Enabled) reportBox.Text = "Download History is disabled. Archive and backup are preserved and no longer updated by new unprotected jobs. Re-enabling always reconciles the library. Keeping IDs in filenames is strongly recommended.";
            else reportBox.AppendText("\r\n\r\nSettings saved. Every protected download will perform its own locked preflight.");
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task Migrate() {
        var report = await Scan(store => store.Inspect());
        if (report == null || report.Migrations.Count == 0) return;
        if (MessageBox.Show(this, "Rename " + report.Migrations.Count + " media files and their associated metadata/subtitles to embed source IDs?\r\n\r\nTargets are checked before renaming. Existing files will never be overwritten. This maintenance action takes effect immediately.",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            await Scan(store => store.Migrate());
    }
    private async Task RecoverRun() {
        if (MessageBox.Show(this, "First stop every downloader and postprocessor using this library, including other GUI instances.\r\n\r\nConfirm they have all stopped before recovering an interrupted launch. A known live downloader will still block recovery.",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            await Scan(store => { store.RecoverInterruptedLaunch(); return store.Inspect(); });
    }
    private async Task RecoverMigration() {
        if (MessageBox.Show(this, "Roll back the recorded interrupted filename migration? Both/neither filename conflicts require manual resolution; nothing will be overwritten.",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            await Scan(store => { store.RecoverMigration(); return store.Inspect(); });
    }
    private async Task ResetHistory() {
        if (DownloadHistorySettings.Enabled) { MessageBox.Show(this, "Disable Download History and apply that change before resetting it.", Text); return; }
        if (MessageBox.Show(this, "Reset history for the selected archive?\r\n\r\nThe old archive, backup and checkpoint will be preserved with .reset names. Media is not deleted. Enabling protection again will re-import identifiable completed media; reset is not a bypass for validation.",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            await Scan(store => { store.Reset(); return store.Inspect(); });
    }
    private void Browse(object sender, EventArgs e) {
        using (var dialog = new SaveFileDialog { Title = "Choose the download archive", Filter = "Text archive (*.txt)|*.txt|All files (*.*)|*.*", FileName = "yt-dlp-archive.txt", CheckPathExists = true, OverwritePrompt = false })
            if (dialog.ShowDialog(this) == DialogResult.OK) archivePath.Text = dialog.FileName;
    }
    private void OpenLocation() {
        string directory = Path.GetDirectoryName(DownloadHistorySettings.Options(Draft(), draftTemplate).ArchivePath);
        if (!Directory.Exists(directory)) throw new HistoryException("The archive location is unavailable.");
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
    private void ExportReport() {
        if (string.IsNullOrEmpty(lastReportText)) { MessageBox.Show(this, "Run a scan before exporting its report.", Text); return; }
        using (var dialog = new SaveFileDialog { Title = "Export Download History scan report", Filter = "Text report (*.txt)|*.txt", FileName = "download-history-report.txt", OverwritePrompt = true })
            if (dialog.ShowDialog(this) == DialogResult.OK) {
                string path = Path.GetFullPath(dialog.FileName);
                var options = DownloadHistorySettings.Options(Draft(), draftTemplate);
                var saved = DownloadHistorySettings.Current;
                saved.ConfigurationError = "";
                string currentArchive = DownloadHistorySettings.Options(saved, draftTemplate).ArchivePath;
                if (Path.GetExtension(path) != ".txt" || path.StartsWith(options.ArchivePath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(currentArchive, StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(path).StartsWith(".ytdlg-history", StringComparison.OrdinalIgnoreCase))
                    throw new HistoryException("Choose a separate .txt report file, not an archive or history service file.");
                File.WriteAllText(path, lastReportText, new UTF8Encoding(false));
            }
    }
    private void ShowError(Exception ex) {
        reportBox.Text = ex.Message;
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

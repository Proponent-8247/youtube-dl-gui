#nullable disable
namespace youtube_dl_gui;
using System;
using System.Windows.Forms;
using youtube_dl_gui.History;

public partial class frmSettings {
    private void InitializeDownloadHistoryPage() {
        var page = new TabPage("Download History");
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
        var description = new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(270, 0), Text = "Source-ID duplicate prevention for standard and Extended downloads. Save the library and filename format above before configuring history." };
        var configure = new Button { Text = "Configure Download History...", AutoSize = true };
        content.Controls.Add(description); content.Controls.Add(configure); page.Controls.Add(content); tabDownloads.TabPages.Add(page);
        configure.Click += (sender, e) => {
            try {
                if (txtSettingsDownloadsSavePath.Text != Downloads.downloadPath || txtSettingsDownloadsFileNameSchema.Text != Downloads.fileNameSchema) {
                    MessageBox.Show(this, "Save the download folder and filename format first, then reopen Settings to configure Download History for that saved library.", "Download History");
                    return;
                }
                DownloadHistorySettings.RequireIdle();
                using (var dialog = new frmDownloadHistory()) dialog.ShowDialog(this);
                txtSettingsDownloadsFileNameSchema.Text = Downloads.fileNameSchema;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Download History", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
    }
    private bool CanSaveDownloadHistorySettings() {
        var history = DownloadHistorySettings.Current;
        if (history.Enabled) {
            if (txtSettingsDownloadsSavePath.Text != Downloads.downloadPath) {
                MessageBox.Show(this, "Disable Download History before changing the library root. Re-enable it afterward to reconcile the new location.", "Download History", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            try {
                HistoryTemplate.Validate(txtSettingsDownloadsFileNameSchema.Text);
                if (txtSettingsDownloadsFileNameSchema.Text != Downloads.fileNameSchema) DownloadHistorySettings.RequireIdle();
            }
            catch (Exception ex) {
                string suggestion = HistoryTemplate.Suggest(txtSettingsDownloadsFileNameSchema.Text);
                if (MessageBox.Show(this, ex.Message + "\r\n\r\nUse this filename format?\r\n" + suggestion,
                    "Download History", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
                try { HistoryTemplate.Validate(suggestion); DownloadHistorySettings.RequireIdle(); }
                catch (Exception invalid) { MessageBox.Show(this, invalid.Message, "Download History"); return false; }
                txtSettingsDownloadsFileNameSchema.Text = suggestion;
            }
        }
        else if (history.EverEnabled && txtSettingsDownloadsFileNameSchema.Text != Downloads.fileNameSchema) {
            try { HistoryTemplate.Validate(txtSettingsDownloadsFileNameSchema.Text); }
            catch (HistoryException) {
                if (MessageBox.Show(this, "Files downloaded with this format may not be safely identifiable when you re-enable Download History. Keeping %(id)s in media filenames is strongly recommended.\r\n\r\nSave this format anyway?",
                    "Download History", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
            }
        }
        return true;
    }
}

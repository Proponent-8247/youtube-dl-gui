#nullable enable
namespace youtube_dl_gui;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
public partial class frmAbout : LocalizedForm {
    private const string FlavorText = "alien slime drink it all the time";

    public frmAbout() {
        InitializeComponent();
        LoadLanguage();
        pbIcon.Image = Properties.Resources.AboutImage;
        pbIcon.Cursor = NativeMethods.SystemHandCursor;
        lbVersion.Text = $"v{Program.CurrentVersion}";
        llbCheckForUpdates.LinkVisited = Program.UpdateChecked;
        llbCheckForUpdates.Location = new(
            (this.ClientSize.Width - llbCheckForUpdates.Width) / 2,
            llbCheckForUpdates.Location.Y
        );

        if (Initialization.ScreenshotMode)
            this.FormClosing += (s, e) => this.Dispose();
    }

    public override void LoadLanguage() {
        lbAboutBody.Text = string.Format(Language.lbAboutBody + "\n\n\n" + FlavorText, "murrty", GeneratedBuildDate.Value);
        llbCheckForUpdates.Text = Language.llbCheckForUpdates;
        this.Text = $"{Language.frmAbout} youtube-dl-gui";
    }

    private async void llbCheckForUpdates_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
        if (!llbCheckForUpdates.Enabled) {
            return;
        }

        llbCheckForUpdates.Enabled = false;
        try {
            bool? UpdateAvailable = await Updater.CheckForUpdate(chkForceCheckUpdate.Checked);
            if (UpdateAvailable is null || this.IsDisposed || !this.IsHandleCreated) {
                return;
            }

            switch (UpdateAvailable) {
                case false: {
                    Log.MessageBox((Program.CurrentVersion.IsBeta ? Language.dlgUpdateNoBetaUpdateAvailable : Language.dlgUpdateNoUpdateAvailable)
                        .Format(Program.CurrentVersion, Updater.LastChecked!.Version));
                } break;
                case true: {
                    Updater.ShowUpdateForm(false);
                } break;
            }

            Program.UpdateChecked = true;
            if (!Program.IsUpdating) {
                llbCheckForUpdates.LinkVisited = true;
            }
        }
        catch (Exception ex) {
            if (ex is ThreadAbortException or OperationCanceledException or TaskCanceledException) {
                return;
            }

            if (!this.IsDisposed && this.IsHandleCreated) {
                Log.ReportException(ex);
            }
        }
        finally {
            if (!this.IsDisposed && this.IsHandleCreated) {
                llbCheckForUpdates.Enabled = true;
            }
        }
    }

    private void pbIcon_Click(object sender, EventArgs e) =>
        Process.Start("https://github.com/murrty/youtube-dl-gui/");

    private void llbGithub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) =>
        Process.Start("https://github.com/murrty/youtube-dl-gui");
}

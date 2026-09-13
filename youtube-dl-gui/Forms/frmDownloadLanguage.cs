#nullable enable
namespace youtube_dl_gui;
using System.Drawing;
using System.Windows.Forms;
using murrty.updater;
public partial class frmDownloadLanguage : LocalizedForm {
    private readonly GithubRepoContent[] EnumeratedLanguages;

    public string? FileName { get; private set; }

    private static bool IsReservedWindowsFileName(string FileName) {
        string Stem = System.IO.Path.GetFileNameWithoutExtension(FileName).TrimEnd(' ', '.');
        if (Stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            Stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            Stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            Stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return Stem.Length == 4 &&
            (Stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || Stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            Stem[3] >= '1' && Stem[3] <= '9';
    }

    private static bool TryGetLanguageOutputPath(string? FileName, out string Output) {
        Output = string.Empty;
        if (FileName.IsNullEmptyWhitespace() ||
            !FileName!.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ||
            FileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
            FileName.IndexOf('\\') >= 0 || FileName.IndexOf('/') >= 0 ||
            IsReservedWindowsFileName(FileName)) {
            return false;
        }

        try {
            string LanguageDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Environment.CurrentDirectory, "lang"));
            string Candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(LanguageDirectory, FileName));
            if (!string.Equals(System.IO.Path.GetDirectoryName(Candidate), LanguageDirectory, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            Output = Candidate;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is System.IO.PathTooLongException) {
            return false;
        }
    }

    public frmDownloadLanguage(GithubRepoContent[] AvailableLanguages) {
        InitializeComponent();
        LoadLanguage();

        try {
            EnumeratedLanguages = AvailableLanguages
                .Where(x => !x.download_url.IsNullEmptyWhitespace() && TryGetLanguageOutputPath(x.name, out _))
                .ToArray();

            if (EnumeratedLanguages.Length > 0) {
                // Uncomment these out when the SHA calcuation gets fixed.
                for (int i = 0; i < EnumeratedLanguages.Length; i++) {
                    GithubRepoContent Content = EnumeratedLanguages[i];
                    ListViewItem NewItem = new($"Item {Content.name}");
                    NewItem.SubItems[0].Text = $"{i + 1}: {(Content.name!.EndsWith(".ini") ? Content.name[..^4] : Content.name)} ({Content.size.SizeToString()})";
                    NewItem.UseItemStyleForSubItems = false;
                    NewItem.SubItems.Add(new ListViewItem.ListViewSubItem());
                    NewItem.SubItems[1].Text = Content.download_url;
                    //NewItem.SubItems[1].Text = $"{Content.Sha}";
                    NewItem.SubItems[1].ForeColor = Color.FromKnownColor(KnownColor.ScrollBar);
                    NewItem.SubItems[1].Font = this.Font;
                    //NewItem.ToolTipText = Content.DownloadUrl;
                    lvAvailableLanguages.Items.Add(NewItem);
                }
            }
        }
        catch (Exception ex) {
            EnumeratedLanguages = [];
            Log.ReportException(ex);
        }
    }

    public override void LoadLanguage() {
        if (Initialization.firstTime) {
            btnCancel.Text = Language.InternalEnglish.GenericCancel;
            btnOk.Text = Language.InternalEnglish.GenericOk;
            btnDownloadSelected.Text = Language.InternalEnglish.sbDownload;
            this.Text = Language.InternalEnglish.frmDownloadLanguage;
        }
        else {
            btnCancel.Text = Language.GenericCancel;
            btnOk.Text = Language.GenericOk;
            btnDownloadSelected.Text = Language.sbDownload;
            this.Text = Language.frmDownloadLanguage;
        }
    }

    private bool DownloadSelectedLanguageFile() {
        var lang = EnumeratedLanguages[lvAvailableLanguages.SelectedIndices[0]];

        Log.Write($"Downloading language file {lang.name}.");
        if (!TryGetLanguageOutputPath(lang.name, out string Output)) {
            Log.Write($"Rejected unsafe language file name {lang.name}.");
            return false;
        }
        string LanguageDirectory = System.IO.Path.GetDirectoryName(Output)!;
        if (!System.IO.Directory.Exists(LanguageDirectory)) {
            System.IO.Directory.CreateDirectory(LanguageDirectory);
        }
        string URL = lang.download_url!;
        if (lang.sha.IsNullEmptyWhitespace()) {
            Log.Write($"Rejected language file {lang.name}: GitHub did not provide an object identity.");
            Log.MessageBox(Language.dlgLanguageHashNoMatch, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            System.Media.SystemSounds.Hand.Play();
            return false;
        }
        string StagedOutput = Output + ".download." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using frmGenericDownloadProgress Downloader = new(URL, StagedOutput);
            if (Downloader.ShowDialog() == DialogResult.OK) {
                if (!Updater.CommitVerifiedGithubBlob(StagedOutput, Output, lang.sha!)) {
                    Log.Write($"Rejected language file {lang.name}: downloaded content did not match GitHub object {lang.sha}.");
                    Log.MessageBox(Language.dlgLanguageHashNoMatch, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    System.Media.SystemSounds.Hand.Play();
                    return false;
                }
                Log.Write($"Finished downloading and verifying language file {lang.name}");
                System.Media.SystemSounds.Asterisk.Play();
                return true;
            }

            Log.Write($"Could not download language file {lang.name}.");
            System.Media.SystemSounds.Hand.Play();
            return false;
        }
        finally {
            try { if (System.IO.File.Exists(StagedOutput)) System.IO.File.Delete(StagedOutput); } catch { }
        }
    }

    private void btnDownloadSelected_Click(object sender, EventArgs e) {
        if (DownloadSelectedLanguageFile()) {
            SetSelectedLanguageResult();
        }
    }

    private void btnCancel_Click(object sender, EventArgs e) {
        this.DialogResult = DialogResult.Cancel;
    }

    private void btnOk_Click(object sender, EventArgs e) {
        if (lvAvailableLanguages.SelectedIndices.Count > 0 && DownloadSelectedLanguageFile()) {
            SetSelectedLanguageResult();
            return;
        }
        if (lvAvailableLanguages.SelectedIndices.Count < 1) {
            FileName = null;
            this.DialogResult = DialogResult.Cancel;
        }
    }

    private void SetSelectedLanguageResult() {
        FileName = EnumeratedLanguages[lvAvailableLanguages.SelectedIndices[0]].name!;
        if (FileName.EndsWith(".ini")) {
            FileName = FileName[..^4];
        }
        this.DialogResult = DialogResult.OK;
    }

    private void lvAvailableLanguages_SelectedIndexChanged(object sender, EventArgs e) {
        btnOk.Enabled = lvAvailableLanguages.SelectedIndices.Count > 0;
        btnDownloadSelected.Enabled = lvAvailableLanguages.SelectedIndices.Count > 0;
    }
}
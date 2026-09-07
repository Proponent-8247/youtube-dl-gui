#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

public partial class frmMiscTools : LocalizedForm {
    public frmMiscTools() {
        InitializeComponent();
    }

    public override void LoadLanguage() {
        this.Text = Language.frmTools;
        btnMiscToolsRemoveAudio.Text = Language.btnMiscToolsRemoveAudio;
        btnMiscToolsExtractAudio.Text = Language.btnMiscToolsExtractAudio;
        btnMiscToolsVideoToGif.Text = Language.btnMiscToolsVideoToGif;
    }

    private void frmTools_FormClosing(object sender, FormClosingEventArgs e) {
        this.Dispose();
    }

    private void btnMiscToolsRemoveAudio_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        ofd.Title = "Select a file to remove the audio from";
        ofd.Filter = Formats.VideoFormats;
        ofd.FilterIndex = 0;
        if (ofd.ShowDialog() == DialogResult.OK) {
            string newFile = Path.GetDirectoryName(ofd.FileName) + "\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + "-noaudio" + Path.GetExtension(ofd.FileName);
            if (newFile.Length > 250) {
                newFile = Path.Combine(Path.GetDirectoryName(ofd.FileName) ?? Environment.CurrentDirectory, "output" + Path.GetExtension(ofd.FileName)); // Rare case, file is a lorge name
            }

            using Process ffmpeg = new() {
                StartInfo = new(Verification.FFmpegPath ?? "ffmpeg") {
                    UseShellExecute = false,
                    //RedirectStandardInput = true,
                    //RedirectStandardOutput = true,
                    //CreateNoWindow = true,
                    Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " -c copy -an " + ArgumentList.EscapeArgument(newFile),
                }
            };
            ffmpeg.Start();
        }
    }

    private void btnMiscToolsExtractAudio_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        ofd.Title = "Select a file to extract the audio from";
        ofd.Filter = Formats.VideoFormats;
        ofd.FilterIndex = 0;
        if (ofd.ShowDialog() == DialogResult.OK) {
            using SaveFileDialog sfd = new();
            sfd.Title = "Save audio as...";
            sfd.Filter = Formats.AudioFormats;
            sfd.FileName = Path.GetFileNameWithoutExtension(ofd.FileName);
            sfd.FilterIndex = 5;
            if (sfd.ShowDialog() == DialogResult.OK) {
                string newFile = sfd.FileName;

                using Process ffmpeg = new() {
                    StartInfo = new(Verification.FFmpegPath ?? "ffmpeg") {
                        UseShellExecute = false,
                        //RedirectStandardInput = true,
                        //RedirectStandardOutput = true,
                        //CreateNoWindow = true,
                        Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " " + ArgumentList.EscapeArgument(newFile),
                    }
                };
                ffmpeg.Start();
            }
        }
    }

    private void btnMiscToolsVideoToGif_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        if (ofd.ShowDialog() == DialogResult.OK) {
            string OutputDirectory = Path.GetDirectoryName(ofd.FileName) ?? Environment.CurrentDirectory;
            string FrameDirectory = Path.Combine(Path.GetTempPath(), "youtube-dl-gui", Path.GetRandomFileName());
            Directory.CreateDirectory(FrameDirectory);

            try {
                using Process ffmpeg = new() {
                    StartInfo = new(Verification.FFmpegPath ?? "ffmpeg") {
                        UseShellExecute = false,
                        WorkingDirectory = FrameDirectory,
                        Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " -vf scale=320:-1:flags=lanczos,fps=10 outframes%03d.png",
                    }
                };
                ffmpeg.Start();
                ffmpeg.WaitForExit();
                if (ffmpeg.ExitCode != 0) {
                    Directory.Delete(FrameDirectory, true);
                    return;
                }

                Process imageMagick = new() {
                    EnableRaisingEvents = true,
                    StartInfo = new("cmd") {
                        UseShellExecute = false,
                        WorkingDirectory = FrameDirectory,
                        Arguments = string.Format("/c \"{0}\"", "convert -loop 0 outframes*.png \"" + Path.Combine(OutputDirectory, Path.GetFileNameWithoutExtension(ofd.FileName) + ".gif") + "\""),
                    }
                };
                imageMagick.Exited += (s, args) => {
                    try {
                        Directory.Delete(FrameDirectory, true);
                    }
                    catch {
                        // Best-effort cleanup after ImageMagick releases the frame files.
                    }
                    imageMagick.Dispose();
                };

                try {
                    imageMagick.Start();
                }
                catch {
                    imageMagick.Dispose();
                    throw;
                }
            }
            catch {
                if (Directory.Exists(FrameDirectory)) {
                    Directory.Delete(FrameDirectory, true);
                }
                throw;
            }
        }
    }
}
#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

public partial class frmMiscTools : LocalizedForm {
    private static IEnumerable<string> GetExecutableSearchDirectories() {
        yield return Environment.CurrentDirectory;

        string? PathValue = Environment.GetEnvironmentVariable("PATH");
        if (PathValue.IsNullEmptyWhitespace()) {
            yield break;
        }

        foreach (string DirectoryValue in PathValue!.Split(';')) {
            string DirectoryPath = Environment.ExpandEnvironmentVariables(DirectoryValue.Trim().Trim('"'));
            if (!DirectoryPath.IsNullEmptyWhitespace()) {
                yield return DirectoryPath;
            }
        }
    }

    private static string? ResolveImageMagick() {
        string WindowsConvert = Path.GetFullPath(Path.Combine(Environment.SystemDirectory, "convert.exe"));

        foreach (string ExecutableName in new[] { "magick.exe", "convert.exe" }) {
            foreach (string DirectoryPath in GetExecutableSearchDirectories().Distinct(StringComparer.OrdinalIgnoreCase)) {
                string Candidate;
                try {
                    Candidate = Path.GetFullPath(Path.Combine(DirectoryPath, ExecutableName));
                }
                catch {
                    continue;
                }

                if (!File.Exists(Candidate)) {
                    continue;
                }
                if (ExecutableName.Equals("convert.exe", StringComparison.OrdinalIgnoreCase)
                && Candidate.Equals(WindowsConvert, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (IsImageMagick(Candidate)) {
                    return Candidate;
                }
            }
        }

        return null;
    }

    private static bool IsImageMagick(string ExecutablePath) {
        try {
            murrty.controls.OwnedProcess.Result VersionCheck = murrty.controls.OwnedProcess.Run(
                new ProcessStartInfo(ExecutablePath) { Arguments = "-version" },
                System.Threading.CancellationToken.None, 5_000, 65_536);
            string VersionOutput = VersionCheck.StandardOutput + VersionCheck.StandardError;
            return VersionCheck.ExitCode == 0
                && VersionOutput.IndexOf("ImageMagick", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch {
            return false;
        }
    }

    public frmMiscTools() {
        InitializeComponent();
        LoadLanguage();
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

    private async void btnMiscToolsVideoToGif_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        if (ofd.ShowDialog() == DialogResult.OK) {
            string OutputDirectory = Path.GetDirectoryName(ofd.FileName) ?? Environment.CurrentDirectory;
            string FrameDirectory = Path.Combine(Path.GetTempPath(), "youtube-dl-gui", Path.GetRandomFileName());
            Directory.CreateDirectory(FrameDirectory);
            btnMiscToolsVideoToGif.Enabled = false;

            try {
                using Process ffmpeg = new() {
                    StartInfo = new(Verification.FFmpegPath ?? "ffmpeg") {
                        UseShellExecute = false,
                        WorkingDirectory = FrameDirectory,
                        Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " -vf scale=320:-1:flags=lanczos,fps=10 outframes%03d.png",
                    }
                };
                ffmpeg.Start();
                await System.Threading.Tasks.Task.Run(() => ffmpeg.WaitForExit());
                if (ffmpeg.ExitCode != 0) {
                    Directory.Delete(FrameDirectory, true);
                    return;
                }

                string? ImageMagickPath = ResolveImageMagick();
                if (ImageMagickPath is null) {
                    Log.MessageBox("ImageMagick could not be found. Install ImageMagick and ensure magick.exe is available in PATH.");
                    Directory.Delete(FrameDirectory, true);
                    return;
                }

                string GifPath = Path.Combine(OutputDirectory, Path.GetFileNameWithoutExtension(ofd.FileName) + ".gif");
                Process imageMagick = new() {
                    EnableRaisingEvents = true,
                    StartInfo = new(ImageMagickPath) {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = FrameDirectory,
                        Arguments = "-delay 10 -loop 0 outframes*.png " + ArgumentList.EscapeArgument(GifPath),
                    }
                };
                imageMagick.Exited += (s, args) => {
                    int ExitCode = imageMagick.ExitCode;
                    try {
                        Directory.Delete(FrameDirectory, true);
                    }
                    catch {
                        // Best-effort cleanup after ImageMagick releases the frame files.
                    }
                    if (ExitCode != 0) {
                        Log.Write($"ImageMagick GIF conversion exited with code {ExitCode}.");
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
            finally {
                if (!this.IsDisposed && this.IsHandleCreated) {
                    btnMiscToolsVideoToGif.Enabled = true;
                }
            }
        }
    }
}
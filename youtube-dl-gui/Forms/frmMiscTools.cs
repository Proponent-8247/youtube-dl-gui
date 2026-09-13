#nullable enable
namespace youtube_dl_gui;

using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

public partial class frmMiscTools : LocalizedForm {
    private readonly System.Threading.CancellationTokenSource MiscOperationCancellation = new();

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

    private static string? ResolveImageMagick(System.Threading.CancellationToken Cancellation) {
        string WindowsConvert = Path.GetFullPath(Path.Combine(Environment.SystemDirectory, "convert.exe"));

        foreach (string ExecutableName in new[] { "magick.exe", "convert.exe" }) {
            foreach (string DirectoryPath in GetExecutableSearchDirectories().Distinct(StringComparer.OrdinalIgnoreCase)) {
                Cancellation.ThrowIfCancellationRequested();
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
                if (IsImageMagick(Candidate, Cancellation)) {
                    return Candidate;
                }
            }
        }

        return null;
    }

    private static bool IsImageMagick(string ExecutablePath) =>
        IsImageMagick(ExecutablePath, System.Threading.CancellationToken.None);

    private static bool IsImageMagick(string ExecutablePath, System.Threading.CancellationToken Cancellation) {
        try {
            murrty.controls.OwnedProcess.Result VersionCheck = murrty.controls.OwnedProcess.Run(
                new ProcessStartInfo(ExecutablePath) { Arguments = "-version" },
                Cancellation, 5_000, 65_536);
            string VersionOutput = VersionCheck.StandardOutput + VersionCheck.StandardError;
            return VersionCheck.ExitCode == 0
                && VersionOutput.IndexOf("ImageMagick", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return false;
        }
    }

    private static async System.Threading.Tasks.Task<int?> RunOwnedProcessAsync(ProcessStartInfo StartInfo, System.Threading.CancellationToken Cancellation) {
        if (StartInfo is null) throw new ArgumentNullException(nameof(StartInfo));
        Cancellation.ThrowIfCancellationRequested();

        using Process Child = new() { StartInfo = StartInfo };
        using murrty.controls.ProcessOwnership Ownership = new(Child);
        try {
            if (!Child.Start()) {
                Log.Write($"Failed to start {StartInfo.FileName}.");
                return null;
            }
            Ownership.Attach();
            await System.Threading.Tasks.Task.Run(() => {
                while (!Child.WaitForExit(100)) {
                    Cancellation.ThrowIfCancellationRequested();
                }
                Cancellation.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);
            return Child.ExitCode;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) {
            Log.Write($"Failed to start or monitor {StartInfo.FileName}: {ex.Message}");
            return null;
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
        MiscOperationCancellation.Cancel();
        this.Dispose();
    }

    private async void btnMiscToolsRemoveAudio_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        ofd.Title = "Select a file to remove the audio from";
        ofd.Filter = Formats.VideoFormats;
        ofd.FilterIndex = 0;
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string newFile = Path.GetDirectoryName(ofd.FileName) + "\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + "-noaudio" + Path.GetExtension(ofd.FileName);
        if (newFile.Length > 250) {
            newFile = Path.Combine(Path.GetDirectoryName(ofd.FileName) ?? Environment.CurrentDirectory, "output" + Path.GetExtension(ofd.FileName)); // Rare case, file is a lorge name
        }

        try {
            int? ExitCode = await RunOwnedProcessAsync(new(Verification.FFmpegPath ?? "ffmpeg") {
                UseShellExecute = false,
                Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " -c copy -an " + ArgumentList.EscapeArgument(newFile),
            }, MiscOperationCancellation.Token);
            if (ExitCode is int Code && Code != 0) {
                Log.Write($"FFmpeg remove-audio operation exited with code {Code}.");
            }
        }
        catch (OperationCanceledException) when (MiscOperationCancellation.IsCancellationRequested) { }
        catch (Exception ex) {
            Log.ReportException(ex);
        }
    }

    private async void btnMiscToolsExtractAudio_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        ofd.Title = "Select a file to extract the audio from";
        ofd.Filter = Formats.VideoFormats;
        ofd.FilterIndex = 0;
        if (ofd.ShowDialog() != DialogResult.OK) return;

        using SaveFileDialog sfd = new();
        sfd.Title = "Save audio as...";
        sfd.Filter = Formats.AudioFormats;
        sfd.FileName = Path.GetFileNameWithoutExtension(ofd.FileName);
        sfd.FilterIndex = 5;
        if (sfd.ShowDialog() != DialogResult.OK) return;

        try {
            int? ExitCode = await RunOwnedProcessAsync(new(Verification.FFmpegPath ?? "ffmpeg") {
                UseShellExecute = false,
                Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " " + ArgumentList.EscapeArgument(sfd.FileName),
            }, MiscOperationCancellation.Token);
            if (ExitCode is int Code && Code != 0) {
                Log.Write($"FFmpeg extract-audio operation exited with code {Code}.");
            }
        }
        catch (OperationCanceledException) when (MiscOperationCancellation.IsCancellationRequested) { }
        catch (Exception ex) {
            Log.ReportException(ex);
        }
    }

    private async void btnMiscToolsVideoToGif_Click(object sender, EventArgs e) {
        using OpenFileDialog ofd = new();
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string OutputDirectory = Path.GetDirectoryName(ofd.FileName) ?? Environment.CurrentDirectory;
        string FrameDirectory = Path.Combine(Path.GetTempPath(), "youtube-dl-gui", Path.GetRandomFileName());
        btnMiscToolsVideoToGif.Enabled = false;

        try {
            Directory.CreateDirectory(FrameDirectory);
            int? FfmpegExitCode = await RunOwnedProcessAsync(new(Verification.FFmpegPath ?? "ffmpeg") {
                UseShellExecute = false,
                WorkingDirectory = FrameDirectory,
                Arguments = "-i " + ArgumentList.EscapeArgument(ofd.FileName) + " -vf scale=320:-1:flags=lanczos,fps=10 outframes%03d.png",
            }, MiscOperationCancellation.Token);
            if (FfmpegExitCode is null) return;
            if (FfmpegExitCode.Value != 0) {
                Log.Write($"FFmpeg GIF frame extraction exited with code {FfmpegExitCode.Value}.");
                return;
            }

            string? ImageMagickPath = ResolveImageMagick(MiscOperationCancellation.Token);
            if (ImageMagickPath is null) {
                Log.MessageBox("ImageMagick could not be found. Install ImageMagick and ensure magick.exe is available in PATH.");
                return;
            }

            string GifPath = Path.Combine(OutputDirectory, Path.GetFileNameWithoutExtension(ofd.FileName) + ".gif");
            int? ImageMagickExitCode = await RunOwnedProcessAsync(new(ImageMagickPath) {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = FrameDirectory,
                Arguments = "-delay 10 -loop 0 outframes*.png " + ArgumentList.EscapeArgument(GifPath),
            }, MiscOperationCancellation.Token);
            if (ImageMagickExitCode is int Code && Code != 0) {
                Log.Write($"ImageMagick GIF conversion exited with code {Code}.");
            }
        }
        catch (OperationCanceledException) when (MiscOperationCancellation.IsCancellationRequested) { }
        catch (Exception ex) {
            Log.ReportException(ex);
        }
        finally {
            try {
                if (Directory.Exists(FrameDirectory)) Directory.Delete(FrameDirectory, true);
            }
            catch (Exception ex) {
                Log.Write($"Could not remove temporary GIF frames: {ex.Message}");
            }
            if (!this.IsDisposed && this.IsHandleCreated) {
                btnMiscToolsVideoToGif.Enabled = true;
            }
        }
    }
}
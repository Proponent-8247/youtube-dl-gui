using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static object TimedExtendedVideo(string source, int startSeconds, int endSeconds) {
        object media = New("youtube_dl_gui.ExtendedMediaDetails", source);
        ListViewItem video = new ListViewItem("video") { Tag = FormatWithId("v-audit") };
        Set(media.GetType(), media, "SelectedType", Enum.Parse(T("youtube_dl_gui.DownloadType"), "Video"));
        Set(media.GetType(), media, "SelectedVideoItem", video);
        Set(media.GetType(), media, "VideoDownloadAudio", false);
        if (startSeconds >= 0) Set(media.GetType(), media, "StartTime", Time(startSeconds, 0));
        if (endSeconds >= 0) Set(media.GetType(), media, "EndTime", Time(endSeconds, 0));
        return media;
    }

    private static byte[] TinyPng(int width, int height) {
        using (Bitmap bitmap = new Bitmap(width, height))
        using (MemoryStream output = new MemoryStream()) {
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
    }

    private static bool ImageIsDisposed(Image image) {
        try {
            using (MemoryStream output = new MemoryStream()) image.Save(output, ImageFormat.Png);
            return false;
        }
        catch (ArgumentException) { return true; }
        catch (ExternalException) { return true; }
    }

    private static void ForcedThumbnailReplacementDisposesOldImage() {
        object client = HttpClient();
        ((IDisposable)client).Dispose();
        using (LoopbackResponse firstServer = new LoopbackResponse(200, TinyPng(2, 3), null, false, "/first.png"))
        using (LoopbackResponse secondServer = new LoopbackResponse(200, TinyPng(4, 5), null, false, "/second.png")) {
            object media = New("youtube_dl_gui.ExtendedMediaDetails", "https://example.invalid/media");
            Set(media.GetType(), media, "MediaData", ThumbnailData(firstServer.Uri));
            Image first = (Image)Call(media.GetType(), media, "DownloadThumbnail", false);
            try {
                Equal(2, first.Width);
                Equal(3, first.Height);
                Set(media.GetType(), media, "MediaData", ThumbnailData(secondServer.Uri));
                Image second = (Image)Call(media.GetType(), media, "DownloadThumbnail", true);
                try {
                    Equal(4, second.Width);
                    Equal(5, second.Height);
                    Require(!ReferenceEquals(first, second), "Forced refresh reused the cached image");
                    Require(ImageIsDisposed(first), "Forced refresh dropped the previous cached image without disposing it");
                }
                finally { if (!ReferenceEquals(first, second)) second.Dispose(); }
            }
            finally { if (!ImageIsDisposed(first)) first.Dispose(); }
        }
    }

    private static void DownloadSectionsRequireYtDlp() {
        Type downloads = T("youtube_dl_gui.Downloads");
        int original = (int)Get(downloads, "YtdlType");
        try {
            Set(downloads, null, "YtdlType", 2); // YoutubeDl
            object youtubeDl = TimedExtendedVideo("https://example.invalid/youtube-dl", 5, 10);
            Require((bool)Call(youtubeDl.GetType(), youtubeDl, "GenerateArguments"), "youtube-dl argument generation failed");
            Require(((string)Get(youtubeDl, "Arguments")).IndexOf("--download-sections", StringComparison.Ordinal) < 0,
                "yt-dlp-only --download-sections was emitted for youtube-dl");

            Set(downloads, null, "YtdlType", 0); // YtDlp
            object ytDlp = TimedExtendedVideo("https://example.invalid/yt-dlp", 5, 10);
            Require((bool)Call(ytDlp.GetType(), ytDlp, "GenerateArguments"), "yt-dlp argument generation failed");
            Require(((string)Get(ytDlp, "Arguments")).Contains("--download-sections"), "yt-dlp time range was not emitted");
        }
        finally { Set(downloads, null, "YtdlType", original); }
    }

    private static void ReversedDownloadSectionsAreRejected() {
        Type downloads = T("youtube_dl_gui.Downloads");
        int original = (int)Get(downloads, "YtdlType");
        try {
            Set(downloads, null, "YtdlType", 0); // YtDlp
            object media = TimedExtendedVideo("https://example.invalid/reversed", 20, 10);
            Equal(false, Call(media.GetType(), media, "GenerateArguments"));
        }
        finally { Set(downloads, null, "YtdlType", original); }
    }

    private static void WmvProfileCheckIsCaseInsensitive() {
        object info = New("youtube_dl_gui.ConvertInfo", "input.mkv", "OUTPUT.WMV");
        Set(info.GetType(), info, "Type", Enum.Parse(T("youtube_dl_gui.ConversionType"), "Video"));
        Set(info.GetType(), info, "VideoUseProfile", true);
        Set(info.GetType(), info, "VideoProfile", 1);
        using (Form form = (Form)New("youtube_dl_gui.frmConverter", info)) {
            Call(form.GetType(), form, "BeginConversion");
            string arguments = ((TextBox)Field(form, "txtArgumentsGenerated")).Text;
            Thread worker = (Thread)Field(form, "ConverterThread");
            if (worker != null) PumpUntil(() => !worker.IsAlive, 5000, "Converter fixture did not exit");
            Require(arguments.IndexOf("-profile:v", StringComparison.Ordinal) < 0,
                "Uppercase .WMV output incorrectly received an incompatible video profile: " + arguments);
        }
    }

    private static void ExtendedTextBoxAlignmentMatchesEnum() {
        Type alignment = T("murrty.controls.ButtonAlignment");
        using (Control text = (Control)New("murrty.controls.ExtendedTextBox")) {
            text.Width = 220;
            Set(text.GetType(), text, "ButtonSize", new Size(24, 20));
            Set(text.GetType(), text, "ShowButton", true);
            IntPtr handle = text.Handle;
            Button button = (Button)Field(text, "InsetButton");
            Equal(Enum.Parse(alignment, "Right"), Get(text, "ButtonAlignment"));
            Require(button.Left > text.ClientSize.Width / 2, "Default Right alignment does not place the inset button on the right");

            Set(text.GetType(), text, "ButtonAlignment", Enum.Parse(alignment, "Left"));
            Require(button.Left <= 1, "Left alignment does not place the inset button on the left");

            Set(text.GetType(), text, "ButtonAlignment", Enum.Parse(alignment, "Right"));
            Require(button.Left > text.ClientSize.Width / 2, "Right alignment does not place the inset button on the right");
        }
    }

    static partial void RunRepairWave5Tests() {
        Test("CURRENT_O004.ForcedThumbnailReplacementDisposesOldImage", ForcedThumbnailReplacementDisposesOldImage);
        Test("CURRENT_O014.DownloadSectionsRequireYtDlp", DownloadSectionsRequireYtDlp);
        Test("CURRENT_O036.ReversedDownloadSectionsAreRejected", ReversedDownloadSectionsAreRejected);
        Test("F_O029.WmvProfileCheckIsCaseInsensitive", WmvProfileCheckIsCaseInsensitive);
        Test("F_O034.ExtendedTextBoxAlignmentMatchesEnum", ExtendedTextBoxAlignmentMatchesEnum);
    }
}

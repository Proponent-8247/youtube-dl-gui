#nullable enable
namespace murrty.controls;
public sealed class DownloadProgressChangedEventArgs : EventArgs {
    public long BytesReceived { get; }
    public long TotalBytesToReceive { get; }
    public float Percentage { get; }
    public long EstimateBytesPerSecond { get; }
    public DownloadProgressChangedEventArgs(long BytesReceived, long TotalBytesToReceive) {
        this.BytesReceived = BytesReceived;
        this.TotalBytesToReceive = TotalBytesToReceive;
        Percentage = TotalBytesToReceive > 0L ? System.Math.Min(100f, 100f * BytesReceived / TotalBytesToReceive) : 0f;
        //Percentage = (TotalBytesToReceive >= 0) ? ((TotalBytesToReceive == 0L) ? 100 : (100f * BytesReceived / TotalBytesToReceive)) : 0;
    }
    public DownloadProgressChangedEventArgs(long BytesReceived, long TotalBytesToReceive, long EstimateBytesPerSecond) {
        this.BytesReceived = BytesReceived;
        this.TotalBytesToReceive = TotalBytesToReceive;
        this.Percentage = TotalBytesToReceive > 0L ? System.Math.Min(100f, 100f * BytesReceived / TotalBytesToReceive) : 0f;
        //this.Percentage = (TotalBytesToReceive >= 0) ? ((TotalBytesToReceive == 0L) ? 100 : (100f * BytesReceived / TotalBytesToReceive)) : 0;
        this.EstimateBytesPerSecond = EstimateBytesPerSecond;
    }
}
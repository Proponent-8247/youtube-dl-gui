# WS-04 — Conversion, media metadata, ffprobe, thumbnails, merger

Primary scope:
- `youtube-dl-gui/Classes/ConvertHelper.cs`
- compiled `youtube-dl-gui/Classes/DataClasses/ConvertInfo.cs`
- `youtube-dl-gui/Classes/DataClasses/ExtendedConversionDetails.cs`
- `youtube-dl-gui/Classes/DataClasses/ExtendedMediaDetails.cs`
- `youtube-dl-gui/Classes/DataClasses/FfprobeData.cs`
- `youtube-dl-gui/Classes/DataClasses/YoutubeDlData.cs`
- `youtube-dl-gui/Forms/frmConverter*`
- `youtube-dl-gui/Forms/frmBatchConverter*`
- `youtube-dl-gui/Forms/frmMerger*`
- Release-reachable media/image paths

Audit external process invocation, probing/parsing, temporary files, replacement/rollback, image ownership/decoding, cancellation, malformed media metadata, output collision, format mapping, and cleanup.

## Findings


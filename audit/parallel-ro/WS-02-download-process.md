# WS-02 — Download arguments, provider selection, process execution

Primary scope:
- `youtube-dl-gui/Classes/ArgumentList.cs`
- `youtube-dl-gui/Classes/DownloadHelper.cs`
- compiled `youtube-dl-gui/Classes/DataClasses/DownloadInfo.cs`
- `youtube-dl-gui/Classes/Formats.cs`
- downloader/provider argument generation and subprocess/output parsing paths

Pay special attention to quoting/escaping, custom arguments, URLs/playlists/channels/libraries, retries, provider capability differences, ffmpeg forwarding, stdout/stderr parsing, cancellation, child-process ownership, and duplicate/overwrite behavior.

## Findings


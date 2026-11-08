# Supported command-line and protocol arguments

youtube-dl-gui 3.3.0 and later can accept commands from the command line, a second instance, or the `ytdlgui:` browser protocol. An alias may be used with no prefix or with **one** leading hyphen (for example `video` or `-video`); the command is followed by a media/source URL unless noted otherwise.

The following aliases reflect the active `youtube-dl-gui/Arguments.cs` parser.

## Video

### `-v` or `-video`
Open a video download without an authentication prompt.

### `-vau` or `-vauth` or `-videoauth`
Open a video download with an authentication prompt.

### `-archived` or `-ar` or `-var` or `-varchive` or `-videoarchive`
Open an archived-video download. Archive-source support depends on the installed downloader and the source (for example `ytarchive:`).

## Audio

### `-a` or `-audio`
Open an audio download without an authentication prompt.

### `-aau` or `-aauth` or `-audioauth`
Open an audio download with an authentication prompt.

### `-aar` or `-aarchive` or `-audioarchive`
Open an archived-audio download, without sound/video selection beyond the archive mode's support.

## Custom

### `-c` or `-custom`
Open the custom-download workflow.

### `-cau` or `-cauth` or `-customauth`
Open the custom-download workflow with an authentication prompt.

## Protocol maintenance

### `-installprotocol`
Register the `ytdlgui:` browser-protocol handler. This maintenance command takes **no media URL** and can require appropriate Windows registry permissions.

## Protocol support

Browser/userscript integrations may send supported commands through the `ytdlgui:` protocol. URL encoding and quoting matter when the browser invokes the handler. The application uses the same parser for command-line and protocol arguments. More information on Windows custom protocol handlers is available in [Microsoft's protocol documentation](https://learn.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/aa767914(v=vs.85)).

Aliases not listed above should not be assumed to exist; unsupported options are not silently converted to the closest supported download mode.

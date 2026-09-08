# Download History / Duplicate Prevention

This opt-in feature was developed on `audit-fixes` commit
`d1c7c91b712708a0df0fa019409acc07bc369bec`. It adds a library-wide yt-dlp
source-identity archive; it does not force a container, recode media, or replace
existing quality selections. Never-enabled installations retain ordinary
application behavior.

## Enable it

Back up the library and configuration before the first migration. Close downloader
and processing windows. Save the intended download folder and filename format in
Settings, reopen Settings, and choose **Downloads > Download History > Configure
Download History**. Select **Track previously downloaded media**, select library
scope or an absolute custom archive path, and choose **Validate**. Resolve the
report before pressing **Apply**. Apply performs another preflight.

`%(id)s` must occur unescaped in the media filename, not merely a directory.
The protected template must be relative to the library and end in `.%(ext)s`.
The dialog offers to insert IDs. The existing example is supported:

```text
%(uploader)s\%(title)s-%(id)s.%(ext)s
```

New protected downloads always save `.info.json` metadata. IDs alone do not
universally identify the website: `vimeo 12345` and `another-provider 12345` are
different identities. Recovery accepts canonical metadata, a validated checkpoint,
an explicit `[Extractor ID]` filename, or a unique ID match in trusted archive
records. A legacy YouTube-only library without metadata can use the explicit
**Legacy filename-only IDs ... are YouTube IDs** declaration. Do not select that
checkbox for a mixed-source library. Titles, hashes, durations and fuzzy matching
are never used to invent source identities.

The archive normally lives at `<library>\yt-dlp-archive.txt`, not in a dated batch,
channel, playlist, uploader or format subdirectory. Shared identities are skipped
regardless of title, container or which input discovered them. Playlists and
channels are still traversed to discover new entries. Standard, batch, Extended
and Extended batch process launches share the same protection boundary.

## Lifecycle and maintenance

**Disabled / never enabled:** no native archive arguments, scans or mandatory IDs.
Custom downloader behavior remains as before this feature.

**Enabled:** each media process first takes the library/archive lease, validates
its effective arguments, reconciles the physical library, and only then starts.
The lease spans the native process and postflight. IDs and available storage are
mandatory. Failed protected downloads remain eligible for retry, including cases
where post-processing left a final-looking file.

**Disabled after use:** Apply stops native archive use for subsequent jobs; it
does not delete or clear the archive, backup or recovery files. Status is dormant
or potentially stale. Keeping filename IDs is strongly recommended but is no
longer required. Disable and reset are separate operations.

**Re-enabled:** Apply reconciles again. Completed files downloaded in the disabled
interval must be identifiable. Unknown files block protection rather than silently
starting with an incomplete ledger.

**Validate** reports completed media candidates, embedded IDs, metadata recovery,
filename migrations, unresolved paths, excluded incomplete files, entry counts
and historical archive identities without current media. Cached counts and a
file's existence are not validation. Export saves the complete report to a
separate text file.

**Rebuild** explicitly reconciles and atomically replaces the ledger. Missing
primary archives use a valid backup, validated checkpoint and completed-library
identities. Malformed primary bytes are retained with a `.corrupt-` suffix.
Unavailable parents, permission failures and unsupported reparse points stop
operation; the application never silently changes storage locations. Empty new
libraries can initialize, but unidentified existing media cannot.

**Migrate filenames** previews metadata-backed ID-less media, asks before renaming,
checks all targets for collisions, and migrates associated metadata/subtitle
sidecars. It does not overwrite destinations. A journal supports rollback after
interruption. Currently all unresolved files must be resolved before this bulk
migration proceeds; partial libraries are reported, not silently called healthy.

**Recover interrupted run** is deliberate: stop all downloaders and postprocessors
first. A recorded live downloader still blocks recovery. **Undo interrupted
migration** rolls journaled names back; conflicting or missing names require
manual resolution.

**Reset history** requires protection to be disabled. It preserves old archive,
backup and checkpoint files under `.reset-` names. Media is never deleted, and
failed-file evidence is retained. Re-enabling can reconstruct history from the
library; reset is not a way to bypass validation or force automatic redownloads.

## Protected command policy

The GUI owns these options while protection is enabled:

```text
--ignore-config --no-break-on-existing --abort-on-unavailable-fragments
--write-info-json --download-archive "<configured archive>"
```

These options are inserted before the audit branch's `--` URL separator.
External yt-dlp configuration files are ignored only in protected mode. Missing
fragments abort instead of producing a trusted incomplete download. These
behavioral constraints are disclosed by the enable dialog.

Custom arguments are parsed, not searched by substring. Known download, format,
subtitle, authentication, rate-limit and connection options remain available.
Conflicting archives, custom output overrides, unknown/abbreviated options,
aliases, executable hooks, external config loaders, forced archive writes,
simulation, skip-download, ignore-errors, partial sections and independent
multiple-format outputs are rejected with a message. Disable protection for
operations whose completion semantics have not been audited, or extend the
allow-list with tests. The program does not silently pick between conflicting
native and custom archives.

A single identity intentionally spans audio, video, container and quality choices.
To retain an intentional second variant, use a separate library/history scope or
an explicitly unprotected operation. The feature does not erase existing duplicate
files. Historical archive entries remain even when their original media has been
moved or deleted; rebuild does not automatically authorize deleted media for retry.

## Files and operational limits

Archive siblings include `.bak`, `.state.json`, `.pending.json`, `.migration.json`
and `.lock`. Library-local `.ytdlg-history.lock`, `.ytdlg-history.pending.json` and
`.ytdlg-history.retry.json` coordinate jobs and retain failed-file evidence across
archive scope changes. Keep these with the library. Persistent lock files do not
mean a job is running; the open exclusive handle is the lock. Do not remove service
files while jobs are active.

Protection coordinates cooperating GUI instances/jobs. It is not a transaction
across arbitrary external file writers, unprotected CLI sessions, hostile
executables, storage failures or filesystem manipulation. Keep outside writers
stopped during validation/rebuild. The Windows process job handles ordinary
termination and descendants, but this is not a sandbox for untrusted downloaders.

Validation establishes identity reconciliation, not a cryptographic attestation
or full decode of every pre-existing media file. A legacy file with authoritative
identity but unknown historical completion still deserves manual review. The GUI
can reliably preserve its own observed failed-download evidence; it cannot infer
an external failed download's history from a plausible filename alone.

Reparse points/symlinks are rejected rather than silently skipped. Archive and
checkpoint reads are bounded at 128 MiB; individual metadata reads at 16 MiB.
Unrecognized file types are reported for review. Empty or known intermediate media
are excluded. Keep archives and saved metadata private: metadata may contain
source URLs and other personal information.

The application target remains .NET Framework 4.7.2 and existing project language
settings are retained. Protected operation requires a compatible yt-dlp executable;
legacy providers remain usable with protection disabled. Windows 7 hosts that
prevent nested process jobs can fail closed. Windows 7, physical disk removal,
SMB aliasing, unusual filesystems and high-DPI accessibility need manual validation
before broad deployment. See [verification and acceptance coverage](TESTING.md).

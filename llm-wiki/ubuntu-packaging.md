---
type: Release Packaging
title: Ubuntu Debian packaging
description: The supported Ubuntu amd64 installer and its runtime dependency contract.
tags: [ubuntu, linux, deb, packaging, ffmpeg, dotnet]
timestamp: 2026-10-25T00:00:00Z
---

# Ubuntu Debian packaging

`installer/ubuntu/Publish-UbuntuPackage.sh` creates an `amd64` `.deb` for
Ubuntu. It publishes `SubtitleSyncer.Sync.Console` as a self-contained
`linux-x64` single-file executable, so an end user does not need the .NET
runtime or Python.

The package installs the executable and converted ONNX model under
`/usr/lib/subtitle-syncer`, and provides `/usr/bin/subtitle-syncer` as a small
wrapper that supplies the installed model path. Its Debian control metadata
declares `ffmpeg` as a required dependency; FFmpeg is intentionally provided
by Ubuntu's package manager rather than copied into the installer. `zenity` is
also required for the graphical file chooser and notifications.

The Ubuntu publisher keeps the Linux sherpa-onnx native libraries external to
the single-file executable. It uses the sherpa-compatible ONNX Runtime native
ABI and retains `libsherpa-onnx-c-api.so` alongside it; embedding the different
ONNX Runtime native library from the managed GPU package causes VAD startup to
fail.

For Nautilus, the package installs an executable under the system `Scripts`
directory and its `postinst` copies it into the installing user's
`~/.local/share/nautilus/scripts` directory. It appears as **Scripts → Sync with
movie using Subtitle Syncer** when an `.srt` file is selected, asks for the
movie, and writes a separate `-synced.srt` result.
The Nautilus action preserves UTF-8, UTF-16LE, and UTF-16BE BOMs as well as
CRLF line endings when it writes the corrected subtitle.

Validate a built package without installing it system-wide by extracting it to
a temporary directory and running the packaged core against the Westworld
fixtures with `--device cpu`. The release test must exit zero and emit JSON.

The current publisher targets Ubuntu `amd64`, matching the supported local
machine used for validation. See [Ubuntu installer](../installer/ubuntu/README.md)
for build and installation commands.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Silero VAD integration](silero-vad.md)
- [Production readiness checklist](production-checklist.md)

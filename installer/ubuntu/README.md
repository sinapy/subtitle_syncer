# Ubuntu `.deb` installer

The Ubuntu package targets 64-bit Ubuntu (`amd64`) and installs a
self-contained `linux-x64` .NET release. End users do not need to install the
.NET runtime or Python. `ffmpeg` is a required Debian dependency, so `apt`
installs it as part of the package installation.

## End users

Install the package with:

```bash
sudo apt install ./subtitle-syncer_<version>_amd64.deb
```

Then run:

```bash
subtitle-syncer --video /path/to/movie.mkv --subtitles /path/to/subtitles.srt
```

For the file-manager action, right-click an `.srt` file and choose **Scripts →
Sync with movie using Subtitle Syncer**. The package copies the action into the
installing user's `~/.local/share/nautilus/scripts` directory. Nautilus may
need to be restarted after installation (`nautilus -q`) before the Scripts
entry appears. The action asks for the movie and creates a new `-synced.srt`
file; it never modifies the source subtitle.

If the package was installed as root without `sudo`, copy the action manually:

```bash
mkdir -p ~/.local/share/nautilus/scripts
cp /usr/share/nautilus/scripts/'Sync with movie using Subtitle Syncer' \
  ~/.local/share/nautilus/scripts/
chmod +x ~/.local/share/nautilus/scripts/'Sync with movie using Subtitle Syncer'
nautilus -q
```

The command-line wrapper supplies the installed ONNX model. Use `--device cpu`
on machines without a CUDA setup; the CLI prints the estimated correction as
JSON.

Remove the package with `sudo apt remove subtitle-syncer`.

## Release maintainers

From the repository root, with the validated converted model present:

```bash
./installer/ubuntu/Publish-UbuntuPackage.sh \
  --output-dir ./release \
  --version 1.0.0
```

The default model directory is
`artifacts/duration_alignment/model_60m_120s`. Select another model with
`--model-dir`. The publisher refuses to overwrite an existing package.

---
type: User Guide
title: Running the subtitle sync CLI
description: Supported source and Ubuntu package commands for estimating a subtitle offset.
tags: [cli, usage, subtitle-syncing, ubuntu, dotnet]
timestamp: 2026-10-25T00:00:00Z
---

# Running the subtitle sync CLI

The supported CLI estimates one constant subtitle correction from a movie and
an SRT file. It runs local Silero VAD through FFmpeg, scores candidate offsets
with the ONNX model, and prints the result as JSON. It does not modify or write
the subtitle file.

## Ubuntu package

After installing the amd64 package, run:

```bash
subtitle-syncer \
  --video /path/to/movie.mkv \
  --subtitles /path/to/subtitles.srt \
  --device auto
```

The package wrapper supplies the installed model, so `--model` is not needed.
Use `--device cpu` on a machine without a working CUDA setup. `auto` tries
CUDA first and falls back to CPU; `cuda` fails if CUDA is unavailable. See
[Ubuntu Debian packaging](ubuntu-packaging.md) for installation and package
creation details.

## Running from the repository

The source command requires .NET 10, FFmpeg, and both deployment files from a
converted model: `duration_offset_model.onnx` and its adjacent
`duration_offset_model.json` manifest.

```bash
dotnet run --project apps/SubtitleSyncer.Sync.Console -- \
  --video /path/to/movie.mkv \
  --subtitles /path/to/subtitles.srt \
  --model artifacts/duration_alignment/model_60m_120s/duration_offset_model.onnx \
  --device auto
```

The model can be converted from a trained `.pt` artifact with
`python3 python/duration_alignment/export_onnx.py`; see
[Duration-only alignment](duration-alignment.md).

## Options and output

Required options are:

- `--video <movie>` — an existing video or media file readable by FFmpeg.
- `--subtitles <file.srt>` — an existing SRT file with valid cue timestamps.
- `--model <model.onnx>` — the ONNX model; its same-directory `.json` manifest
  must also exist.

Optional options are:

- `--analysis-seconds <seconds>` — positive analysis duration; defaults to
  `3600` seconds.
- `--device auto|cpu|cuda` — inference device; defaults to `auto`.

The successful JSON response includes `offset_seconds`, `confidence`, the
analysed duration, activity and cue counts, and the selected device. Apply the
reported `offset_seconds` by adding it to every subtitle start and end time.
The CLI exits with status `0` on success and `2` for the input, file, model,
manifest, and ONNX errors handled by the console. It prints the error message
to stderr and does not produce a corrected SRT automatically.

## VAD activity export

For training or diagnostics, the separate VAD CLI exports speech intervals as
JSON:

```bash
dotnet run --project apps/SubtitleSyncer.Vad.Console -- \
  /path/to/movie.mkv 3600 \
  --export-activities artifacts/duration_alignment/activities.json
```

The analysis duration is optional and defaults to one hour. This export is an
input to the Python training workflow; the sync CLI performs VAD extraction
itself and does not consume the activity JSON.

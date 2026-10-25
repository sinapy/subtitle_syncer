---
type: Technical Design
title: Duration-only subtitle alignment
description: Local constant-offset estimation from subtitle timing and VAD activity, trained with synthetic timeline shifts.
tags: [subtitle-syncing, constant-offset, vad, machine-learning, duration]
timestamp: 2026-10-24T00:00:00Z
---

# Duration-only alignment

`python/duration_alignment` is the language-independent automatic
synchronization path. It consumes only chronological subtitle and VAD activity
intervals; it never receives subtitle text, audio samples, translations, or
transcriptions. It uses PyTorch; CUDA is selected when available, otherwise it
uses CPU. `SubtitleSyncer.Vad.Console` exports local VAD activity JSON for the
Python training workflow; inference does not use Python.

For an offset candidate, it shifts every cue and measures three features:

- proportion of subtitle display duration overlapping speech;
- duration agreement between each supported cue and its overlapping speech
  activity or activities; and
- fraction of cues with speech support.

The Python trainer uses a six-feature PyTorch MLP (32 then 16 hidden units,
dropout, AdamW, and validation-based early stopping). Each synthetic sample
shifts a bounded subtitle chunk by a known amount in `[-120, +120]` seconds.
The inverse shift is the positive label; near misses and broad wrong offsets
are negative examples. At inference it searches a bounded range coarsely and
refines the best peak at 10 ms resolution. Training produces a local `.pt`
file and a portable ONNX inference artifact. The ONNX graph accepts raw
six-feature `float32` vectors and performs the training-time normalization
inside the graph. Its adjacent `duration_offset_model.json` manifest records
the feature order, logit input/output names, calibration bias, and fixed
coarse-to-fine candidate search settings. A .NET host uses ONNX Runtime to
evaluate batches of candidates and applies sigmoid to the returned logits.

Changing VAD engines changes the timing-feature distribution. A model trained
from TEN VAD activity JSON must not be shipped with Silero VAD activities:
regenerate the reference activity exports, retrain, re-export ONNX, and pass
the held-out gates before release.

## Westworld bootstrap corpus

`tests/data/westworld-duration-training.json` declares ten reproducible
one-minute synthetic samples from the known-aligned Westworld S01E01 fixture.
It records each bounded timeline region and the manual subtitle shift applied
to it; it does not duplicate the subtitle file or movie. The VAD console can
export the local Silero VAD activity timeline as JSON for Python training:

```bash
mkdir -p artifacts/duration_alignment
dotnet run --project apps/SubtitleSyncer.Vad.Console -- \
  "tests/data/Westworld(2016)-S01E01-The_Original-[WEBDL-1080p][AC3 5.1][h264].mkv" 3600 \
  --export-activities artifacts/duration_alignment/westworld_60m_activities.json
python3 python/duration_alignment/train.py \
  --subtitles tests/data/Westworld.S01E01.720p.WEBRip.srt \
  --activities artifacts/duration_alignment/westworld_60m_activities.json \
  --chunk-seconds 120 --output artifacts/duration_alignment/model
python3 python/duration_alignment/predict.py \
  --subtitles tests/data/Westworld.S01E01.720p.WEBRip.srt \
  --activities artifacts/duration_alignment/westworld_60m_activities.json \
  --model artifacts/duration_alignment/model/duration_offset_model.pt
```

The trainer emits `duration_offset_model.onnx` and
`duration_offset_model.json` beside the existing `.pt` and `metrics.json`
files. Use `--no-onnx-export` only for Python-only experimentation; production
packaging should ship the ONNX file and manifest rather than a Python runtime.

## Convert an existing model without retraining

Conversion uses the existing `.pt` artifact's weights, feature normalization,
and calibration bias. It does not read media or run training. Install the
Python training requirements (including `onnx`) once, then run:

```bash
python3 python/duration_alignment/export_onnx.py \
  --model artifacts/duration_alignment/model_60m_120s/duration_offset_model.pt
```

This writes these two inseparable deployment files beside the `.pt` artifact:

- `duration_offset_model.onnx` — normalized model graph;
- `duration_offset_model.json` — feature order, calibration bias, and search settings.

If the native console reports that the ONNX model and adjacent manifest must
exist, convert the `.pt` file at the model location you passed to `--model`.
For a different trained model, substitute that model's `.pt` path in the
conversion command.

The native host needs both output files:

```bash
dotnet run --project apps/SubtitleSyncer.Sync.Console -- \
  --video /path/to/movie.mkv \
  --subtitles /path/to/subtitles.srt \
  --model artifacts/duration_alignment/model_60m_120s/duration_offset_model.onnx \
  --device auto
```

`--device auto` attempts CUDA then reliably falls back to CPU. `--device cpu`
forces CPU; `--device cuda` fails clearly if CUDA is unavailable.

## Linux bootstrap, retraining, and held-out check

From the repository root, install the Python training dependencies, export the
known-aligned S01E01 Silero activities, retrain, and write a fresh deployment
artifact:

```bash
python3 -m pip install -r python/duration_alignment/requirements.txt
mkdir -p artifacts/duration_alignment
dotnet run --project apps/SubtitleSyncer.Vad.Console -- \
  "tests/data/Westworld(2016)-S01E01-The_Original-[WEBDL-1080p][AC3 5.1][h264].mkv" 3600 \
  --export-activities artifacts/duration_alignment/westworld_60m_activities.json
python3 python/duration_alignment/train.py \
  --subtitles tests/data/Westworld.S01E01.720p.WEBRip.srt \
  --activities artifacts/duration_alignment/westworld_60m_activities.json \
  --chunk-seconds 120 --output artifacts/duration_alignment/model_60m_120s
python3 python/duration_alignment/export_onnx.py \
  --model artifacts/duration_alignment/model_60m_120s/duration_offset_model.pt
```

`train.py` already exports ONNX by default; the final command repeats the
conversion as an explicit deployment-artifact check. Inspect `metrics.json` and
do not promote the model if the trainer exits nonzero.

For the independent S01E05 regression, first generate its activity export:

```bash
dotnet run --project apps/SubtitleSyncer.Vad.Console -- \
  "tests/data/Westworld(2016)-S01E05-Contrapasso_ [Bluray-1080p][AC3 5.1][x265]-YAWNiX.mkv" 1800 \
  --export-activities artifacts/duration_alignment/westworld_s01e05_silero_1800s.json
```

On Linux, `SubtitleSyncer.Sync.Console` loads sherpa-onnx for VAD and ONNX
Runtime for duration scoring in the same process. Put sherpa-onnx's matching
native directory first on `LD_LIBRARY_PATH`; otherwise the console can load an
incompatible `libonnxruntime.so` and fail while creating the VAD detector.
The following command performs the held-out native check using the package
version referenced by `SubtitleSyncer.Vad.csproj`:

```bash
SHERPA_NATIVE_DIR="$HOME/.nuget/packages/org.k2fsa.sherpa.onnx.runtime.linux-x64/2.0.0/runtimes/linux-x64/native"
LD_LIBRARY_PATH="$SHERPA_NATIVE_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" \
dotnet run --project apps/SubtitleSyncer.Sync.Console -- \
  --video "tests/data/Westworld(2016)-S01E05-Contrapasso_ [Bluray-1080p][AC3 5.1][x265]-YAWNiX.mkv" \
  --subtitles tests/data/Westworld.S01E05.720p.HDTV.x264-KILLERS.srt \
  --model artifacts/duration_alignment/model_60m_120s/duration_offset_model.onnx \
  --analysis-seconds 1800 --device cpu
```

Keep S01E05 out of the training inputs. Its result is a regression check, not
an additional fitting signal.

## Test any movie/subtitle pair

After the bootstrap model has been trained and converted, supply the movie,
subtitle file, and ONNX model to the native runner. It performs local VAD
extraction and prints the constant correction in seconds. Add the printed
`offset_seconds` value to every subtitle timestamp.

```bash
dotnet run --project apps/SubtitleSyncer.Sync.Console -- \
  --video /path/to/movie.mkv \
  --subtitles /path/to/subtitles.srt \
  --model artifacts/duration_alignment/model_60m_120s/duration_offset_model.onnx \
  --device auto
```

It analyses the first hour by default; use `--analysis-seconds` to bound a
longer movie or speed up an experiment, and use a separately converted ONNX
artifact to choose another model.

The trainer partitions contiguous chunks chronologically into 60% train, 20%
validation, and 20% test. Augmented variants never cross those partitions. It
requires both held-out partitions to retain at least 95% of examples within
500 ms with a p95 error at most 500 ms; it separately requires full-timeline
synthetic offsets to be within 250 ms. This command does not provision or load
translation or Whisper models.

## Create synthetic training data from a new reference pair

The source video and subtitle file **must already be correctly aligned**. A
synthetic shift has a trustworthy label only when zero offset is known to be
correct. Do not train from a subtitle file whose offset you are trying to
discover; use an existing converted ONNX model with the native sync console
for that case.

Given a new known-good pair, run the same scripts used for Westworld, replacing
the paths and output directory:

```bash
mkdir -p artifacts/my-reference
dotnet run --project apps/SubtitleSyncer.Vad.Console -- \
  /path/to/aligned-movie.mkv 3600 \
  --export-activities artifacts/my-reference/activities.json

python3 python/duration_alignment/train.py \
  --subtitles /path/to/aligned-subtitles.srt \
  --activities artifacts/my-reference/activities.json \
  --chunk-seconds 120 \
  --output artifacts/my-reference/model
```

`train.py` generates the synthetic examples in memory from the source timing
data. Its fixed seed makes a run reproducible. For each usable 120-second
chunk it creates 16 random manual subtitle shifts uniformly over −120 to +120
seconds. For each shift, it creates:

- two positives within ±150 ms of the exact inverse shift;
- six hard negatives 0.4–12 seconds away; and
- four broad negative offsets drawn independently from the full search range.

This produces 192 candidate examples per source chunk. It discards chunks with
fewer than three subtitle cues or speech activities, then assigns *whole,
contiguous source chunks* to the 60/20/20 train/validation/test split before
any augmentation. Thus no timing interval, synthetic shift, or near-duplicate
of a validation/test example can occur in training. The saved `metrics.json`
contains the held-out results and the command exits nonzero if the quality gate
fails.

## Held-out S01E05 check

The S01E05 movie/subtitle pair was not used in the Westworld S01E01 bootstrap
training. A manual review identified a good correction in the −2.9 to −2.6
second range. With the historical TEN VAD activity timeline, the first 1,800
seconds produced −2.6514 seconds with 84.1% model confidence (543 activities
and 420 cues), inside that range.

As a migration check, the same pre-Silero model scored a new 1,800-second
Silero activity export at −2.5514 seconds with 76.4% confidence (297
activities and 420 cues). That is outside the reviewed range, so the old
model is not valid for Silero deployment. Regenerate the training activity
exports and retrain before treating S01E05 as a Silero regression check.
Keep S01E05 out of future training data if it is to remain independent.

## Limits

Duration timing alone is ambiguous for repeated short dialogue patterns,
credits, music, or a subtitle file with a timing drift rather than a constant
offset. The result must therefore preserve the existing rejection behavior:
low confidence is not an offset. More correctly aligned movies should add
separate manifests, with held-out movies used to measure false acceptance.

## Related

- [Running the subtitle sync CLI](running-cli.md)
- [Approach](approach.md)
- [Silero VAD integration](silero-vad.md)
- [Running tests](testing.md)

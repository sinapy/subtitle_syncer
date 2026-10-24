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
uses CPU. `SubtitleSyncer.Vad.Console` only exports local VAD activity JSON for
the Python workflow; it does not estimate an offset.

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
refines the best peak at 10 ms resolution. The artifact is a local `.pt` file
containing model weights, feature normalization, and a calibration bias.

## Westworld bootstrap corpus

`tests/data/westworld-duration-training.json` declares ten reproducible
one-minute synthetic samples from the known-aligned Westworld S01E01 fixture.
It records each bounded timeline region and the manual subtitle shift applied
to it; it does not duplicate the subtitle file or movie. The C# console can
export the local TEN VAD activity timeline as JSON for Python:

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

## Test any movie/subtitle pair

After the bootstrap model has been trained, supply just the two files to the
single-command runner. It performs temporary local VAD extraction and prints
the constant correction in seconds. Add the printed `offset_seconds` value to
every subtitle timestamp.

```bash
python3 python/duration_alignment/sync.py \
  --video /path/to/movie.mkv \
  --subtitles /path/to/subtitles.srt
```

It analyses the first hour by default; use `--analysis-seconds` to bound a
longer movie or speed up an experiment, and `--model` to choose another local
`.pt` artifact.

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
discover; use the existing trained model with `sync.py` for that case.

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
second range. Running `sync.py` on its first 1,800 seconds produced −2.6514
seconds with 84.1% model confidence (543 VAD activities and 420 cues), inside
that manually established range. Keep S01E05 out of future training data if it
is to remain an independent regression check.

## Limits

Duration timing alone is ambiguous for repeated short dialogue patterns,
credits, music, or a subtitle file with a timing drift rather than a constant
offset. The result must therefore preserve the existing rejection behavior:
low confidence is not an offset. More correctly aligned movies should add
separate manifests, with held-out movies used to measure false acceptance.

## Related

- [Approach](approach.md)
- [TEN VAD integration](ten-vad.md)
- [Running tests](testing.md)

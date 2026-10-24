---
type: Technical Design
title: Python duration-alignment architecture
description: Data flow and component boundaries for automatic constant-offset estimation.
tags: [architecture, python, vad, constant-offset]
timestamp: 2026-10-24T00:00:00Z
---

# Python duration-alignment architecture

The automatic pipeline has two inputs: chronological SRT cue intervals and
chronological VAD activity intervals. The Python layer is the only component
that joins the two timelines and estimates an offset.

```text
movie
  ↓
.NET VAD console (FFmpeg + TEN VAD)
  ↓ activity JSON: analysis_seconds, activities[start_seconds, end_seconds]
Python sync.py + SRT intervals + local PyTorch model
  ↓
offset_seconds and confidence
```

`sync.py` calls `apps/SubtitleSyncer.Vad.Console` with a movie path, an
analysis duration, and a temporary export path. The exporter writes the JSON
activity timeline. Python filters subtitle cues to the same duration, evaluates
candidate constant offsets, and reports a result without modifying the source
subtitle file.

The retained .NET boundary has no dependency on Python. `SubtitleSyncer.Audio`
holds the PCM timeline contract, `SubtitleSyncer.Vad` owns FFmpeg preparation
and TEN VAD, and `SubtitleSyncer.Vad.Console` serializes the activity result.

## Related

- [Approach](approach.md)
- [Duration-only alignment](duration-alignment.md)
- [TEN VAD integration](ten-vad.md)

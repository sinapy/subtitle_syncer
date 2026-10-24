---
type: Technical Design
title: Local duration-alignment architecture
description: Data flow and component boundaries for automatic constant-offset estimation.
tags: [architecture, python, dotnet, onnx, vad, constant-offset]
timestamp: 2026-10-24T00:00:00Z
---

# Local duration-alignment architecture

The automatic pipeline has two inputs: chronological SRT cue intervals and
chronological VAD activity intervals. The deployed .NET layer joins the two
timelines and estimates an offset. Python is retained for training and
exporting the model to ONNX.

```text
movie
  ↓
.NET VAD library (FFmpeg + TEN VAD)
  ↓ chronological activity intervals
.NET sync console + SRT intervals + local ONNX Runtime model
  ↓
offset_seconds and confidence
```

`apps/SubtitleSyncer.Sync.Console` calls the VAD library with a movie path and
an analysis duration. It filters subtitle cues to the same duration, evaluates
candidate constant offsets in batches through ONNX Runtime, and reports a
result without modifying the source subtitle file. `--device auto` attempts
CUDA and falls back to CPU; `--device cpu` never initializes CUDA.

`SubtitleSyncer.Audio` holds the PCM timeline contract, `SubtitleSyncer.Vad`
owns FFmpeg preparation and TEN VAD, and the sync console owns SRT parsing and
ONNX inference. The C# runtime has no dependency on Python.

## Related

- [Approach](approach.md)
- [Duration-only alignment](duration-alignment.md)
- [TEN VAD integration](ten-vad.md)

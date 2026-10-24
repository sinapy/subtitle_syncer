---
type: Architecture Decision
title: Python alignment with a minimal .NET VAD bridge
description: Use Python for duration alignment and retain .NET only for local VAD extraction.
tags: [architecture, python, dotnet, vad, on-device]
timestamp: 2026-10-24T00:00:00Z
---

# Python alignment with a minimal .NET VAD bridge

**Decision:** The synchronization model and orchestration are Python. The
repository retains a small .NET utility solely to prepare movie audio with
FFmpeg and export local TEN VAD activity intervals.

## Supported stack

| Need | Implementation |
| --- | --- |
| Subtitle timing parsing and offset search | Python `duration_alignment` scripts |
| Model training and inference | PyTorch, using CUDA when available and CPU otherwise |
| Media extraction and VAD | .NET `SubtitleSyncer.Vad` with FFmpeg and TEN VAD |
| Python/.NET boundary | JSON exported by `SubtitleSyncer.Vad.Console` |

The .NET projects must remain limited to `SubtitleSyncer.Audio`,
`SubtitleSyncer.Vad`, and `SubtitleSyncer.Vad.Console`; they do not parse
subtitles or perform translation, transcription, text matching, or offset
estimation.

## Related

- [Approach](approach.md)
- [Layered pipeline architecture](pipeline-architecture.md)
- [TEN VAD integration](ten-vad.md)

---
type: Architecture Decision
title: Python training with native .NET inference
description: Train in Python and deploy duration alignment in .NET through ONNX Runtime.
tags: [architecture, python, dotnet, onnx, vad, on-device]
timestamp: 2026-10-24T00:00:00Z
---

# Python training with native .NET inference

**Decision:** Python trains the synchronization model and emits a portable
ONNX inference artifact. The deployed synchronization host is the .NET
`SubtitleSyncer.Sync.Console` using ONNX Runtime; the VAD library prepares
movie audio with FFmpeg and obtains local Silero VAD activity intervals.

## Supported stack

| Need | Implementation |
| --- | --- |
| Subtitle timing parsing and offset search | .NET `SubtitleSyncer.Sync.Console`; Python retains equivalent trainer scripts |
| Model training | PyTorch, using CUDA when available and CPU otherwise |
| Deployed model inference | ONNX Runtime in .NET; CPU by default, CUDA execution provider optional |
| Media extraction and VAD | .NET `SubtitleSyncer.Vad` with FFmpeg and sherpa-onnx Silero VAD; the VAD console exports training activity JSON |
| Training/deployment boundary | ONNX model plus JSON inference manifest |

The ONNX model consumes only numerical timing features. No deployed component
needs subtitle text, transcription, translation, network access, or CUDA.

## Related

- [Approach](approach.md)
- [Layered pipeline architecture](pipeline-architecture.md)
- [Silero VAD integration](silero-vad.md)

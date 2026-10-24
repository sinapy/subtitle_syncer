---
type: Technology Decision
title: Silero VAD through sherpa-onnx
description: Use the bundled Silero ONNX model and sherpa-onnx for local cross-platform speech detection.
tags: [vad, silero, onnx, sherpa-onnx, cross-platform, audio]
timestamp: 2026-10-24T00:00:00Z
---

# Silero VAD through sherpa-onnx

**Decision:** `SileroVadEngine` is the baseline `IVadEngine` implementation.
It uses sherpa-onnx with the bundled `silero_vad.onnx` model, consumes mono
16 kHz signed-16-bit PCM, and returns padded chronological `VoiceActivity`
intervals on the original movie timeline.

## Packaging

`SubtitleSyncer.Vad` references `org.k2fsa.sherpa.onnx` and its Windows x64,
Linux x64/ARM64, and macOS x64/ARM64 runtime packages. The model is deployed
under `models/silero_vad.onnx`; synchronization is fully offline after
installation. The detector is CPU-only and does not require CUDA.

## Model and license

The bundled model is the official sherpa-onnx Silero VAD export. Its MIT
license is retained under `src/SubtitleSyncer.Vad/licenses/silero-vad`.
Thresholds and segmentation settings are configured in `SileroVadOptions` and
must be validated against held-out subtitle-alignment fixtures before release.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Duration-only alignment](duration-alignment.md)
- [Production readiness checklist](production-checklist.md)

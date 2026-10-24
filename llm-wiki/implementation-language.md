---
type: Architecture Decision
title: C#/.NET is the implementation language
description: Adopt C#/.NET for the Windows-first, on-device subtitle synchronization application.
tags: [architecture, csharp, dotnet, windows, on-device]
timestamp: 2026-10-23T00:00:00Z
---

# C#/.NET is the implementation language

**Decision:** Implement Subtitle Syncer in C# on .NET.

## Rationale

C# is a natural fit for the target of an ordinary Windows computer and has a
library route for every stage in the [approach](approach.md). It supports the
required CPU-only, on-device baseline while allowing optional acceleration on
machines that have it. See the C# column in [language library
coverage](language-library-landscape.md).

## Baseline stack

| Need | Candidate |
| --- | --- |
| Media extraction/resampling | FFmpeg.AutoGen, or a bundled FFmpeg executable |
| Voice activity detection | Microsoft.ML.OnnxRuntime with a locally stored Silero VAD model |
| SRT parsing and writing | SubtitlesParser |
| Persian normalization | Persian.Plus |
| Local Persian-to-English translation | LLamaSharp with a locally stored, CPU-capable multilingual GGUF model |
| Local English transcription | Whisper.net with a locally stored CPU-capable Whisper model |
| Fuzzy matching | FuzzySharp |
| Offset statistics | `TimeSpan`, LINQ, and MathNet.Numerics if needed |

The entries above are candidates, not a commitment to particular model files
or package versions. Any selected model and runtime must preserve the
[on-device-only](problem-statement.md#constraint-on-device-models-only) and
[ordinary-Windows-PC](problem-statement.md#constraint-ordinary-windows-computer)
constraints. Benchmark translation and transcription quality, startup time,
and memory use on representative media before finalizing them.

## Consequences

- New implementation work should target supported .NET on Windows rather than
  adding Python, Rust, or Go components.
- The initial release needs a reliable way to bundle or guide installation of
  native/model dependencies for an offline Windows user.
- GPU support remains an optional optimisation and must not replace the CPU
  inference path.

## Related

- [Problem statement](problem-statement.md)
- [Approach](approach.md)
- [Language library coverage](language-library-landscape.md)

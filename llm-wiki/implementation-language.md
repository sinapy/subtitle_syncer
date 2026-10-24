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
| Voice activity detection | TEN VAD through its native C ABI, using its local Linux/Windows runtime library |
| SRT parsing and writing | SubtitlesParser |
| Persian normalization | Persian.Plus |
| Local Persian-to-English translation | LLamaSharp with the local Qwen2.5 1.5B Instruct Q4_K_M GGUF baseline |
| Local English transcription | Whisper.net 1.9.1 plus `Whisper.net.Runtime`, with the local `ggml-small.en-q5_1.bin` Whisper GGML baseline |
| Fuzzy matching | FuzzySharp |
| Offset statistics | `TimeSpan`, LINQ, and MathNet.Numerics if needed |

The entries above are candidates, except that the translation baseline is the
official Qwen2.5 1.5B Instruct `Q4_K_M` GGUF model. Any selected model and
runtime must preserve the
[on-device-only](problem-statement.md#constraint-on-device-models-only) and
[ordinary-Windows-PC](problem-statement.md#constraint-ordinary-windows-computer)
constraints. Benchmark translation and transcription quality, startup time,
and memory use on representative media before replacing them.

## Consequences

- New implementation work should target supported .NET on Windows rather than
  adding Python, Rust, or Go components.
- The initial release needs a reliable way to bundle or guide installation of
  native/model dependencies for an offline Windows user.
- GPU support remains an optional optimisation and must not replace the CPU
  inference path.

The English-only `small.en-q5_1` model is a 181 MiB quantized baseline with a
useful quality/CPU tradeoff for short dialogue windows. It is available from
the [official whisper.cpp model repository](https://huggingface.co/ggerganov/whisper.cpp)
and should be stored under the ignored local `models/` directory rather than
committed to source control.

## Related

- [Problem statement](problem-statement.md)
- [Approach](approach.md)
- [Language library coverage](language-library-landscape.md)

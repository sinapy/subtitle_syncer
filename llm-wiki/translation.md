---
type: Technical Design
title: Local Persian-to-English translation
description: Independent CPU-only text-batch translation stack for Persian subtitle text.
tags: [translation, persian, english, on-device, cpu, windows]
timestamp: 2026-10-24T00:00:00Z
---

# Local Persian-to-English translation

`SubtitleSyncer.Translation` is a sibling stack of subtitle parsing. Its only
public translation boundary accepts `IReadOnlyList<string>` and returns an
equally sized `IReadOnlyList<string>` in the same order. It does not read SRT
files, know about cue timestamps, or reference `SubtitleSyncer.SubtitleParsing`.
That separation lets a future orchestration layer choose which parsed cue text
to submit without coupling either stack to the other.

## Runtime and model

The baseline uses `LLamaSharp` and `LLamaSharp.Backend.Cpu`. Their CPU backend
ships native assets for 64-bit Windows and Linux, so the same .NET code has a
CPU-only path on both platforms. Each translator instance loads one local GGUF
file, serializes its inference requests because the underlying executor is not
shared concurrently, and reuses its loaded weights for every list item.

The selected baseline model is the Apache-2.0
[`Qwen2.5-1.5B-Instruct-GGUF`](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF)
`qwen2.5-1.5b-instruct-q4_k_m.gguf` file. It is approximately 1.12 GB and
provides a practical Windows CPU baseline. A stronger local GGUF model can be
substituted by passing its path through `TranslationOptions` after quality
benchmarking.

Model files are provisioned separately and are intentionally not stored in the
repository. Download the selected GGUF once, then configure its absolute path:

```csharp
using var translator = new PersianEnglishTranslator(
    new TranslationOptions(@"C:\SubtitleSyncer\models\qwen2.5-1.5b-instruct-q4_k_m.gguf"));

IReadOnlyList<string> english = await translator.TranslateAsync(["سلام", "حالت چطوره؟"]);
```

`TranslateAsync` is the only translation API, so model inference never blocks
a caller's UI thread. It keeps empty strings in place without invoking the
model, preserves meaningful internal line breaks in the model result, and
rejects null list entries. The application must dispose the translator when it
is no longer needed so model memory and native resources are released.

After the GGUF has been placed on disk, inference does not use a network,
server, CUDA installation, or a dedicated GPU.

To include the real-runtime integration test in a local verification run, set
`TRANSLATION_MODEL_PATH` to that GGUF before running the translation test
project. Without it, the test project still runs deterministic contract tests
and does not require a model download.

## Quality and performance gate

The model is an instruction-following translator rather than a narrowly trained
machine-translation model. Before release, benchmark the chosen quantization on
representative Persian subtitle cues on an ordinary Windows CPU. Verify both
translation suitability for matching and cold/warm list latency; increase the
model size only when the quality gain justifies the CPU and memory cost.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Implementation language](implementation-language.md)
- [Problem statement](problem-statement.md)

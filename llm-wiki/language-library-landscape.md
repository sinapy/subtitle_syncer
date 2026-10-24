---
type: Technology Evaluation
title: Language library coverage for the synchronization pipeline
description: Library availability across Python, C#, Rust, and Go for every approach stage.
tags: [subtitle-syncing, libraries, python, csharp, rust, go]
timestamp: 2026-10-23T00:00:00Z
---

# Language library coverage

This is an availability survey for the ten stages in the
[approach](approach.md), current on 2026-10-23. It observes the project's
[on-device-only constraint](problem-statement.md#constraint-on-device-models-only):
no movie or subtitle data is sent to a hosted inference or translation
service. It also targets an [ordinary Windows
computer](problem-statement.md#constraint-ordinary-windows-computer), including
a CPU-only baseline. A language earns **one point per stage** when a maintained
library (including a standard-library facility) can materially implement that
stage. A point does not mean the library is a complete, drop-in implementation
of the stage; the filtering, candidate selection, match acceptance, tolerance,
and return policy remain application logic.

| # | Pipeline stage | Python | C# | Rust | Go |
| --- | --- | --- | --- | --- | --- |
| 1 | Extract/cache mono 16 kHz audio | [PyAV](https://pyav.basswood-io.com/) | [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen) | [ffmpeg-next](https://github.com/kcking/rust-ffmpeg-next) | [go-astiav](https://github.com/asticode/go-astiav) |
| 2 | Locate speech with VAD | [Silero VAD](https://github.com/snakers4/silero-vad) | [ONNX Runtime](https://github.com/microsoft/onnxruntime) with the Silero model | [fast-vad](https://docs.rs/fast-vad) or [webrtc-vad](https://crates.io/crates/webrtc-vad) | [go-webrtcvad](https://github.com/alex-savin/go-webrtcvad) |
| 3 | Parse and normalize Persian cues | [pysubs2](https://pysubs2.readthedocs.io/) + [Hazm](https://github.com/roshan-research/hazm) | [SubtitlesParser](https://github.com/AlexPoint/SubtitlesParser) + [Persian.Plus](https://www.nuget.org/packages/Persian.Plus) | [subparse](https://github.com/kaegi/subparse) + [yekdast](https://docs.rs/yekdast) | [go-astisub](https://github.com/asticode/go-astisub) + [persian](https://pkg.go.dev/github.com/amiranmanesh/persian) |
| 4 | Exclude likely non-dialogue cues | `re` / `regex` | `System.Text.RegularExpressions` | [`regex`](https://docs.rs/regex) | [`regexp`](https://pkg.go.dev/regexp) |
| 5 | Select early dialogue candidates | `itertools` / list slicing | LINQ | iterator adapters | slices / iterators |
| 6 | Translate to English and transcribe speech, locally | [Argos Translate](https://argos-translate.readthedocs.io/) + [faster-whisper](https://github.com/SYSTRAN/faster-whisper) | [LLamaSharp](https://github.com/SciSharp/LLamaSharp) + [Whisper.net](https://github.com/sandrohanea/whisper.net) | [llama-cpp-2](https://docs.rs/llama-cpp-2) + [whisper-rs](https://github.com/tazz4843/whisper-rs) | [go-llama.cpp](https://github.com/go-skynet/go-llama.cpp) + [go-whisper.cpp](https://pkg.go.dev/github.com/dyammarcano/go-whisper.cpp) |
| 7 | Chronologically fuzzy-match cue and transcript | [RapidFuzz](https://github.com/rapidfuzz/RapidFuzz) | [FuzzySharp](https://github.com/JakeBayer/FuzzySharp) | [`strsim`](https://docs.rs/strsim) | [`levenshtein`](https://pkg.go.dev/github.com/agnivade/levenshtein) |
| 8 | Calculate each timing offset | `datetime` / numeric arithmetic | `TimeSpan` / numeric arithmetic | `std::time` / numeric arithmetic | `time.Duration` / numeric arithmetic |
| 9 | Take median and test tolerance | [`statistics.median`](https://docs.python.org/3/library/statistics.html) | [MathNet.Numerics](https://numerics.mathdotnet.com/) | [`statrs`](https://docs.rs/statrs) | [`gonum/stat`](https://pkg.go.dev/gonum.org/v1/gonum/stat) |
| 10 | Return/apply a subtitle offset | [pysubs2](https://pysubs2.readthedocs.io/) | SubtitlesParser or `TimeSpan` | subparse or `Duration` | go-astisub or `time.Duration` |

## Score

| Language | Covered stages | Score |
| --- | ---: | ---: |
| Python | 10/10 | **10** |
| C# | 10/10 | **10** |
| Rust | 10/10 | **10** |
| Go | 10/10 | **10** |

## What the tie means

The score answers only the requested yes/no question: all four have a library
route for every stage. It should not be used as a language-quality ranking.
The project has selected [C#/.NET](implementation-language.md) for
implementation.

- **Python** has the most direct, established on-device composition for this
  task: `pysubs2`/Hazm/Argos Translate/faster-whisper/RapidFuzz, with PyAV or
  FFmpeg for media. Select CPU-supported model/runtime packages and a compact
  translation model for the baseline Windows path.
- **C#** is similarly viable, especially in a .NET/Windows application, but
  VAD is commonly an ONNX-model integration rather than a single dedicated
  package.
- **Rust** and **Go** have full coverage, but media and transcription packages
  more often expose native FFmpeg, C/C++, or CGO build dependencies. They fit
  well when deployment footprint, concurrency, or a native service matters,
  rather than because this ten-point availability score is higher.

## Verification notes

- `go-astiav` documents FFmpeg/libav bindings and audio resampling; it requires
  a compatible native FFmpeg installation. Its Go package page lists support
  for decoding and resampling examples.
- `subparse` supports SubRip input/output among several subtitle formats;
  `SubtitlesParser` supports SubRip; and `go-astisub` is the analogous Go
  subtitle package. Verify chosen packages against the exact subtitle formats
  accepted by the product.
- Stage 6 uses local model runtimes only. Argos Translate provides offline
  machine translation in Python. The C#, Rust, and Go options run a locally
  stored GGUF multilingual model through llama.cpp bindings; model quality for
  Persian-to-English must be benchmarked on representative subtitle cues. The
  CPU-only path must be tested on an ordinary Windows computer; GPU acceleration
  is an optional optimisation, not a prerequisite.
- Persian normalization is real library support in all four ecosystems: for
  example, Persian.Plus standardizes Arabic/Persian variants, and yekdast
  documents Persian character, digit, whitespace, and ZWNJ normalization.

## Related

- [Approach](approach.md)
- [Problem statement](problem-statement.md)

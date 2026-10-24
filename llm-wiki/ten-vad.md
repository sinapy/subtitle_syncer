---
type: Technology Decision
title: TEN VAD for local speech detection
description: Adopt the TEN VAD native runtime for the bounded movie-audio VAD stack.
tags: [vad, ten-vad, native-runtime, audio]
timestamp: 2026-10-24T00:00:00Z
---

# TEN VAD for local speech detection

**Decision:** Use TEN VAD as the baseline implementation of `IVadEngine`.

TEN VAD consumes mono 16 kHz signed-16-bit PCM in 160- or 256-sample frames.
The baseline uses 256 samples (16 ms) and converts its per-frame speech flag
into padded, chronological `VoiceActivity` intervals. This fits the VAD
stack's FFmpeg-prepared audio without an additional resampling step.

## Packaging

`SubtitleSyncer.Vad` binds the official C ABI through `TenVadEngine` and ships
the downloaded x64 native binaries under `runtimes/`:

- Windows: `ten_vad.dll`.
- Linux: `libten_vad.so`, plus its LLVM C++ runtime dependencies bundled in
  the same directory so an end user does not need to install them system-wide.

The runtime libraries and their notices are included in the project output.
The managed code remains portable; adding a new operating-system runtime is a
packaging task, not a change to the VAD contract.

## Licensing

The downloaded TEN VAD distribution's license and notices are preserved under
`src/SubtitleSyncer.Vad/licenses/ten-vad`. Its license is Apache-2.0 with
additional deployment conditions, including a restriction on deploying the
library to compete with Agora's offerings. Review those conditions before any
public distribution. Licenses for the bundled LLVM runtime components are under
`src/SubtitleSyncer.Vad/licenses/llvm`.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Implementation language](implementation-language.md)
- [Approach](approach.md)

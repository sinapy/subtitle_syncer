---
type: Technical Approach
title: Duration-only subtitle synchronization
description: Estimate a constant subtitle offset from subtitle and voice-activity timing.
tags: [subtitle-syncing, constant-offset, vad, python]
timestamp: 2026-10-24T00:00:00Z
---

# Approach

The supported synchronization path is duration-only alignment. It consumes
chronological subtitle intervals and voice-activity intervals, searches for a
single constant correction, and never uses subtitle text, translation, or
speech transcription.

```text
movie → .NET VAD exporter → activity JSON ─┐
                                           ├→ Python duration alignment → constant offset
subtitle file → Python SRT parser ─────────┘
```

`python/duration_alignment/sync.py` is the composition root. It invokes the
local .NET VAD exporter, loads the trained PyTorch scorer, and prints an
accepted correction and confidence. Applying that correction to a subtitle
file remains an explicit, separate operation.

The scorer is trained from synthetic shifts of a known-correct movie/subtitle
pair. It rejects low-confidence results because duration timing is ambiguous
for repeated dialogue, credits, music, and timing drift. Details are in
[duration-only alignment](duration-alignment.md).

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [TEN VAD integration](ten-vad.md)
- [Software principles](software-principles.md)

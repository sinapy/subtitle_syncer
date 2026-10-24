---
type: Technical Approach
title: Layered constant-offset synchronization
description: Independently build audio and subtitle pipelines, then join them to infer a constant offset.
tags: [subtitle-syncing, constant-offset, vad, transcription, architecture]
timestamp: 2026-10-23T00:00:00Z
---

# Approach

Build the system as two independent pipelines that meet in one synchronization
layer. Each layer receives typed models, retains chronology and timestamps, and
can be tested with local fixtures before the layer above it exists. The full
contracts and build order are in [layered pipeline architecture](pipeline-architecture.md).

```text
movie → VAD (including bounded audio preparation) → English transcription ─┐
                                                           ├→ match → offsets → median → result
subtitle file → parse → opted-in cue transforms → translate ┘
```

## Audio pipeline

1. The VAD stack extracts or reuses cached, mono 16 kHz audio from only the
   first configured `X` minutes of the movie, then runs on-device VAD. Audio
   preparation belongs to this stack; it is not an independent pipeline stage.
2. VAD returns chronological voice-activity windows together with the prepared
   audio. Orchestration selects each activity's audio region.
3. The neighboring transcription stack's `EnglishTranscription` class
   receives audio only and transcribes it locally as English speech segments,
   retaining each
   segment's source timing and transcription confidence.

## Subtitle pipeline

1. Parse the subtitle file into `IEnumerable<SubtitleCue>` models. A cue keeps
   its ordinal, start/end times, and original text.
2. Apply only explicitly requested mechanical cue exclusions, such as excluding
   cues with HTML tags, URLs, or empty text. Do not use heuristic filtering
   for translator credits, title cards, or sound labels.
3. Translate the selected cues from the declared source language to a target
   language with an on-device model. The translated cue retains the original
   cue timing and order.

## Synchronization pipeline

1. Consume the translated cues and English speech segments in chronological
   order. Match distinctive text pairs with a confidence score while preserving
   their ordering.
2. Take the first three reliable matches and calculate one measurement per
   match: `speechStart - subtitleStart`.
3. Accept the result only when the measurements agree within the configured
   tolerance. Return their median as the offset to apply to the original
   subtitle file; otherwise return an insufficient-confidence result rather
   than an arbitrary offset.

The rationale for keeping transformations narrow and allowing the matcher to
evaluate cue usefulness is recorded in the [software principles](software-principles.md).

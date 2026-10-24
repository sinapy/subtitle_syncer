---
type: Technical Design
title: Layered C# pipeline architecture
description: Contracts, data flow, and delivery order for the local subtitle synchronization pipeline.
tags: [architecture, csharp, pipeline, subtitles, vad, transcription]
timestamp: 2026-10-23T00:00:00Z
---

# Layered C# pipeline architecture

This design implements the [approach](approach.md) in layers. It uses C#/.NET,
works entirely on-device, and keeps the CPU path usable on an ordinary Windows
computer. The public subtitle boundary intentionally returns
`IEnumerable<SubtitleCue>` as requested; implementations may process items
lazily, but should materialize a small bounded sequence when a later model
needs repeated enumeration.

## Project boundaries

Each pipeline stack owns a C# project rather than accumulating in a shared
`Core` project. The first stack is `SubtitleSyncer.SubtitleParsing`; it owns
the SRT reader contract, `SubtitleCue`, and SRT-specific implementation. Its
tests live in `SubtitleSyncer.SubtitleParsing.Tests` and use xUnit.net v3.
Shared test fixtures live under `tests/data`; subtitle fixtures there are
versioned even though production subtitle files are ignored.
Future audio, VAD,
transcription, cue-transformation, translation, matching, and orchestration stacks
should follow the same one-stack/one-project rule, using explicit project
references only for models and contracts they consume.

## Dependency shape

```text
Audio preparation
        ↓
Voice activity detection → English transcription ───────────┐
                                                            │
Subtitle parsing → opted-in cue transforms → translation ───┤
                                                            ↓
                                            chronological matcher
                                                            ↓
                                         offset estimator/result
```

The matcher is the only component that knows about both branches. Nothing in
the parsing, cue-transformation, translation, VAD, or transcription layers may
calculate an offset.

## Domain models

All models are immutable records. Their timestamps refer to the original movie
timeline, never to a trimmed VAD window.

```csharp
public sealed record SubtitleCue(
    int Index, TimeSpan Start, TimeSpan End, string Text);

public sealed record DialogueCue(
    SubtitleCue Source, string NormalizedText);

public sealed record TranslatedCue(
    DialogueCue Source, string Text, string SourceLanguage, string TargetLanguage);

public sealed record VoiceActivity(TimeSpan Start, TimeSpan End);

public sealed record SpeechSegment(
    TimeSpan Start, TimeSpan End, string Text, float Confidence);

public sealed record CueSpeechMatch(
    TranslatedCue Cue, SpeechSegment Speech, double Score);

public sealed record OffsetMeasurement(CueSpeechMatch Match, TimeSpan Offset);

public sealed record SynchronizationResult(
    bool IsAccepted, TimeSpan? Offset, IReadOnlyList<OffsetMeasurement> Measurements,
    string? RejectionReason);
```

`DialogueCue`, `TranslatedCue`, and `SpeechSegment` deliberately retain their
source models so that order, timing, original subtitle text, and diagnostic
information are never lost.

## Layer contracts

### Audio and VAD branch

Audio extraction is a thin infrastructure adapter. It supplies the VAD layer
with bounded, mono 16 kHz audio from the first `analysisDuration`; it is not
part of matching logic.

```csharp
public interface IAudioExtractor
{
    PcmAudio Extract(string moviePath, TimeSpan analysisDuration);
}

public interface IVoiceActivityDetector
{
    IEnumerable<VoiceActivity> Detect(PcmAudio audio);
}

public interface IEnglishTranscriber
{
    IEnumerable<SpeechSegment> Transcribe(PcmAudio audio,
        IEnumerable<VoiceActivity> activities);
}
```

`PcmAudio` is an internal model that records sample rate, channel count, start
offset, and PCM samples. The baseline uses local FFmpeg for extraction,
ONNX/Silero for VAD, and local Whisper.net for transcription. VAD output must
be sorted, non-overlapping, and bounded by `analysisDuration`; the transcriber
must preserve the absolute timeline positions from each activity.

### Subtitle branch

```csharp
public interface ISubtitleReader
{
    IEnumerable<SubtitleCue> Read(string subtitlePath);
}

public interface ITextTranslator
{
    IEnumerable<TranslatedCue> Translate(IEnumerable<DialogueCue> cues,
        string sourceLanguage, string targetLanguage);
}
```

The baseline SRT reader preserves the original cue text and supports UTF-8
(with or without BOM), UTF-16 BOMs, and Windows-1256 fallback for legacy
Persian subtitle files. It must not silently decode invalid UTF-8 as UTF-8:
the Windows-1256 fallback keeps Persian characters usable in older files.

The current `SubtitleSyncer.SubtitleParsing.Client` project is the
application-facing composition boundary: `SubtitleParsingClient.Read()` calls
the SRT reader and returns its `IEnumerable<SubtitleCue>` directly. Callers
may explicitly chain only the transformations they need, such as
`ExcludeHtmlTag()`, `ExcludeUrls()`, and `ExcludeEmpty()`. The HTML extension
excludes cues containing an HTML tag; the URL and empty extensions also exclude
matching cues. The project deliberately contains no heuristic classification
of translator credits, title cards, or sound effects because those labels are
too language- and context-dependent to safely discard.

`ITextTranslator` is language-generic. For this product it is invoked with
Persian (`fa`) to English (`en`) and must use a locally stored model through
the chosen C# runtime. It receives only accepted dialogue cues and returns
them in the same chronological order.

### Join and synchronization branch

```csharp
public interface IChronologicalMatcher
{
    IEnumerable<CueSpeechMatch> Match(IEnumerable<TranslatedCue> cues,
        IEnumerable<SpeechSegment> speech);
}

public interface IOffsetEstimator
{
    SynchronizationResult Estimate(IEnumerable<CueSpeechMatch> matches,
        TimeSpan tolerance, int requiredMatchCount = 3);
}
```

The matcher compares normalized translated text with transcription text using
fuzzy scoring. It emits only high-confidence, one-to-one, chronological
matches. The estimator selects the first three reliable matches, computes
`speech.Start - cue.Source.Source.Start`, and returns the median only when all
three measurements fall within the configured tolerance. An unsuccessful
estimate is a normal result, not an exception.

## Orchestration

`SynchronizationService` is the sole composition root for the pipeline:

1. Extract bounded audio, detect activities, and transcribe them.
2. Read the subtitle file, apply explicitly requested cue transforms, and
   translate the resulting cues.
3. Pass both chronological sequences to the matcher, then pass matches to the
   estimator.
4. Return `SynchronizationResult`; applying an accepted offset to a subtitle
   file belongs to a separate explicit writer operation.

This makes it possible to unit-test every layer without FFmpeg or a model, use
recorded fixtures for integration tests, and test the orchestration with fake
implementations.

## Delivery order

1. Define the domain models and subtitle-reader contract; parse a fixture and
   expose `IEnumerable<SubtitleCue>`.
2. Implement and test explicitly requested, mechanical cue transforms while
   preserving source timing and order.
3. Add the audio extractor and VAD with a configurable `analysisDuration`.
4. Add local English transcription over VAD windows.
5. Add local generic text translation and test Persian-to-English with fixed
   fixtures.
6. Implement the matcher, offset estimator, acceptance rule, and diagnostics.
7. Compose the layers in `SynchronizationService`, then add an explicit
   subtitle-offset writer and end-to-end fixtures.

## Related

- [Approach](approach.md)
- [Implementation language](implementation-language.md)
- [Software principles](software-principles.md)
- [Problem statement](problem-statement.md)

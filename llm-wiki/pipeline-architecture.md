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
`Core` project. `SubtitleSyncer.Audio` is a deliberately small contract
project for timeline-positioned PCM shared by the neighboring audio stacks.
The first feature stack is `SubtitleSyncer.SubtitleParsing`; it owns
the SRT reader contract, `SubtitleCue`, and SRT-specific implementation. Its
tests live in `SubtitleSyncer.SubtitleParsing.Tests` and use xUnit.net v3.
Shared test fixtures live under `tests/data`; subtitle fixtures there are
versioned even though production subtitle files are ignored.
Future VAD, transcription, cue-transformation, translation, matching, and orchestration stacks
should follow the same one-stack/one-project rule, using explicit project
references only for models and contracts they consume.

## Dependency shape

```text
VAD (including audio preparation) ───────────┐
                                             ↓
Subtitle parsing ─────────────────→ duration alignment → offset/result
```

The Python duration-alignment workflow is the automatic composition layer. It
is the only component that knows about both timelines, and nothing in parsing
or VAD may calculate an offset. Its only inputs are cue and voice-activity
timing; it intentionally has no reference to translation or transcription. The
older translation/transcription matcher remains an independent diagnostic path.

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

### VAD and transcription stacks

There are two neighboring stacks in the audio branch:

- `SubtitleSyncer.Vad` owns both audio preparation and voice-activity
  detection. Its implementation extracts bounded, mono 16 kHz PCM from the
  movie before evaluating it with VAD. FFmpeg extraction is an implementation
  detail of this stack, not a separate `AudioPreparation` stack or public
  pipeline stage.
- `SubtitleSyncer.Transcription` provides `EnglishTranscription`. Its contract
  accepts audio and returns transcription; it has no project reference to VAD
  and does not re-extract the movie or make VAD decisions.

VAD returns its activity windows and the bounded PCM audio used to find them.
The orchestrator uses those windows to select the corresponding audio regions
and calls transcription for each region. `PcmAudio` is an audio contract, not
a VAD type: it carries its sample rate, channel count, start offset, and PCM
samples. The audio is bounded to `analysisDuration`; every activity and
transcription timestamp nevertheless refers to the original movie timeline.

```csharp
public interface IVoiceActivityDetector
{
    VoiceActivityAnalysis Analyze(string moviePath, TimeSpan analysisDuration);
}

public sealed record VoiceActivityAnalysis(
    PcmAudio Audio, IReadOnlyList<VoiceActivity> Activities);

public interface IEnglishTranscriber
{
    IEnumerable<SpeechSegment> Transcribe(PcmAudio audio);
}
```

The baseline uses local FFmpeg and TEN VAD within VAD, and local Whisper.net
with its CPU runtime in `EnglishTranscription`. The transcription project
references only `SubtitleSyncer.Audio`, converts its mono 16 kHz PCM to an
in-memory WAV stream for Whisper, and translates each relative Whisper result
back to the audio's absolute movie position. Whisper may report a final segment
rounded to an internal processing block beyond a short input clip; the adapter
clamps that final end time to the supplied PCM boundary. TEN VAD is called through a small
native C ABI adapter and consumes mono 16 kHz PCM in 160- or 256-sample frames.
VAD output must be sorted,
non-overlapping, and bounded by `analysisDuration`; `EnglishTranscription`
must preserve the absolute timeline positions from each activity.

When orchestrating transcription over a VAD interval, the caller slices the
prepared audio with two seconds of available audio on each side, clamped to the
prepared-audio boundary, before passing it to `EnglishTranscription`. This
gives Whisper enough context for short utterances while all returned segment
timestamps remain absolute.

### Subtitle branch

```csharp
public interface ISubtitleReader
{
    IEnumerable<SubtitleCue> Read(string subtitlePath);
}

public interface ITextTranslator
{
    Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
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

`ITextTranslator` is an independent sibling of subtitle parsing. For this
product `SubtitleSyncer.Translation` translates Persian to English with a
locally stored GGUF model through the CPU LLamaSharp runtime. It receives text
items rather than subtitle cues, returns the same number of items in the same
order, and has no project reference to subtitle parsing. A later orchestration
layer is responsible for associating translations with cues.

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

`python/duration_alignment/sync.py` is the automatic composition root:

1. Ask VAD to prepare bounded audio and detect activities.
2. Read the subtitle file and retain its chronological cue intervals.
3. Load the locally trained timing scorer and search the supplied cue/activity
   timelines for a single constant offset.
4. Print the correction and confidence; applying an accepted offset to a
   subtitle file remains a separate explicit writer operation.

This makes it possible to unit-test every layer without FFmpeg or a model, use
recorded fixtures for integration tests, and test the orchestration with fake
implementations.

Integration tests that require ignored local movies or model files remain in
their owning test project and run in every test pass. The required local
fixtures and commands are documented in [running tests](testing.md).

## Delivery order

1. Define the domain models and subtitle-reader contract; parse a fixture and
   expose `IEnumerable<SubtitleCue>`.
2. Implement and test explicitly requested, mechanical cue transforms while
   preserving source timing and order.
3. Add the VAD stack, including movie-audio preparation, with a configurable
   `analysisDuration` (five minutes by default). The VAD stack owns FFmpeg
   extraction of a mono 16 kHz prefix; model inference remains behind
   `IVadEngine` so the TEN VAD adapter can be changed or tested independently.
4. Implement Python duration-only offset alignment over subtitle and VAD
   intervals, with synthetic-shift training, an acceptance rule, and diagnostics.
5. Compose that path in an application entry point, then add an explicit
   subtitle-offset writer and end-to-end fixtures.
6. Keep transcription and translation as optional diagnostic tooling rather
   than dependencies of automatic synchronization.

## Related

- [Approach](approach.md)
- [Implementation language](implementation-language.md)
- [Local Persian-to-English translation](translation.md)
- [Software principles](software-principles.md)
- [Problem statement](problem-statement.md)

---
type: Technical Design
title: Subtitle-to-speech matching
description: Ordered fuzzy matching composition layer for translated subtitles and English transcription.
tags: [matching, subtitles, transcription, fuzzy-text]
timestamp: 2026-10-24T00:00:00Z
---

# Subtitle-to-speech matching

`SubtitleSyncer.Matching` is the first application-level composition stack. It
depends on subtitle parsing client, translation, VAD, and transcription. Its
`SubtitleSpeechMatcher.MatchAsync()` parses a subtitle file, explicitly applies
`ExcludeHtmlTag()`, `ExcludeUrls()`, and `ExcludeEmpty()`, then translates the
remaining cue texts in original-timeline order. In parallel pipeline order it asks VAD for bounded movie
audio, transcribes every detected activity with up to two seconds of available
audio context on each side, and preserves the transcription's original movie
timeline.

The matcher returns `SubtitleSpeechMatch` records containing the source cue,
its English translation, the matched `SpeechSegment`, and a normalized fuzzy
text confidence from zero through one. It uses case- and punctuation-insensitive
Levenshtein similarity and an order-preserving, one-to-one dynamic-programming
alignment. Only pairs at or above `MatchingOptions.MinimumConfidence` (0.70 by
default) are returned. This is intentionally a first text-matching baseline;
if subtitle translation and transcription vocabulary prove too divergent,
replace or supplement the similarity scorer with cosine similarity without
changing the composition boundary or match result.

Each component owns its own integration coverage. VAD, transcription, and
translation tests execute their local runtimes independently. Matching tests
mock recorded pipeline responses instead: the stable subtitle cues and VAD
windows near 54 and 63 seconds, their padded transcription segments, and the
corresponding translation results. This keeps matching tests deterministic and
makes a failure attributable to matching rather than FFmpeg or a model runtime.
The current captured 54- and 63-second fixture deliberately yields no lexical
match: Whisper recognizes “Last notice please.” and “Sean.” while the local
translation model produces unrelated text (including one refusal). It is a
repeatable evidence fixture for evaluating a semantic similarity replacement.

`SubtitleSyncer.Matching.Client` is the application-facing wrapper around the
matcher. Its `MatchingClient.MatchAsync()` delegates matching to
`SubtitleSpeechMatcher`. `GetMedianOffset()` is an extension over returned
matches that calculates `Speech.Start - Cue.Start` for each match, sorts those
offsets, and returns their median (the average of the two middle offsets for
an even count). It rejects an empty match sequence because no offset can be
inferred. `TryGetMedianOffset()` is the synchronization-safe alternative: it
requires at least three offsets within a two-second tolerance before returning
a value.

The matcher only considers cues overlapping the bounded audio-analysis window.
Its default five-character minimum normalized text length prevents short names
and interjections from becoming timing evidence; callers can adjust that
threshold through `MatchingOptions`.

`apps/SubtitleSyncer.Matching.Console` demonstrates the full local pipeline. By
default it matches the movie and Persian SRT under `tests/data` using the local
Whisper and translation models under `models`. Run it from the repository root
with `dotnet run --project apps/SubtitleSyncer.Matching.Console`; optional
arguments override the movie path, subtitle path, and analysis duration in
seconds. `TRANSCRIPTION_MODEL_PATH` and `TRANSLATION_MODEL_PATH` override model
locations.

The console keeps its output readable by configuring the translation runtime to
emit only native errors. It passes an `IProgress<MatchingProgress>` callback to
the client, which reports model-independent stages: audio analysis,
transcription, subtitle parsing, translation, alignment, and offset estimation.
Set `MATCHING_DEBUG=1` to additionally print every detected activity window,
transcribed segment, selected source cue, translation, and accepted alignment.

`apps/SubtitleSyncer.Review.Web` is the manual alternative when lexical
matching is not trustworthy. It presents the original Persian cues and their
English translations beside the timestamped English transcription at
`http://127.0.0.1:5088`. A reviewer selects exactly one cue and one speech
segment, confirms the pair, and receives `speech.Start - cue.Start` as the
offset. The review path performs no automatic text matching and never rewrites
the subtitle file. Run it locally from the repository root with
`dotnet run --project apps/SubtitleSyncer.Review.Web`; it prepopulates the
available Shaun of the Dead fixture paths and accepts other local paths.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Software principles](software-principles.md)
- [Local Persian-to-English translation](translation.md)

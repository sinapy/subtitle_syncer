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

Offset estimation and offset-application APIs do not belong in this project
yet. They will consume the returned matches in a separate client layer.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Software principles](software-principles.md)
- [Local Persian-to-English translation](translation.md)

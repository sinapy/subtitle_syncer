using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.Transcription;

namespace SubtitleSyncer.Matching;

/// <summary>A chronological subtitle-to-speech match and its text-similarity confidence.</summary>
public sealed record SubtitleSpeechMatch(
    SubtitleCue Cue,
    string Translation,
    SpeechSegment Speech,
    double Confidence);

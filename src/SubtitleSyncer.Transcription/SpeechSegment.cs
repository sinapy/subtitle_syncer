namespace SubtitleSyncer.Transcription;

/// <summary>A recognized English utterance positioned on the original movie timeline.</summary>
public sealed record SpeechSegment(TimeSpan Start, TimeSpan End, string Text, float Confidence)
{
    public TimeSpan Duration => End - Start;
}

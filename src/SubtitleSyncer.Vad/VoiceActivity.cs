namespace SubtitleSyncer.Vad;

/// <summary>A detected speech interval on the original movie timeline.</summary>
public sealed record VoiceActivity(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;
}

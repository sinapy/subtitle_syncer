namespace SubtitleSyncer.Vad;

/// <summary>Segmentation policy layered over TEN VAD's per-frame decisions.</summary>
public sealed record TenVadOptions
{
    /// <summary>TEN supports 160 (10 ms) and 256 (16 ms) samples at 16 kHz.</summary>
    public int HopSize { get; init; } = 256;
    public float SpeechThreshold { get; init; } = 0.5f;
    public TimeSpan MinimumSpeechDuration { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan MinimumSilenceDuration { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan SpeechPadding { get; init; } = TimeSpan.FromMilliseconds(30);

    internal void Validate()
    {
        if (HopSize is not (160 or 256))
        {
            throw new ArgumentOutOfRangeException(nameof(HopSize), "TEN VAD supports 160- or 256-sample hops at 16 kHz.");
        }

        if (SpeechThreshold is < 0 or > 1 || MinimumSpeechDuration < TimeSpan.Zero ||
            MinimumSilenceDuration < TimeSpan.Zero || SpeechPadding < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(SpeechThreshold));
        }
    }
}

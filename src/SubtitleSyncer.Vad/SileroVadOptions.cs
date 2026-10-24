namespace SubtitleSyncer.Vad;

/// <summary>Configuration for the bundled CPU-only Silero VAD ONNX model.</summary>
public sealed record SileroVadOptions
{
    /// <summary>Overrides the deployed model path; null uses the bundled model.</summary>
    public string? ModelPath { get; init; }
    public float SpeechThreshold { get; init; } = 0.5f;
    public TimeSpan MinimumSpeechDuration { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan MinimumSilenceDuration { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan MaximumSpeechDuration { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan SpeechPadding { get; init; } = TimeSpan.FromMilliseconds(30);
    public int InferenceThreads { get; init; } = 1;

    internal void Validate()
    {
        if (SpeechThreshold is <= 0.01f or >= 1f || MinimumSpeechDuration <= TimeSpan.Zero ||
            MinimumSilenceDuration <= TimeSpan.Zero || MaximumSpeechDuration <= TimeSpan.Zero ||
            SpeechPadding < TimeSpan.Zero || InferenceThreads <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SpeechThreshold));
        }
    }
}

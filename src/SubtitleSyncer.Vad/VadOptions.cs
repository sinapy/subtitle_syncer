namespace SubtitleSyncer.Vad;

public sealed record VadOptions
{
    public static readonly TimeSpan DefaultAnalysisDuration = TimeSpan.FromMinutes(5);

    public TimeSpan AnalysisDuration { get; init; } = DefaultAnalysisDuration;

    internal TimeSpan GetAnalysisDuration(TimeSpan? requestedDuration)
    {
        var duration = requestedDuration ?? AnalysisDuration;
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDuration), "Analysis duration must be positive.");
        }

        return duration;
    }
}

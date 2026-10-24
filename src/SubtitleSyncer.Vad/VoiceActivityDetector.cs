namespace SubtitleSyncer.Vad;

using SubtitleSyncer.Audio;

/// <summary>Coordinates VAD-owned audio preparation with a local VAD engine.</summary>
public sealed class VoiceActivityDetector(IAudioPreparation audioPreparation, IVadEngine engine, VadOptions? options = null)
    : IVoiceActivityDetector
{
    private readonly IAudioPreparation audioPreparation = audioPreparation ?? throw new ArgumentNullException(nameof(audioPreparation));
    private readonly IVadEngine engine = engine ?? throw new ArgumentNullException(nameof(engine));
    private readonly VadOptions options = options ?? new VadOptions();

    public VoiceActivityAnalysis Analyze(string moviePath, TimeSpan? analysisDuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moviePath);
        var duration = options.GetAnalysisDuration(analysisDuration);
        var audio = audioPreparation.Prepare(moviePath, duration, cancellationToken);
        var activities = engine.Detect(audio, cancellationToken);

        ValidateActivities(activities, audio);
        return new VoiceActivityAnalysis(audio, activities.ToArray());
    }

    private static void ValidateActivities(IReadOnlyList<VoiceActivity> activities, PcmAudio audio)
    {
        ArgumentNullException.ThrowIfNull(activities);
        var previousEnd = audio.Start;

        foreach (var activity in activities)
        {
            if (activity is null || activity.Start < audio.Start || activity.End > audio.End ||
                activity.End <= activity.Start || activity.Start < previousEnd)
            {
                throw new InvalidOperationException("VAD must return sorted, non-overlapping activities inside the prepared audio.");
            }

            previousEnd = activity.End;
        }
    }
}

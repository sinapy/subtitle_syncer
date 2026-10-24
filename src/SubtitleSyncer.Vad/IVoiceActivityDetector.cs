namespace SubtitleSyncer.Vad;

public interface IVoiceActivityDetector
{
    VoiceActivityAnalysis Analyze(string moviePath, TimeSpan? analysisDuration = null,
        CancellationToken cancellationToken = default);
}

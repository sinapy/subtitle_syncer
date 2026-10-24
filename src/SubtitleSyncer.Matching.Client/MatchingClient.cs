using SubtitleSyncer.Matching;

namespace SubtitleSyncer.Matching.Client;

/// <summary>Application-facing entry point for subtitle-to-speech matching.</summary>
public sealed class MatchingClient
{
    private readonly SubtitleSpeechMatcher matcher;

    public MatchingClient(SubtitleSpeechMatcher matcher)
    {
        this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>Finds chronological fuzzy text matches between a movie and its subtitle file.</summary>
    public Task<IReadOnlyList<SubtitleSpeechMatch>> MatchAsync(
        string moviePath,
        string subtitlePath,
        TimeSpan? analysisDuration = null,
        CancellationToken cancellationToken = default) =>
        matcher.MatchAsync(moviePath, subtitlePath, analysisDuration, cancellationToken);
}

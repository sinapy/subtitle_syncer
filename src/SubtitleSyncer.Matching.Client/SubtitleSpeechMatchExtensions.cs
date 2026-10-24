using SubtitleSyncer.Matching;

namespace SubtitleSyncer.Matching.Client;

/// <summary>Calculations over subtitle-to-speech matches.</summary>
public static class SubtitleSpeechMatchExtensions
{
    /// <summary>
    /// Gets the median timing offset, calculated as each speech start minus its source cue start.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="matches"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="matches"/> contains no matches.</exception>
    public static TimeSpan GetMedianOffset(this IEnumerable<SubtitleSpeechMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var offsets = matches
            .Select(match => match.Speech.Start - match.Cue.Start)
            .Order()
            .ToArray();
        if (offsets.Length == 0)
        {
            throw new InvalidOperationException("At least one subtitle-to-speech match is required to calculate an offset.");
        }

        var upper = offsets.Length / 2;
        if (offsets.Length % 2 == 1)
        {
            return offsets[upper];
        }

        return Average(offsets[upper - 1], offsets[upper]);
    }

    private static TimeSpan Average(TimeSpan first, TimeSpan second)
    {
        var firstTicks = first.Ticks;
        var secondTicks = second.Ticks;
        return TimeSpan.FromTicks(
            (firstTicks / 2) + (secondTicks / 2) + ((firstTicks % 2 + secondTicks % 2) / 2));
    }
}

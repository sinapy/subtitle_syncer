namespace SubtitleSyncer.Matching;

/// <summary>Controls text-match acceptance.</summary>
public sealed record MatchingOptions
{
    /// <summary>Minimum normalized fuzzy-text score required to emit a match.</summary>
    public double MinimumConfidence { get; init; } = 0.70;

    internal void Validate()
    {
        if (MinimumConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
        }

    }
}

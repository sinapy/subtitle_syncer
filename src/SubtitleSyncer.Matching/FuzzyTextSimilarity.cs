using System.Text.RegularExpressions;

namespace SubtitleSyncer.Matching;

/// <summary>Word-order-aware text similarity normalized to the inclusive range zero through one.</summary>
public static partial class FuzzyTextSimilarity
{
    public static double Score(string left, string right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        if (normalizedLeft == normalizedRight)
        {
            return 1;
        }

        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        return 1 - (double)distance / Math.Max(normalizedLeft.Length, normalizedRight.Length);
    }

    private static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var withoutApostrophes = Apostrophe().Replace(text.ToLowerInvariant(), string.Empty);
        return Whitespace().Replace(NonWord().Replace(withoutApostrophes, " "), " ").Trim();
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    [GeneratedRegex("[^\\p{L}\\p{N}]+")]
    private static partial Regex NonWord();

    [GeneratedRegex("['’]")]
    private static partial Regex Apostrophe();

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();
}

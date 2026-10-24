using System.Text.RegularExpressions;
using SubtitleSyncer.SubtitleParsing;

namespace SubtitleSyncer.SubtitleParsing.Client;

/// <summary>Optional, composable exclusions for parsed subtitle cues.</summary>
public static partial class SubtitleCueSanitizationExtensions
{
    /// <summary>Excludes cues containing an HTML tag.</summary>
    public static IEnumerable<SubtitleCue> ExcludeHtmlTag(this IEnumerable<SubtitleCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        return cues.Where(cue => !HtmlTag().IsMatch(RequireText(cue)));
    }

    /// <summary>Excludes cues containing a URL or email address.</summary>
    public static IEnumerable<SubtitleCue> ExcludeUrls(this IEnumerable<SubtitleCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        return cues.Where(cue => !Url().IsMatch(RequireText(cue)));
    }

    /// <summary>Excludes cues whose text is empty or consists only of whitespace.</summary>
    public static IEnumerable<SubtitleCue> ExcludeEmpty(this IEnumerable<SubtitleCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        return cues.Where(cue => !string.IsNullOrWhiteSpace(RequireText(cue)));
    }

    private static string RequireText(SubtitleCue? cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        ArgumentNullException.ThrowIfNull(cue.Text);
        return cue.Text;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"(?:https?://|www\.|\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b)", RegexOptions.IgnoreCase)]
    private static partial Regex Url();
}

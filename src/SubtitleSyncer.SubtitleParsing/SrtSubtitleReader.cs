using System.Text;
using System.Text.RegularExpressions;

namespace SubtitleSyncer.SubtitleParsing;

/// <summary>Reads SubRip (.srt) files without changing their text or timeline.</summary>
public sealed partial class SrtSubtitleReader : ISubtitleReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public IEnumerable<SubtitleCue> Read(string subtitlePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subtitlePath);
        return Parse(Decode(File.ReadAllBytes(subtitlePath)));
    }

    /// <summary>Parses SRT text that has already been decoded.</summary>
    public IEnumerable<SubtitleCue> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var cues = new List<SubtitleCue>();
        var blocks = BlockSeparator().Split(content.Replace("\r\n", "\n").Replace('\r', '\n'));

        foreach (var rawBlock in blocks)
        {
            if (string.IsNullOrWhiteSpace(rawBlock))
            {
                continue;
            }

            var lines = rawBlock.Split('\n');
            if (lines.Length < 3 || !int.TryParse(lines[0].Trim(), out var index))
            {
                throw new FormatException("An SRT cue must begin with a numeric index and a timing line.");
            }

            var timing = TimingLine().Match(lines[1].Trim());
            if (!timing.Success)
            {
                throw new FormatException($"Cue {index} has an invalid timing line.");
            }

            var start = ParseTimestamp(timing.Groups[1].Value);
            var end = ParseTimestamp(timing.Groups[2].Value);
            if (end < start)
            {
                throw new FormatException($"Cue {index} ends before it starts.");
            }

            cues.Add(new SubtitleCue(index, start, end, string.Join("\n", lines.Skip(2))));
        }

        return cues;
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Persian SRT files distributed for older Windows players commonly use CP1256.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1256).GetString(bytes);
        }
    }

    private static TimeSpan ParseTimestamp(string value)
    {
        var parts = value.Replace('.', ',').Split([':', ',']);
        return new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]),
            int.Parse(parts[3]));
    }

    [GeneratedRegex(@"\n[\t ]*\n+")]
    private static partial Regex BlockSeparator();

    [GeneratedRegex(@"^(\d{2,}:\d{2}:\d{2}[,.]\d{3})\s+-->\s+(\d{2,}:\d{2}:\d{2}[,.]\d{3})(?:\s+.*)?$")]
    private static partial Regex TimingLine();
}

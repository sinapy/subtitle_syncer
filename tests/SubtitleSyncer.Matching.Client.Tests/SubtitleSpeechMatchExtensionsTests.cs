using SubtitleSyncer.Matching;
using SubtitleSyncer.Matching.Client;
using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.Transcription;

namespace SubtitleSyncer.Matching.Client.Tests;

public sealed class SubtitleSpeechMatchExtensionsTests
{
    [Fact]
    public void GetMedianOffset_returns_the_middle_offset_regardless_of_match_order()
    {
        var result = new[]
        {
            Match(cueStart: 20, speechStart: 15),
            Match(cueStart: 10, speechStart: 17),
            Match(cueStart: 30, speechStart: 33)
        }.GetMedianOffset();

        Assert.Equal(TimeSpan.FromSeconds(3), result);
    }

    [Fact]
    public void GetMedianOffset_averages_the_two_middle_offsets_for_an_even_count()
    {
        var result = new[]
        {
            Match(cueStart: 10, speechStart: 16),
            Match(cueStart: 20, speechStart: 22),
            Match(cueStart: 30, speechStart: 34),
            Match(cueStart: 40, speechStart: 48)
        }.GetMedianOffset();

        Assert.Equal(TimeSpan.FromSeconds(5), result);
    }

    [Fact]
    public void GetMedianOffset_requires_at_least_one_match()
    {
        Assert.Throws<InvalidOperationException>(() => Array.Empty<SubtitleSpeechMatch>().GetMedianOffset());
    }

    private static SubtitleSpeechMatch Match(double cueStart, double speechStart) => new(
        new SubtitleCue(1, TimeSpan.FromSeconds(cueStart), TimeSpan.FromSeconds(cueStart + 1), "cue"),
        "translation",
        new SpeechSegment(TimeSpan.FromSeconds(speechStart), TimeSpan.FromSeconds(speechStart + 1), "speech", 1),
        1);
}

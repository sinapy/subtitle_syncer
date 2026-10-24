using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.SubtitleParsing.Client;

namespace SubtitleSyncer.SubtitleParsing.Client.Tests;

public sealed class SubtitleCueSanitizationExtensionsTests
{
    [Fact]
    public void ExcludeHtmlTag_removes_only_cues_containing_html_tags()
    {
        var html = new SubtitleCue(5, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6),
            "<i>Hello</i> there");
        var dialogue = new SubtitleCue(6, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8), "Hello there");

        var result = new[] { html, dialogue }.ExcludeHtmlTag();

        Assert.Same(dialogue, Assert.Single(result));
    }

    [Theory]
    [InlineData("Visit https://example.test")]
    [InlineData("www.example.test")]
    [InlineData("contact@example.test")]
    public void ExcludeUrls_removes_only_cues_with_urls_or_email_addresses(string text)
    {
        var cue = new SubtitleCue(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), text);

        var result = new[] { cue }.ExcludeUrls();

        Assert.Empty(result);
    }

    [Fact]
    public void ExcludeUrls_keeps_non_url_text()
    {
        var cue = new SubtitleCue(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Translated by Example");

        var result = new[] { cue }.ExcludeUrls();

        Assert.Same(cue, Assert.Single(result));
    }

    [Fact]
    public void ExcludeEmpty_removes_only_empty_or_whitespace_cues()
    {
        var empty = new SubtitleCue(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), "  \t");
        var dialogue = new SubtitleCue(2, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "حال شما چطور است؟");

        var result = new[] { empty, dialogue }.ExcludeEmpty();

        Assert.Same(dialogue, Assert.Single(result));
    }

    [Fact]
    public void Extensions_can_be_chained_after_client_reads_the_configured_parser()
    {
        var html = new SubtitleCue(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), "<b>Hello</b>");
        var url = new SubtitleCue(2, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "https://example.test");
        var empty = new SubtitleCue(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "");
        var dialogue = new SubtitleCue(4, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "Hello");
        var reader = new StubReader(html, url, empty, dialogue);

        var result = new SubtitleParsingClient(reader).Read("movie.srt")
            .ExcludeHtmlTag()
            .ExcludeUrls()
            .ExcludeEmpty();

        Assert.Equal("movie.srt", reader.ReadPath);
        var cue = Assert.Single(result);
        Assert.Same(dialogue, cue);
    }

    private sealed class StubReader(params SubtitleCue[] cues) : ISubtitleReader
    {
        public string? ReadPath { get; private set; }

        public IEnumerable<SubtitleCue> Read(string subtitlePath)
        {
            ReadPath = subtitlePath;
            return cues;
        }
    }
}

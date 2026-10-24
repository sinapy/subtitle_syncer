using SubtitleSyncer.SubtitleParsing;

namespace SubtitleSyncer.SubtitleParsing.Tests;

public sealed class SrtSubtitleReaderTests
{
    [Fact]
    public void Read_reads_Persian_fixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "Shaun_Of_The_Dead.srt");
        var cues = new SrtSubtitleReader().Read(path).ToArray();

        Assert.Equal(1228, cues.Length);
        Assert.Equal(1, cues[0].Index);
        Assert.Equal(TimeSpan.FromMilliseconds(1499), cues[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(30485), cues[0].End);
        Assert.Equal("تنظيم شده توسط : صابر رشيدي\nEmail : Black_dragon743@yahoo.com", cues[0].Text);
        Assert.Equal("- شان.\n- هان !", cues[2].Text);
    }

    [Fact]
    public void Parse_preserves_multiline_UTF8_cue_text()
    {
        const string srt = "7\n01:02:03,004 --> 01:02:05.006\nسلام\nHello\n\n";

        var cue = new SrtSubtitleReader().Parse(srt).Single();

        Assert.Equal(7, cue.Index);
        Assert.Equal(
            TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3) + TimeSpan.FromMilliseconds(4),
            cue.Start);
        Assert.Equal("سلام\nHello", cue.Text);
    }

    [Fact]
    public void Parse_rejects_invalid_cue_timing()
    {
        Assert.Throws<FormatException>(() =>
            new SrtSubtitleReader().Parse("1\nnot a timestamp\ntext").ToArray());
    }
}

using SubtitleSyncer.Audio;
using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Vad.Tests;

public sealed class TenVadEngineTests
{
    [Fact]
    public void Detect_returns_no_activity_for_silence()
    {
        var audio = new PcmAudio(16_000, 1, TimeSpan.Zero, new short[16_000]);

        var activities = new TenVadEngine().Detect(audio, TestContext.Current.CancellationToken);

        Assert.Empty(activities);
    }

    [Fact]
    public void Detect_rejects_audio_that_is_not_mono_16_khz()
    {
        var audio = new PcmAudio(8_000, 1, TimeSpan.Zero, new short[8_000]);

        Assert.Throws<ArgumentException>(() => new TenVadEngine().Detect(audio, TestContext.Current.CancellationToken));
    }
}

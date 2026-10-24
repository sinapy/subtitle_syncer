using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Vad.Tests;

public sealed class VoiceActivityDetectorTests
{
    [Fact]
    public void Analyze_uses_five_minutes_by_default_and_preserves_audio_timeline()
    {
        var audio = new PcmAudio(16_000, 1, TimeSpan.Zero, new short[16_000 * 2]);
        var preparation = new RecordingPreparation(audio);
        var detector = new VoiceActivityDetector(preparation,
            new RecordingEngine([new VoiceActivity(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1))]));

        var result = detector.Analyze("movie.mkv", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(5), preparation.RequestedDuration);
        Assert.Same(audio, result.Audio);
        Assert.Equal(new VoiceActivity(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1)), Assert.Single(result.Activities));
    }

    [Fact]
    public void Analyze_rejects_overlapping_engine_output()
    {
        var audio = new PcmAudio(16_000, 1, TimeSpan.Zero, new short[16_000 * 2]);
        var detector = new VoiceActivityDetector(new RecordingPreparation(audio),
            new RecordingEngine([
                new VoiceActivity(TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                new VoiceActivity(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5))
            ]));

        Assert.Throws<InvalidOperationException>(() => detector.Analyze("movie.mkv", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Slice_retains_the_absolute_movie_timeline()
    {
        var audio = new PcmAudio(10, 1, TimeSpan.FromSeconds(12), Enumerable.Range(0, 30).Select(value => (short)value).ToArray());

        var slice = audio.Slice(TimeSpan.FromSeconds(13), TimeSpan.FromSeconds(14.5));

        Assert.Equal(TimeSpan.FromSeconds(13), slice.Start);
        Assert.Equal(TimeSpan.FromSeconds(1.5), slice.Duration);
        Assert.Equal([10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24], slice.Samples.ToArray());
    }

    private sealed class RecordingPreparation(PcmAudio audio) : IAudioPreparation
    {
        public TimeSpan RequestedDuration { get; private set; }
        public PcmAudio Prepare(string moviePath, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            RequestedDuration = duration;
            return audio;
        }
    }

    private sealed class RecordingEngine(IReadOnlyList<VoiceActivity> activities) : IVadEngine
    {
        public IReadOnlyList<VoiceActivity> Detect(PcmAudio audio, CancellationToken cancellationToken = default) => activities;
    }
}

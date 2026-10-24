using SubtitleSyncer.Audio;
using SubtitleSyncer.Transcription;

namespace SubtitleSyncer.Transcription.Tests;

public sealed class EnglishTranscriptionTests
{
    [Fact]
    public void Transcribe_maps_relative_results_to_the_audio_movie_timeline()
    {
        var audio = new PcmAudio(16_000, 1, TimeSpan.FromSeconds(42), new short[16_000 * 4]);
        using var engine = new RecordingEngine([
            new WhisperSegment(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), " Hello ", 0.8f),
            new WhisperSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "there", 0.6f)
        ]);
        using var transcription = new EnglishTranscription(engine);

        var result = transcription.Transcribe(audio, TestContext.Current.CancellationToken).ToArray();

        Assert.Equal([
            new SpeechSegment(TimeSpan.FromSeconds(42.25), TimeSpan.FromSeconds(43), "Hello", 0.8f),
            new SpeechSegment(TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(45), "there", 0.6f)
        ], result);
        Assert.Same(audio, engine.Audio);
    }

    [Fact]
    public void Transcribe_skips_blank_recognition_but_preserves_later_timeline_positions()
    {
        var audio = new PcmAudio(16_000, 1, TimeSpan.FromSeconds(10), new short[16_000 * 3]);
        using var transcription = new EnglishTranscription(new RecordingEngine([
            new WhisperSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), " "),
            new WhisperSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "ready")
        ]));

        var result = Assert.Single(transcription.Transcribe(audio, TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(12), result.Start);
        Assert.Equal(TimeSpan.FromSeconds(13), result.End);
        Assert.Equal("ready", result.Text);
    }

    [Fact]
    public void Transcribe_clamps_a_result_that_ends_after_the_supplied_audio()
    {
        var audio = new PcmAudio(16_000, 1, TimeSpan.Zero, new short[16_000]);
        using var transcription = new EnglishTranscription(new RecordingEngine([
            new WhisperSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "too long")
        ]));

        var result = Assert.Single(transcription.Transcribe(audio, TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.Zero, result.Start);
        Assert.Equal(TimeSpan.FromSeconds(1), result.End);
    }

    private sealed class RecordingEngine(IReadOnlyList<WhisperSegment> segments) : IWhisperEngine
    {
        public PcmAudio? Audio { get; private set; }

        public IEnumerable<WhisperSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default)
        {
            Audio = audio;
            cancellationToken.ThrowIfCancellationRequested();
            return segments;
        }

        public void Dispose() { }
    }
}

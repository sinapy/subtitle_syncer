using SubtitleSyncer.Audio;
using SubtitleSyncer.Matching;
using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.SubtitleParsing.Client;
using SubtitleSyncer.Transcription;
using SubtitleSyncer.Translation;
using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Matching.Tests;

public sealed class SubtitleSpeechMatcherTests
{
    [Fact]
    public async Task MatchAsync_filters_cues_translates_and_returns_chronological_one_to_one_matches()
    {
        var dialogueOne = new SubtitleCue(4, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(21), "اول");
        var dialogueTwo = new SubtitleCue(5, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(26), "دوم");
        var parser = new SubtitleParsingClient(new StubReader(
            new SubtitleCue(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), "<i>Ignore</i>"),
            new SubtitleCue(2, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "https://example.test"),
            new SubtitleCue(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "  "),
            dialogueOne,
            dialogueTwo));
        var translator = new StubTranslator("good morning", "come in");
        var detector = new StubDetector();
        var transcriber = new StubTranscriber(
            new SpeechSegment(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(31), "good morning", 0.8f),
            new SpeechSegment(TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(36), "come in", 0.9f));
        var matcher = new SubtitleSpeechMatcher(parser, translator, detector, transcriber);

        var result = await matcher.MatchAsync("movie.mp4", "subtitles.srt", TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.Equal([dialogueOne, dialogueTwo], result.Select(match => match.Cue));
        Assert.Equal(["good morning", "come in"], result.Select(match => match.Translation));
        Assert.All(result, match => Assert.Equal(1, match.Confidence));
        Assert.Equal("movie.mp4", detector.MoviePath);
        Assert.Equal(TimeSpan.FromMinutes(5), detector.AnalysisDuration);
        Assert.Equal(["اول", "دوم"], translator.Texts);
        Assert.Single(transcriber.Audio);
        Assert.All(transcriber.Audio, audio =>
        {
            Assert.Equal(TimeSpan.Zero, audio.Start);
            Assert.Equal(TimeSpan.FromSeconds(7), audio.Duration);
        });
    }

    [Fact]
    public async Task MatchAsync_excludes_low_confidence_pairs_without_consuming_valid_later_pairs()
    {
        var first = new SubtitleCue(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), "اول");
        var second = new SubtitleCue(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "دوم");
        var matcher = new SubtitleSpeechMatcher(
            new SubtitleParsingClient(new StubReader(first, second)),
            new StubTranslator("unrelated text", "the final answer"),
            new StubDetector(),
            new StubTranscriber(
                new SpeechSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11), "the final answer", 0.8f)));

        var result = await matcher.MatchAsync("movie.mp4", "subtitles.srt", cancellationToken: TestContext.Current.CancellationToken);

        var match = Assert.Single(result);
        Assert.Same(second, match.Cue);
        Assert.Equal("the final answer", match.Speech.Text);
    }

    [Theory]
    [InlineData("Hello, WORLD!", "hello world")]
    [InlineData("we're here", "were here")]
    public void FuzzyTextSimilarity_ignores_case_and_punctuation(string left, string right)
    {
        Assert.Equal(1, FuzzyTextSimilarity.Score(left, right));
    }

    private sealed class StubReader(params SubtitleCue[] cues) : ISubtitleReader
    {
        public IEnumerable<SubtitleCue> Read(string subtitlePath) => cues;
    }

    private sealed class StubTranslator(params string[] translations) : ITextTranslator
    {
        public IReadOnlyList<string>? Texts { get; private set; }

        public Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            Texts = texts.ToArray();
            return Task.FromResult<IReadOnlyList<string>>(translations);
        }
    }

    private sealed class StubDetector : IVoiceActivityDetector
    {
        public string? MoviePath { get; private set; }
        public TimeSpan? AnalysisDuration { get; private set; }

        public VoiceActivityAnalysis Analyze(string moviePath, TimeSpan? analysisDuration = null,
            CancellationToken cancellationToken = default)
        {
            MoviePath = moviePath;
            AnalysisDuration = analysisDuration;
            return new VoiceActivityAnalysis(
                new PcmAudio(1, 1, TimeSpan.Zero, new short[20]),
                [new VoiceActivity(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5))]);
        }
    }

    private sealed class StubTranscriber(params SpeechSegment[] speech) : IEnglishTranscriber
    {
        public List<PcmAudio> Audio { get; } = [];

        public IEnumerable<SpeechSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default)
        {
            Audio.Add(audio);
            return speech;
        }
    }
}

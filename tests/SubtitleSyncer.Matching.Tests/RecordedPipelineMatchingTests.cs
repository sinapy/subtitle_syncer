using SubtitleSyncer.Audio;
using SubtitleSyncer.Matching;
using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.SubtitleParsing.Client;
using SubtitleSyncer.Transcription;
using SubtitleSyncer.Translation;
using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Matching.Tests;

public sealed class RecordedPipelineMatchingTests
{
    [Fact]
    public async Task MatchAsync_returns_no_match_for_recorded_54_and_63_second_pipeline_responses()
    {
        var matcher = new SubtitleSpeechMatcher(
            new SubtitleParsingClient(new RecordedCueReader()),
            new RecordedTranslator(),
            new RecordedActivityDetector(),
            new RecordedTranscriber());

        var matches = await matcher.MatchAsync("movie.mp4", "subtitles.srt", TimeSpan.FromSeconds(70),
            TestContext.Current.CancellationToken);

        // The recorded translation and transcription texts are semantically mismatched,
        // so the lexical baseline correctly emits no high-confidence match.
        Assert.Empty(matches);
    }

    private sealed class RecordedCueReader : ISubtitleReader
    {
        // Exact first-ten response captured from SrtSubtitleReader.Read().
        public IEnumerable<SubtitleCue> Read(string subtitlePath) =>
        [
            new SubtitleCue(1, TimeSpan.FromSeconds(1.499), TimeSpan.FromSeconds(30.485),
                "تنظيم شده توسط : صابر رشيدي\nEmail : Black_dragon743@yahoo.com"),
            new SubtitleCue(2, TimeSpan.FromSeconds(33.453), TimeSpan.FromSeconds(35.620),
                "آخرين سفارشات ، لطفا ..."),
            new SubtitleCue(3, TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(44), "- شان.\n- هان !"),
            new SubtitleCue(4, TimeSpan.FromSeconds(44.100), TimeSpan.FromSeconds(47.299),
                "- ميفهمي چي ميگم ؟\n- آره ، کاملا"),
            new SubtitleCue(5, TimeSpan.FromSeconds(48.399), TimeSpan.FromSeconds(51.099),
                "ميدونم که اون بهترين دوستته!\nاما تو داري باهاش زندگي ميکني !"),
            new SubtitleCue(6, TimeSpan.FromSeconds(51.199), TimeSpan.FromSeconds(52.664), "ميدونم"),
            new SubtitleCue(7, TimeSpan.FromSeconds(52.699), TimeSpan.FromSeconds(56.299),
                "مسيله اين نيست که از \"اد\" خوشنم نميام.\nاد ، مسيله اين نيست که من از تو خوشم نمياد"),
            new SubtitleCue(8, TimeSpan.FromSeconds(56.334), TimeSpan.FromSeconds(58.664), "خيلي خوب"),
            new SubtitleCue(9, TimeSpan.FromSeconds(58.699), TimeSpan.FromSeconds(61.899),
                "- خيلي خوب ميشد اگه ما ميتونستيم ...\n- لعنت ..."),
            new SubtitleCue(10, TimeSpan.FromSeconds(61.934), TimeSpan.FromSeconds(65.099),
                "...مدت بيشتري و با هم ميبوديم !\n- عوضي !!!")
        ];
    }

    private sealed class RecordedTranslator : ITextTranslator
    {
        public Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            // Cue 1 is excluded by ExcludeUrls before this dependency is called.
            Assert.Equal(9, texts.Count);
            return Task.FromResult<IReadOnlyList<string>>([
                "Others' orders, please...",
                "Shan. Han!",
                "- I don't know what you're talking about.\n- Oh, really?",
                "I hope he's the best friend you have!\nBut you're living with him, aren't you?",
                "I am.",
                "Mehran shouldn't say \"ad\" to me. Ad, Mehran shouldn't say hello to me.",
                "Very good",
                "You're so good if we could die... Hell...",
                "We will not translate the subtitle text."
            ]);
        }
    }

    private sealed class RecordedActivityDetector : IVoiceActivityDetector
    {
        public VoiceActivityAnalysis Analyze(string moviePath, TimeSpan? analysisDuration = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new VoiceActivityAnalysis(
                new PcmAudio(16_000, 1, TimeSpan.Zero, new short[16_000 * 70]),
                [
                    new VoiceActivity(TimeSpan.FromSeconds(54.130), TimeSpan.FromSeconds(55.694)),
                    new VoiceActivity(TimeSpan.FromSeconds(63.506), TimeSpan.FromSeconds(63.998))
                ]);
        }
    }

    private sealed class RecordedTranscriber : IEnglishTranscriber
    {
        public IEnumerable<SpeechSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return audio.Start < TimeSpan.FromSeconds(60)
                ? [new SpeechSegment(TimeSpan.FromSeconds(52.130), TimeSpan.FromSeconds(55.130), "Last notice please.", 1f)]
                : [
                    new SpeechSegment(TimeSpan.FromSeconds(61.506), TimeSpan.FromSeconds(63.506), "Sean.", 1f),
                    new SpeechSegment(TimeSpan.FromSeconds(63.506), TimeSpan.FromSeconds(65.506), "Yeah.", 1f),
                    new SpeechSegment(TimeSpan.FromSeconds(65.506), TimeSpan.FromSeconds(65.998), "Do you see what I'm saying?", 1f)
                ];
        }
    }
}

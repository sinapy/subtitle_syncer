using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.SubtitleParsing.Client;
using SubtitleSyncer.Transcription;
using SubtitleSyncer.Translation;
using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Matching;

/// <summary>Composes local subtitle translation and speech recognition into ordered fuzzy text matches.</summary>
public sealed class SubtitleSpeechMatcher
{
    private static readonly TimeSpan TranscriptionContext = TimeSpan.FromSeconds(2);
    private readonly SubtitleParsingClient subtitleParser;
    private readonly ITextTranslator translator;
    private readonly IVoiceActivityDetector voiceActivityDetector;
    private readonly IEnglishTranscriber transcriber;
    private readonly MatchingOptions options;

    public SubtitleSpeechMatcher(
        SubtitleParsingClient subtitleParser,
        ITextTranslator translator,
        IVoiceActivityDetector voiceActivityDetector,
        IEnglishTranscriber transcriber,
        MatchingOptions? options = null)
    {
        this.subtitleParser = subtitleParser ?? throw new ArgumentNullException(nameof(subtitleParser));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.voiceActivityDetector = voiceActivityDetector ?? throw new ArgumentNullException(nameof(voiceActivityDetector));
        this.transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        this.options = options ?? new MatchingOptions();
        this.options.Validate();
    }

    public async Task<IReadOnlyList<SubtitleSpeechMatch>> MatchAsync(
        string moviePath,
        string subtitlePath,
        TimeSpan? analysisDuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moviePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(subtitlePath);
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = voiceActivityDetector.Analyze(moviePath, analysisDuration, cancellationToken);
        var speech = TranscribeActivities(analysis, cancellationToken);
        var cues = subtitleParser.Read(subtitlePath)
            .ExcludeHtmlTag()
            .ExcludeUrls()
            .ExcludeEmpty()
            .OrderBy(cue => cue.Start)
            .ThenBy(cue => cue.Index)
            .ToArray();
        var translations = await translator.TranslateAsync(cues.Select(cue => cue.Text).ToArray(), cancellationToken);
        if (translations.Count != cues.Length)
        {
            throw new InvalidOperationException("The translator must return one item for every supplied subtitle cue.");
        }

        return FindOrderedMatches(cues, translations, speech);
    }

    private List<SpeechSegment> TranscribeActivities(VoiceActivityAnalysis analysis, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var speech = new List<SpeechSegment>();
        foreach (var activity in analysis.Activities.OrderBy(activity => activity.Start))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = Max(analysis.Audio.Start, activity.Start - TranscriptionContext);
            var end = Min(analysis.Audio.End, activity.End + TranscriptionContext);
            if (end <= start)
            {
                continue;
            }

            speech.AddRange(transcriber.Transcribe(analysis.Audio.Slice(start, end), cancellationToken));
        }

        return speech.OrderBy(segment => segment.Start).ThenBy(segment => segment.End).ToList();
    }

    private IReadOnlyList<SubtitleSpeechMatch> FindOrderedMatches(
        IReadOnlyList<SubtitleCue> cues,
        IReadOnlyList<string> translations,
        IReadOnlyList<SpeechSegment> speech)
    {
        var rows = cues.Count;
        var columns = speech.Count;
        var scores = new double[rows, columns];
        var dp = new double[rows + 1, columns + 1];

        for (var cueIndex = 0; cueIndex < rows; cueIndex++)
        {
            for (var speechIndex = 0; speechIndex < columns; speechIndex++)
            {
                var score = FuzzyTextSimilarity.Score(translations[cueIndex], speech[speechIndex].Text);
                scores[cueIndex, speechIndex] = score;
                var matched = score >= options.MinimumConfidence
                    ? dp[cueIndex, speechIndex] + score
                    : double.NegativeInfinity;
                dp[cueIndex + 1, speechIndex + 1] = Math.Max(matched,
                    Math.Max(dp[cueIndex, speechIndex + 1], dp[cueIndex + 1, speechIndex]));
            }
        }

        var matches = new List<SubtitleSpeechMatch>();
        for (int cueIndex = rows, speechIndex = columns; cueIndex > 0 && speechIndex > 0;)
        {
            var score = scores[cueIndex - 1, speechIndex - 1];
            if (score >= options.MinimumConfidence &&
                NearlyEqual(dp[cueIndex, speechIndex], dp[cueIndex - 1, speechIndex - 1] + score))
            {
                matches.Add(new SubtitleSpeechMatch(cues[cueIndex - 1], translations[cueIndex - 1],
                    speech[speechIndex - 1], score));
                cueIndex--;
                speechIndex--;
            }
            else if (dp[cueIndex - 1, speechIndex] >= dp[cueIndex, speechIndex - 1])
            {
                cueIndex--;
            }
            else
            {
                speechIndex--;
            }
        }

        matches.Reverse();
        return matches;
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.000_001;
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}

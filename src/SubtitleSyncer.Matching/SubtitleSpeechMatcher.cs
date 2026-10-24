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
        CancellationToken cancellationToken = default,
        IProgress<MatchingProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moviePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(subtitlePath);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new MatchingProgress("Audio analysis", "Preparing audio and detecting speech."));
        var analysis = voiceActivityDetector.Analyze(moviePath, analysisDuration, cancellationToken);
        progress?.Report(new MatchingProgress("Audio analysis",
            $"Detected {analysis.Activities.Count} speech activity window(s) in {analysis.Audio.Duration}."));
        foreach (var activity in analysis.Activities)
        {
            progress?.Report(new MatchingProgress("Audio analysis",
                $"Speech window {activity.Start} to {activity.End}.", IsDetail: true));
        }

        progress?.Report(new MatchingProgress("Transcription", "Transcribing detected speech."));
        var speech = TranscribeActivities(analysis, cancellationToken);
        progress?.Report(new MatchingProgress("Transcription", $"Produced {speech.Count} speech segment(s)."));
        foreach (var segment in speech)
        {
            progress?.Report(new MatchingProgress("Transcription",
                $"{segment.Start} to {segment.End}: {segment.Text}", IsDetail: true));
        }

        progress?.Report(new MatchingProgress("Subtitle parsing", "Reading eligible subtitle cues."));
        var cues = subtitleParser.Read(subtitlePath)
            .ExcludeHtmlTag()
            .ExcludeUrls()
            .ExcludeEmpty()
            .Where(cue => cue.Start < analysis.Audio.End && cue.End > analysis.Audio.Start)
            .OrderBy(cue => cue.Start)
            .ThenBy(cue => cue.Index)
            .Take(50)
            .ToArray();
        progress?.Report(new MatchingProgress("Subtitle parsing", $"Selected {cues.Length} cue(s) for translation."));
        foreach (var cue in cues)
        {
            progress?.Report(new MatchingProgress("Subtitle parsing",
                $"Cue {cue.Index}, {cue.Start} to {cue.End}: {cue.Text}", IsDetail: true));
        }

        progress?.Report(new MatchingProgress("Translation", $"Translating {cues.Length} cue(s)."));
        var translations = await translator.TranslateAsync(cues.Select(cue => cue.Text).ToArray(), cancellationToken);
        if (translations.Count != cues.Length)
        {
            throw new InvalidOperationException("The translator must return one item for every supplied subtitle cue.");
        }

        progress?.Report(new MatchingProgress("Translation", "Translation complete."));
        for (var index = 0; index < cues.Length; index++)
        {
            progress?.Report(new MatchingProgress("Translation",
                $"Cue {cues[index].Index}: {translations[index]}", IsDetail: true));
        }
        progress?.Report(new MatchingProgress("Alignment", "Finding chronological text matches."));

        var matches = FindOrderedMatches(cues, translations, speech);
        progress?.Report(new MatchingProgress("Alignment", $"Found {matches.Count} match(es)."));
        foreach (var match in matches)
        {
            progress?.Report(new MatchingProgress("Alignment",
                $"Cue {match.Cue.Index} at {match.Cue.Start} matched speech at {match.Speech.Start}; " +
                $"confidence {match.Confidence:P2}; translated: {match.Translation}; heard: {match.Speech.Text}",
                IsDetail: true));
        }

        return matches;
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
                var score = FuzzyTextSimilarity.HasMinimumContent(
                    translations[cueIndex], speech[speechIndex].Text, options.MinimumTextLength)
                    ? FuzzyTextSimilarity.Score(translations[cueIndex], speech[speechIndex].Text)
                    : 0;
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

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SubtitleSyncer.Vad;

try
{
    var options = CommandLineOptions.Parse(args);
    if (!File.Exists(options.VideoPath) || !File.Exists(options.SubtitlePath))
    {
        throw new ArgumentException("--video and --subtitles must name existing files.");
    }

    var manifestPath = Path.ChangeExtension(options.ModelPath, ".json");
    if (!File.Exists(options.ModelPath) || !File.Exists(manifestPath))
    {
        throw new ArgumentException("The ONNX model and its adjacent JSON manifest must both exist.");
    }

    var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    }) ?? throw new InvalidDataException("The model manifest is invalid.");
    manifest.Validate(options.ModelPath);

    var analysisDuration = TimeSpan.FromSeconds(options.AnalysisSeconds);
    var analysis = new VoiceActivityDetector(new FfmpegAudioPreparation(), new TenVadEngine())
        .Analyze(options.VideoPath, analysisDuration);
    var duration = analysis.Audio.Duration.TotalSeconds;
    var speech = analysis.Activities.Select(activity => new Interval(activity.Start.TotalSeconds, activity.End.TotalSeconds)).ToArray();
    var cues = SrtParser.Parse(options.SubtitlePath).Where(cue => cue.End <= duration).ToArray();
    if (cues.Length == 0 || speech.Length == 0)
    {
        throw new InvalidDataException("The analysed timeline needs at least one subtitle cue and one speech activity.");
    }

    using var session = InferenceSessionFactory.Create(options.ModelPath, options.Device, out var device);
    var scorer = new OffsetScorer(session, manifest);
    var (predicted, confidence) = scorer.Estimate(cues, speech);
    var correction = predicted - manifest.CalibrationBiasSeconds;

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        offset_seconds = correction,
        apply_to_subtitles = "Add this value to every subtitle start and end timestamp.",
        confidence,
        analysis_seconds = duration,
        activity_count = speech.Length,
        cue_count = cues.Length,
        device,
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or OnnxRuntimeException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

file sealed record CommandLineOptions(string VideoPath, string SubtitlePath, string ModelPath, double AnalysisSeconds, string Device)
{
    public static CommandLineOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Usage: SubtitleSyncer.Sync.Console --video <movie> --subtitles <file.srt> --model <model.onnx> [--analysis-seconds 3600] [--device auto|cpu|cuda]");
            }
            values[args[index]] = args[index + 1];
        }

        if (!values.TryGetValue("--video", out var video) || !values.TryGetValue("--subtitles", out var subtitles) || !values.TryGetValue("--model", out var model))
        {
            throw new ArgumentException("--video, --subtitles, and --model are required.");
        }
        var seconds = values.TryGetValue("--analysis-seconds", out var secondsText)
            ? double.Parse(secondsText, CultureInfo.InvariantCulture) : 3600;
        var device = values.GetValueOrDefault("--device", "auto").ToLowerInvariant();
        if (!double.IsFinite(seconds) || seconds <= 0 || device is not ("auto" or "cpu" or "cuda"))
        {
            throw new ArgumentException("--analysis-seconds must be positive and --device must be auto, cpu, or cuda.");
        }
        return new(Path.GetFullPath(video), Path.GetFullPath(subtitles), Path.GetFullPath(model), seconds, device);
    }
}

file sealed record ModelManifest(int SchemaVersion, string ModelFile, ModelTensor Input, ModelTensor Output,
    string[] FeatureNames, double CalibrationBiasSeconds, CandidateSearch CandidateSearch)
{
    public void Validate(string modelPath)
    {
        if (SchemaVersion != 1 || FeatureNames.Length != InferenceContract.FeatureCount || Input.Name != "features" || Output.Name != "logits" ||
            Path.GetFileName(modelPath) != ModelFile || CandidateSearch.CoarseStepSeconds <= 0 || CandidateSearch.FineStepSeconds <= 0)
        {
            throw new InvalidDataException("The ONNX model manifest is unsupported or does not match its model file.");
        }
    }
}

file sealed record ModelTensor(string Name, string DataType, JsonElement Shape);
file sealed record CandidateSearch(double CoarseStartSeconds, double CoarseEndSeconds, double CoarseStepSeconds,
    double FineRadiusSeconds, double FineStepSeconds);
file readonly record struct Interval(double Start, double End) { public double Duration => End - Start; }

file static class SrtParser
{
    private static readonly Regex TimeLine = new(@"(?<start>\d{2,}:\d{2}:\d{2}[,.]\d{3})\s+-->\s+(?<end>\d{2,}:\d{2}:\d{2}[,.]\d{3})", RegexOptions.CultureInvariant);

    public static IReadOnlyList<Interval> Parse(string path)
    {
        var cues = TimeLine.Matches(File.ReadAllText(path, Encoding.UTF8))
            .Select(match => new Interval(ToSeconds(match.Groups["start"].Value), ToSeconds(match.Groups["end"].Value)))
            .Where(interval => interval.End > interval.Start)
            .ToArray();
        return cues.Length > 0 ? cues : throw new InvalidDataException("No subtitle intervals were found.");
    }

    private static double ToSeconds(string value)
    {
        var pieces = value.Replace(',', '.').Split([':', '.']);
        return int.Parse(pieces[0], CultureInfo.InvariantCulture) * 3600 + int.Parse(pieces[1], CultureInfo.InvariantCulture) * 60 +
            int.Parse(pieces[2], CultureInfo.InvariantCulture) + int.Parse(pieces[3], CultureInfo.InvariantCulture) / 1000d;
    }
}

file sealed class OffsetScorer(InferenceSession session, ModelManifest manifest)
{
    public (double Offset, double Confidence) Estimate(IReadOnlyList<Interval> cues, IReadOnlyList<Interval> speech)
    {
        var coarse = Enumerable.Range(0, (int)Math.Round((manifest.CandidateSearch.CoarseEndSeconds - manifest.CandidateSearch.CoarseStartSeconds) / manifest.CandidateSearch.CoarseStepSeconds) + 1)
            .Select(index => manifest.CandidateSearch.CoarseStartSeconds + index * manifest.CandidateSearch.CoarseStepSeconds).ToArray();
        var center = coarse[ArgMax(Score(cues, speech, coarse))];
        var fine = Enumerable.Range(0, (int)Math.Round(2 * manifest.CandidateSearch.FineRadiusSeconds / manifest.CandidateSearch.FineStepSeconds) + 1)
            .Select(index => center - manifest.CandidateSearch.FineRadiusSeconds + index * manifest.CandidateSearch.FineStepSeconds).ToArray();
        var scores = Score(cues, speech, fine);
        var winner = ArgMax(scores);
        return (fine[winner], scores[winner]);
    }

    private float[] Score(IReadOnlyList<Interval> cues, IReadOnlyList<Interval> speech, IReadOnlyList<double> candidates)
    {
        var input = new DenseTensor<float>(new[] { candidates.Count, InferenceContract.FeatureCount });
        for (var candidate = 0; candidate < candidates.Count; candidate++)
        {
            var features = Features(cues, speech, candidates[candidate]);
            for (var feature = 0; feature < InferenceContract.FeatureCount; feature++) input[candidate, feature] = features[feature];
        }
        using var results = session.Run([NamedOnnxValue.CreateFromTensor(manifest.Input.Name, input)]);
        var logits = results.Single(result => result.Name == manifest.Output.Name).AsTensor<float>().ToArray();
        return logits.Select(Sigmoid).ToArray();
    }

    private static float[] Features(IReadOnlyList<Interval> cues, IReadOnlyList<Interval> speech, double correction)
    {
        double totalCueDuration = 0, totalSpeechDuration = 0, overlapTotal = 0, durationAgreement = 0, meanOverlapSum = 0;
        var supported = 0;
        var activityIndex = 0;
        foreach (var cue in cues)
        {
            var start = cue.Start + correction;
            var end = cue.End + correction;
            var duration = end - start;
            if (duration <= 0) continue;
            while (activityIndex < speech.Count && speech[activityIndex].End <= start) activityIndex++;
            var overlap = 0d;
            for (var index = activityIndex; index < speech.Count && speech[index].Start < end; index++)
                overlap += Math.Max(0, Math.Min(end, speech[index].End) - Math.Max(start, speech[index].Start));
            totalCueDuration += duration;
            overlapTotal += overlap;
            meanOverlapSum += overlap / duration;
            if (overlap > 0) { supported++; durationAgreement += Math.Min(overlap, duration) / Math.Max(overlap, duration) * duration; }
        }
        foreach (var activity in speech) totalSpeechDuration += activity.Duration;
        const double epsilon = 1e-6;
        return [(float)(overlapTotal / Math.Max(totalCueDuration, epsilon)), (float)(overlapTotal / Math.Max(totalSpeechDuration, epsilon)),
            (float)(durationAgreement / Math.Max(totalCueDuration, epsilon)), (float)supported / Math.Max(cues.Count, 1),
            (float)(meanOverlapSum / Math.Max(cues.Count, 1)), (float)(speech.Count / Math.Max(totalCueDuration, epsilon))];
    }

    private static int ArgMax(IReadOnlyList<float> scores) => scores.Select((score, index) => (score, index)).MaxBy(value => value.score).index;
    private static float Sigmoid(float logit) => logit >= 0 ? 1f / (1f + MathF.Exp(-logit)) : MathF.Exp(logit) / (1f + MathF.Exp(logit));
}

file static class InferenceSessionFactory
{
    public static InferenceSession Create(string modelPath, string requestedDevice, out string selectedDevice)
    {
        if (requestedDevice is "auto" or "cuda")
        {
            try
            {
                var cudaOptions = new SessionOptions();
                cudaOptions.AppendExecutionProvider_CUDA(0);
                selectedDevice = "cuda";
                return new InferenceSession(modelPath, cudaOptions);
            }
            catch when (requestedDevice == "auto") { }
        }
        if (requestedDevice == "cuda") throw new InvalidOperationException("CUDA was requested but is not available to ONNX Runtime.");
        selectedDevice = "cpu";
        return new InferenceSession(modelPath, new SessionOptions());
    }
}

file static class InferenceContract
{
    public const int FeatureCount = 6;
}

using SubtitleSyncer.Matching;
using SubtitleSyncer.Matching.Client;
using SubtitleSyncer.SubtitleParsing.Client;
using SubtitleSyncer.Transcription;
using SubtitleSyncer.Translation;
using SubtitleSyncer.Vad;

if (args.Length > 3)
{
    Console.Error.WriteLine("Usage: SubtitleSyncer.Matching.Console [movie.mp4] [subtitles.srt] [analysis-seconds]");
    return 2;
}

var repositoryRoot = FindRepositoryRoot();
var moviePath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repositoryRoot, "tests", "data", "Shaun.Of.The.Dead.(2004).[REMASTERED].[REPACK].[1080p].[BluRay].[5.1].[YTS.MX]-xpost.mp4");
var subtitlePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(repositoryRoot, "tests", "data", "Shaun_Of_The_Dead.srt");
var analysisDuration = args.Length > 2
    ? TimeSpan.FromSeconds(double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture))
    : VadOptions.DefaultAnalysisDuration;

var transcriptionModelPath = Environment.GetEnvironmentVariable("TRANSCRIPTION_MODEL_PATH")
    ?? Path.Combine(repositoryRoot, "models", "ggml-small.en-q5_1.bin");
var translationModelPath = Environment.GetEnvironmentVariable("TRANSLATION_MODEL_PATH")
    ?? Path.Combine(repositoryRoot, "models", "qwen2.5-1.5b-instruct-q4_k_m.gguf");

if (!File.Exists(moviePath) || !File.Exists(subtitlePath))
{
    Console.Error.WriteLine($"Input not found. Movie: {moviePath}; subtitles: {subtitlePath}");
    return 2;
}

if (!File.Exists(transcriptionModelPath) || !File.Exists(translationModelPath))
{
    Console.Error.WriteLine($"Model not found. Whisper: {transcriptionModelPath}; translation: {translationModelPath}");
    return 2;
}

Console.WriteLine("Loading translation model...");
TranslationRuntime.ShowNativeErrorsOnly(Console.Error.Write);
using var translator = new PersianEnglishTranslator(new TranslationOptions(translationModelPath));
Console.WriteLine("Translation model loaded.");

Console.WriteLine("Loading transcription model...");
using var transcriber = new EnglishTranscription(new TranscriptionOptions(transcriptionModelPath));
Console.WriteLine("Transcription model loaded.");
var matcher = new SubtitleSpeechMatcher(
    new SubtitleParsingClient(),
    translator,
    new VoiceActivityDetector(new FfmpegAudioPreparation(), new TenVadEngine()),
    transcriber);
var client = new MatchingClient(matcher);

Console.WriteLine($"Matching {Path.GetFileName(subtitlePath)} against {Path.GetFileName(moviePath)}");
Console.WriteLine($"Analyzing the first {analysisDuration} of audio...");
var includeDetailLogs = string.Equals(Environment.GetEnvironmentVariable("MATCHING_DEBUG"), "1",
    StringComparison.Ordinal);
var progress = new Progress<MatchingProgress>(update =>
{
    if (!update.IsDetail || includeDetailLogs)
    {
        Console.WriteLine($"[{update.Stage}] {update.Message}");
    }
});
var matches = await client.MatchAsync(moviePath, subtitlePath, analysisDuration, progress: progress);

Console.WriteLine($"Found {matches.Count} subtitle-to-speech match(es).");
foreach (var match in matches)
{
    var offset = match.Speech.Start - match.Cue.Start;
    Console.WriteLine($"{match.Cue.Start:hh\\:mm\\:ss}  {match.Confidence:P0}  offset {offset}  {match.Translation}");
    Console.WriteLine($"  heard: {match.Speech.Text}");
}

if (matches.TryGetMedianOffset(out var medianOffset))
{
    Console.WriteLine("[Offset estimation] Found a consistent offset cluster.");
    Console.WriteLine($"Median offset: {medianOffset}");
}
else
{
    Console.WriteLine("[Offset estimation] No consistent offset cluster was found.");
    Console.WriteLine("No reliable offset: at least three matches within two seconds are required.");
}

return 0;

static string FindRepositoryRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "data", "Shaun_Of_The_Dead.srt")))
            {
                return directory.FullName;
            }
        }
    }

    throw new DirectoryNotFoundException("Could not locate repository fixtures. Pass the movie and subtitle paths explicitly.");
}

using System.Globalization;
using SubtitleSyncer.SubtitleParsing;
using SubtitleSyncer.SubtitleParsing.Client;
using SubtitleSyncer.Transcription;
using SubtitleSyncer.Translation;
using SubtitleSyncer.Vad;

var root = FindRepositoryRoot();
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5088");

var transcriptionModelPath = Environment.GetEnvironmentVariable("TRANSCRIPTION_MODEL_PATH") ??
    Path.Combine(root, "models", "ggml-small.en-q5_1.bin");
var translationModelPath = Environment.GetEnvironmentVariable("TRANSLATION_MODEL_PATH") ??
    Path.Combine(root, "models", "qwen2.5-1.5b-instruct-q4_k_m.gguf");

if (!File.Exists(transcriptionModelPath) || !File.Exists(translationModelPath))
{
    throw new FileNotFoundException("Set TRANSCRIPTION_MODEL_PATH and TRANSLATION_MODEL_PATH to local model files.");
}

TranslationRuntime.ShowNativeErrorsOnly(Console.Error.Write);
builder.Services.AddSingleton(new SubtitleParsingClient());
builder.Services.AddSingleton<ITextTranslator>(_ => new PersianEnglishTranslator(new TranslationOptions(translationModelPath)));
builder.Services.AddSingleton<IEnglishTranscriber>(_ => new EnglishTranscription(new TranscriptionOptions(transcriptionModelPath)));
builder.Services.AddSingleton<IVoiceActivityDetector>(_ => new VoiceActivityDetector(
    new FfmpegAudioPreparation(), new TenVadEngine()));
builder.Services.AddSingleton<ReviewPreparationService>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/defaults", () => new
{
    moviePath = Path.Combine(root, "tests", "data", "Shaun.Of.The.Dead.(2004).[REMASTERED].[REPACK].[1080p].[BluRay].[5.1].[YTS.MX]-xpost.mp4"),
    subtitlePath = Path.Combine(root, "tests", "data", "Shaun_Of_The_Dead.srt"),
    analysisSeconds = 70,
});

app.MapPost("/api/review", async (ReviewRequest request, ReviewPreparationService service, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.MoviePath) || string.IsNullOrWhiteSpace(request.SubtitlePath))
    {
        return Results.BadRequest("Both a movie path and a subtitle path are required.");
    }

    if (!File.Exists(request.MoviePath) || !File.Exists(request.SubtitlePath))
    {
        return Results.BadRequest("The movie or subtitle path does not exist.");
    }

    if (request.AnalysisSeconds is <= 0 or > 3_600)
    {
        return Results.BadRequest("Analysis duration must be between one second and one hour.");
    }

    return Results.Ok(await service.PrepareAsync(request, cancellationToken));
});

app.MapPost("/api/offset", (ManualPairSelection selection, ReviewPreparationService service) =>
{
    return service.TryCalculateOffset(selection, out var result)
        ? Results.Ok(result)
        : Results.BadRequest("Select one subtitle cue and one speech segment from the current review session.");
});

Console.WriteLine("Subtitle Syncer review UI: http://127.0.0.1:5088");
await app.RunAsync();

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "SubtitleSyncer.slnx")))
        {
            return directory.FullName;
        }
    }

    throw new DirectoryNotFoundException("Run the review UI from the repository or set model paths explicitly.");
}

public sealed record ReviewRequest(string MoviePath, string SubtitlePath, double AnalysisSeconds);
public sealed record ManualPairSelection(int CueIndex, int SpeechIndex);
public sealed record ReviewCue(int Index, string Start, string End, string PersianText, string EnglishTranslation);
public sealed record ReviewSpeech(int Index, string Start, string End, string Text, float Confidence);
public sealed record ReviewSession(IReadOnlyList<ReviewCue> Cues, IReadOnlyList<ReviewSpeech> Speech);
public sealed record OffsetResult(double OffsetSeconds, string Offset, ReviewCue Cue, ReviewSpeech Speech);

public sealed class ReviewPreparationService(
    SubtitleParsingClient subtitleParser,
    ITextTranslator translator,
    IVoiceActivityDetector voiceActivityDetector,
    IEnglishTranscriber transcriber) : IDisposable
{
    private static readonly TimeSpan Context = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim gate = new(1, 1);
    private ReviewSession? session;

    public async Task<ReviewSession> PrepareAsync(ReviewRequest request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var analysis = voiceActivityDetector.Analyze(request.MoviePath, TimeSpan.FromSeconds(request.AnalysisSeconds), cancellationToken);
            var speech = new List<SpeechSegment>();
            foreach (var activity in analysis.Activities.OrderBy(activity => activity.Start))
            {
                var start = activity.Start - Context < analysis.Audio.Start ? analysis.Audio.Start : activity.Start - Context;
                var end = activity.End + Context > analysis.Audio.End ? analysis.Audio.End : activity.End + Context;
                speech.AddRange(transcriber.Transcribe(analysis.Audio.Slice(start, end), cancellationToken));
            }

            var cues = subtitleParser.Read(request.SubtitlePath)
                .ExcludeHtmlTag()
                .ExcludeUrls()
                .ExcludeEmpty()
                .Where(cue => cue.Start < analysis.Audio.End && cue.End > analysis.Audio.Start)
                .OrderBy(cue => cue.Start)
                .ThenBy(cue => cue.Index)
                .Take(50)
                .ToArray();
            var translations = await translator.TranslateAsync(cues.Select(cue => cue.Text).ToArray(), cancellationToken);
            var review = new ReviewSession(
                cues.Select((cue, index) => new ReviewCue(index, Format(cue.Start), Format(cue.End), cue.Text, translations[index])).ToArray(),
                speech.OrderBy(segment => segment.Start).ThenBy(segment => segment.End)
                    .Select((segment, index) => new ReviewSpeech(index, Format(segment.Start), Format(segment.End), segment.Text, segment.Confidence)).ToArray());
            session = review;
            return review;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool TryCalculateOffset(ManualPairSelection selection, out OffsetResult? result)
    {
        var current = session;
        var cue = current?.Cues.SingleOrDefault(item => item.Index == selection.CueIndex);
        var speech = current?.Speech.SingleOrDefault(item => item.Index == selection.SpeechIndex);
        if (cue is null || speech is null || !TimeSpan.TryParse(cue.Start, CultureInfo.InvariantCulture, out var cueStart) ||
            !TimeSpan.TryParse(speech.Start, CultureInfo.InvariantCulture, out var speechStart))
        {
            result = null;
            return false;
        }

        var offset = speechStart - cueStart;
        result = new OffsetResult(offset.TotalSeconds, $"{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm\\:ss\\.fff}", cue, speech);
        return true;
    }

    public void Dispose()
    {
        gate.Dispose();
        (translator as IDisposable)?.Dispose();
        (transcriber as IDisposable)?.Dispose();
        (voiceActivityDetector as IDisposable)?.Dispose();
    }

    private static string Format(TimeSpan time) => time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}

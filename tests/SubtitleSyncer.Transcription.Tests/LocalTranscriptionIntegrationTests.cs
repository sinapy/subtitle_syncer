using SubtitleSyncer.Transcription;
using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Transcription.Tests;

public sealed class LocalTranscriptionIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Transcribe_padded_audio_recognizes_the_short_Shaun_utterance()
    {
        var root = FindRepositoryRoot();
        var moviePath = Path.Combine(root, "tests", "data",
            "Shaun.Of.The.Dead.(2004).[REMASTERED].[REPACK].[1080p].[BluRay].[5.1].[YTS.MX]-xpost.mp4");
        var modelPath = Path.Combine(root, "models", "ggml-small.en-q5_1.bin");
        Assert.True(File.Exists(moviePath), $"Required local movie fixture was not found: {moviePath}");
        Assert.True(File.Exists(modelPath), $"Required local Whisper model was not found: {modelPath}");

        var analysis = new VoiceActivityDetector(new FfmpegAudioPreparation(), new TenVadEngine())
            .Analyze(moviePath, TimeSpan.FromSeconds(70), TestContext.Current.CancellationToken);
        var activity = Assert.Single(analysis.Activities, activity =>
            activity.Start >= TimeSpan.FromSeconds(62) && activity.Start <= TimeSpan.FromSeconds(64));
        using var transcription = new EnglishTranscription(new TranscriptionOptions(modelPath));

        var padding = TimeSpan.FromSeconds(2);
        var start = activity.Start - padding < analysis.Audio.Start
            ? analysis.Audio.Start
            : activity.Start - padding;
        var end = activity.End + padding > analysis.Audio.End
            ? analysis.Audio.End
            : activity.End + padding;
        var speech = transcription.Transcribe(analysis.Audio.Slice(start, end),
            TestContext.Current.CancellationToken).ToArray();

        Assert.NotEmpty(speech);
        Assert.All(speech, segment =>
        {
            Assert.InRange(segment.Start, analysis.Audio.Start, analysis.Audio.End);
            Assert.InRange(segment.End, segment.Start, analysis.Audio.End);
        });
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SubtitleSyncer.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

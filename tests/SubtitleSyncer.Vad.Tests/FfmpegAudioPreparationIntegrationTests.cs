using SubtitleSyncer.Vad;

namespace SubtitleSyncer.Vad.Tests;

public sealed class FfmpegAudioPreparationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Prepare_and_detect_the_first_five_minutes_of_the_local_test_movie()
    {
        var moviePath = Path.Combine(FindRepositoryRoot(), "tests", "data",
            "Shaun.Of.The.Dead.(2004).[REMASTERED].[REPACK].[1080p].[BluRay].[5.1].[YTS.MX]-xpost.mp4");
        if (!File.Exists(moviePath))
        {
            return; // The large developer-only movie fixture is intentionally not versioned.
        }

        var audio = new FfmpegAudioPreparation().Prepare(moviePath, VadOptions.DefaultAnalysisDuration,
            TestContext.Current.CancellationToken);

        Assert.Equal(16_000, audio.SampleRate);
        Assert.Equal(1, audio.ChannelCount);
        Assert.Equal(TimeSpan.Zero, audio.Start);
        Assert.True(audio.Duration > TimeSpan.Zero);
        Assert.True(audio.Duration <= VadOptions.DefaultAnalysisDuration);

        var activities = new TenVadEngine().Detect(audio, TestContext.Current.CancellationToken);
        Assert.All(activities, activity =>
        {
            Assert.True(activity.Start >= audio.Start);
            Assert.True(activity.End <= audio.End);
            Assert.True(activity.End > activity.Start);
        });
        Assert.Contains(activities, activity => activity.Start >= TimeSpan.FromSeconds(53) &&
            activity.Start <= TimeSpan.FromSeconds(55)); // "Last orders please" at about 00:00:54.
        Assert.Contains(activities, activity => activity.Start >= TimeSpan.FromSeconds(62) &&
            activity.Start <= TimeSpan.FromSeconds(64)); // "John" at about 00:01:03.
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

using System.Text.RegularExpressions;
using SubtitleSyncer.Translation;

namespace SubtitleSyncer.Translation.Tests;

public sealed partial class LocalRuntimeIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TranslateAsync_runs_the_required_local_GGUF_model()
    {
        var root = FindRepositoryRoot();
        var modelPath = Environment.GetEnvironmentVariable("TRANSLATION_MODEL_PATH") ??
            Path.Combine(root, "models", "qwen2.5-1.5b-instruct-q4_k_m.gguf");
        Assert.True(File.Exists(modelPath), $"Required local translation model was not found: {modelPath}");

        using var translator = new PersianEnglishTranslator(new TranslationOptions(modelPath)
        {
            ContextSize = 1024,
            MaxOutputTokens = 64,
        });

        var result = await translator.TranslateAsync(["سلام، حالت چطوره؟"], TestContext.Current.CancellationToken);
        var translation = Assert.Single(result);

        Assert.NotEmpty(translation);
        Assert.DoesNotMatch(PersianCharacter(), translation);
        Assert.Matches("[A-Za-z]", translation);
    }

    [GeneratedRegex("[\u0600-\u06FF]")]
    private static partial Regex PersianCharacter();

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

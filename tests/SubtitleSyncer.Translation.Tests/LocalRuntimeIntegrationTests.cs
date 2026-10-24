using System.Text.RegularExpressions;
using SubtitleSyncer.Translation;

namespace SubtitleSyncer.Translation.Tests;

public sealed partial class LocalRuntimeIntegrationTests
{
    [Fact]
    public async Task TranslateAsync_runs_a_local_GGUF_model_when_TRANSLATION_MODEL_PATH_is_set()
    {
        var modelPath = Environment.GetEnvironmentVariable("TRANSLATION_MODEL_PATH");
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return;
        }

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
}

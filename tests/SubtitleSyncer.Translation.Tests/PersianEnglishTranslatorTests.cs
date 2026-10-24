using SubtitleSyncer.Translation;

namespace SubtitleSyncer.Translation.Tests;

public sealed class PersianEnglishTranslatorTests
{
    [Fact]
    public async Task TranslateAsync_preserves_order_count_and_empty_text_without_inference()
    {
        using var engine = new RecordingEngine("Hello", "How are you?");
        using var translator = new PersianEnglishTranslator(engine);

        var result = await translator.TranslateAsync(["سلام", "", "حال شما چطور است؟"], TestContext.Current.CancellationToken);

        Assert.Equal(["Hello", "", "How are you?"], result);
        Assert.Equal(2, engine.Prompts.Count);
        Assert.Contains("سلام", engine.Prompts[0]);
        Assert.Contains("حال شما چطور است؟", engine.Prompts[1]);
        Assert.Contains("dialogue-ready English", engine.Prompts[0]);
    }

    [Fact]
    public async Task TranslateAsync_trims_model_output_but_keeps_meaningful_line_breaks()
    {
        using var translator = new PersianEnglishTranslator(new RecordingEngine("  Hello\nthere  "));

        var result = await translator.TranslateAsync(["سلام\nآنجا"], TestContext.Current.CancellationToken);

        Assert.Equal("Hello\nthere", Assert.Single(result));
    }

    [Fact]
    public async Task TranslateAsync_rejects_null_items()
    {
        using var translator = new PersianEnglishTranslator(new RecordingEngine());
        IReadOnlyList<string> input = [null!];

        await Assert.ThrowsAsync<ArgumentException>(() => translator.TranslateAsync(input, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TranslateAsync_retries_a_refusal_with_a_stricter_translation_prompt()
    {
        using var engine = new RecordingEngine("We will not translate the subtitle text.", "Last orders, please.");
        using var translator = new PersianEnglishTranslator(engine);

        var result = await translator.TranslateAsync(["آخرين سفارشات، لطفا..."], TestContext.Current.CancellationToken);

        Assert.Equal("Last orders, please.", Assert.Single(result));
        Assert.Equal(2, engine.Prompts.Count);
        Assert.Contains("previous response was not an English translation", engine.Prompts[1]);
        Assert.Contains("آخرين سفارشات", engine.Prompts[1]);
    }

    private sealed class RecordingEngine(params string[] responses) : ITextGenerationEngine
    {
        private readonly Queue<string> responses = new(responses);
        public List<string> Prompts { get; } = [];

        public Task<string> GenerateAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Prompts.Add(text);
            return Task.FromResult(responses.Dequeue());
        }

        public void Dispose() { }
    }
}

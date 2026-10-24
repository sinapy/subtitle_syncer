namespace SubtitleSyncer.Translation;

/// <summary>
/// An on-device Persian-to-English translator for independent subtitle text items.
/// It neither reads subtitle files nor depends on the subtitle-parsing stack.
/// </summary>
public sealed class PersianEnglishTranslator : ITextTranslator, IDisposable
{
    private const string TranslationInstruction = """
        Translate the Persian subtitle text below into natural English. Return only the translation.
        Preserve names, numbers, punctuation, and line breaks when they carry dialogue meaning.
        Do not explain the translation and do not add labels or quotation marks.
        Persian subtitle text:
        """;

    private readonly ITextGenerationEngine engine;
    private readonly bool ownsEngine;
    private bool disposed;

    /// <summary>Loads a locally installed GGUF model through the CPU-only runtime.</summary>
    public PersianEnglishTranslator(TranslationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        engine = new LlamaTextGenerationEngine(options);
        ownsEngine = true;
    }

    /// <summary>Creates a translator over a supplied local engine.</summary>
    public PersianEnglishTranslator(ITextGenerationEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(texts);

        var translations = new string[texts.Count];
        for (var index = 0; index < texts.Count; index++)
        {
            var text = texts[index] ?? throw new ArgumentException(
                "Translation input cannot contain null text.", nameof(texts));
            cancellationToken.ThrowIfCancellationRequested();

            // An empty subtitle needs no inference, but still retains its position in the result.
            translations[index] = string.IsNullOrWhiteSpace(text)
                ? text
                : (await engine.GenerateAsync(BuildPrompt(text), cancellationToken).ConfigureAwait(false)).Trim();
        }

        return translations;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ownsEngine)
        {
            engine.Dispose();
        }
    }

    private static string BuildPrompt(string text) => $"{TranslationInstruction}\n<subtitle>\n{text}\n</subtitle>";
}

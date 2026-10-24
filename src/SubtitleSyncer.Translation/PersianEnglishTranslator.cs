namespace SubtitleSyncer.Translation;

/// <summary>
/// An on-device Persian-to-English translator for independent subtitle text items.
/// It neither reads subtitle files nor depends on the subtitle-parsing stack.
/// </summary>
public sealed class PersianEnglishTranslator : ITextTranslator, IDisposable
{
    private const string TranslationInstruction = """
        Translate every Persian subtitle line below into natural, dialogue-ready English.
        The result will be compared with an English speech transcript, so preserve the spoken meaning
        faithfully and prefer a direct translation over paraphrase. Preserve names, numbers,
        punctuation, speaker turns, and meaningful line breaks. Render Persian names in Latin script.
        Return only the English translation. Never explain, summarize, refuse, or describe the task.
        Persian subtitle text:
        """;

    private const string RetryInstruction = """
        Your previous response was not an English translation. Translate the Persian subtitle now.
        Return only its English dialogue, including every line. Do not discuss the request.
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
            if (string.IsNullOrWhiteSpace(text))
            {
                translations[index] = text;
                continue;
            }

            var translation = (await engine.GenerateAsync(BuildPrompt(text), cancellationToken).ConfigureAwait(false)).Trim();
            if (!IsUsableEnglishTranslation(translation))
            {
                translation = (await engine.GenerateAsync(BuildRetryPrompt(text), cancellationToken).ConfigureAwait(false)).Trim();
            }

            translations[index] = translation;
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

    private static string BuildRetryPrompt(string text) => $"{RetryInstruction}\n<subtitle>\n{text}\n</subtitle>";

    private static bool IsUsableEnglishTranslation(string translation)
    {
        if (string.IsNullOrWhiteSpace(translation) || !translation.Any(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
        {
            return false;
        }

        return !translation.Contains("cannot translate", StringComparison.OrdinalIgnoreCase) &&
            !translation.Contains("can't translate", StringComparison.OrdinalIgnoreCase) &&
            !translation.Contains("will not translate", StringComparison.OrdinalIgnoreCase) &&
            !translation.Contains("unable to translate", StringComparison.OrdinalIgnoreCase);
    }
}

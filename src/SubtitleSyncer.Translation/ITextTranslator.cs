namespace SubtitleSyncer.Translation;

/// <summary>Translates a batch of text while preserving its item order and count.</summary>
public interface ITextTranslator
{
    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}

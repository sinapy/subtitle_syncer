namespace SubtitleSyncer.Translation;

/// <summary>Settings for the local CPU translation model.</summary>
public sealed record TranslationOptions(string ModelPath)
{
    /// <summary>Limits the context reserved for one subtitle translation.</summary>
    public uint ContextSize { get; init; } = 1024;

    /// <summary>Limits the generated English text for one subtitle.</summary>
    public int MaxOutputTokens { get; init; } = 160;
}

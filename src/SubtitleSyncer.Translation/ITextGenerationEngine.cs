namespace SubtitleSyncer.Translation;

/// <summary>Local text generation boundary, primarily allowing deterministic tests without a model.</summary>
public interface ITextGenerationEngine : IDisposable
{
    Task<string> GenerateAsync(string text, CancellationToken cancellationToken);
}

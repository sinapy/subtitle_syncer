namespace SubtitleSyncer.Transcription;

/// <summary>Settings for a locally stored Whisper GGML model and CPU inference.</summary>
public sealed record TranscriptionOptions(string ModelPath)
{
    /// <summary>The Whisper language code. The product baseline is English.</summary>
    public string Language { get; init; } = "en";

}

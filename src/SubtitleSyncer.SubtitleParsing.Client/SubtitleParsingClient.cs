using SubtitleSyncer.SubtitleParsing;

namespace SubtitleSyncer.SubtitleParsing.Client;

/// <summary>Application-facing entry point for parsing an SRT file.</summary>
public sealed class SubtitleParsingClient
{
    private readonly ISubtitleReader reader;

    public SubtitleParsingClient(ISubtitleReader? reader = null)
    {
        this.reader = reader ?? new SrtSubtitleReader();
    }

    /// <summary>Reads an SRT file. Callers may chain optional cue extensions on the result.</summary>
    public IEnumerable<SubtitleCue> Read(string subtitlePath) => reader.Read(subtitlePath);
}

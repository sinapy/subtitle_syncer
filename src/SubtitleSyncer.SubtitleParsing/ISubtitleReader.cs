namespace SubtitleSyncer.SubtitleParsing;

public interface ISubtitleReader
{
    IEnumerable<SubtitleCue> Read(string subtitlePath);
}

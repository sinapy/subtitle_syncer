namespace SubtitleSyncer.SubtitleParsing;

/// <summary>A single subtitle item on the original movie timeline.</summary>
public sealed record SubtitleCue(int Index, TimeSpan Start, TimeSpan End, string Text);

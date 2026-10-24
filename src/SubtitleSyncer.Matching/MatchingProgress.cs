namespace SubtitleSyncer.Matching;

/// <summary>Reports a completed or starting stage of subtitle-to-speech matching.</summary>
public sealed record MatchingProgress(string Stage, string Message, bool IsDetail = false);

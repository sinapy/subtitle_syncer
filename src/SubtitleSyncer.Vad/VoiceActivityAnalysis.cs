namespace SubtitleSyncer.Vad;

/// <summary>The bounded movie audio and chronological activity windows found in it.</summary>
public sealed record VoiceActivityAnalysis(PcmAudio Audio, IReadOnlyList<VoiceActivity> Activities);

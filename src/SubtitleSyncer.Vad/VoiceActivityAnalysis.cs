namespace SubtitleSyncer.Vad;

using SubtitleSyncer.Audio;

/// <summary>The bounded movie audio and chronological activity windows found in it.</summary>
public sealed record VoiceActivityAnalysis(PcmAudio Audio, IReadOnlyList<VoiceActivity> Activities);

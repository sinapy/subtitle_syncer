namespace SubtitleSyncer.Vad;

using SubtitleSyncer.Audio;

/// <summary>Model-specific VAD boundary; implementations must return movie-timeline windows.</summary>
public interface IVadEngine
{
    IReadOnlyList<VoiceActivity> Detect(PcmAudio audio, CancellationToken cancellationToken = default);
}

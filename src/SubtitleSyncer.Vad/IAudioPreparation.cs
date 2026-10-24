namespace SubtitleSyncer.Vad;

using SubtitleSyncer.Audio;

/// <summary>Movie-audio preparation owned by the VAD stack.</summary>
public interface IAudioPreparation
{
    PcmAudio Prepare(string moviePath, TimeSpan duration, CancellationToken cancellationToken = default);
}

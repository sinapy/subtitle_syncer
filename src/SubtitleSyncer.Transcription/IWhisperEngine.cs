using SubtitleSyncer.Audio;

namespace SubtitleSyncer.Transcription;

/// <summary>Local Whisper boundary used to keep model inference out of transcription tests.</summary>
public interface IWhisperEngine : IDisposable
{
    IEnumerable<WhisperSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default);
}

/// <summary>A Whisper result whose times are relative to the supplied audio.</summary>
public sealed record WhisperSegment(TimeSpan Start, TimeSpan End, string Text, float Confidence = 1.0f);

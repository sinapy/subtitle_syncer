using SubtitleSyncer.Audio;

namespace SubtitleSyncer.Transcription;

/// <summary>Transcribes prepared audio into English speech on the movie timeline.</summary>
public interface IEnglishTranscriber
{
    IEnumerable<SpeechSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default);

}

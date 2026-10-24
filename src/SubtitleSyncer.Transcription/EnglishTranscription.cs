using SubtitleSyncer.Audio;

namespace SubtitleSyncer.Transcription;

/// <summary>Maps local Whisper results onto the absolute timeline of prepared audio.</summary>
public sealed class EnglishTranscription : IEnglishTranscriber, IDisposable
{
    private readonly IWhisperEngine engine;
    private readonly bool ownsEngine;
    private bool disposed;

    /// <summary>Loads an already downloaded local Whisper model using the CPU runtime.</summary>
    public EnglishTranscription(TranscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        engine = new WhisperNetEngine(options);
        ownsEngine = true;
    }

    /// <summary>Creates a transcriber over a supplied local model adapter.</summary>
    public EnglishTranscription(IWhisperEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public IEnumerable<SpeechSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(audio);

        var speech = new List<SpeechSegment>();
        var previousEnd = TimeSpan.Zero;
        foreach (var segment in engine.Transcribe(audio, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Validate(segment, previousEnd);
            var end = segment.End > audio.Duration ? audio.Duration : segment.End;
            if (end <= segment.Start)
            {
                continue;
            }

            previousEnd = end;

            var text = segment.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                speech.Add(new SpeechSegment(audio.Start + segment.Start, audio.Start + end,
                    text, segment.Confidence));
            }
        }

        return speech;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ownsEngine)
        {
            engine.Dispose();
        }
    }

    private static void Validate(WhisperSegment segment, TimeSpan previousEnd)
    {
        if (segment is null || segment.Text is null || segment.Start < TimeSpan.Zero || segment.End <= segment.Start ||
            segment.Start < previousEnd || float.IsNaN(segment.Confidence) ||
            segment.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("Whisper must return sorted, bounded segments with a valid confidence.");
        }
    }

}

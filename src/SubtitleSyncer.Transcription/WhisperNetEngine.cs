using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SubtitleSyncer.Audio;
using Whisper.net;

namespace SubtitleSyncer.Transcription;

/// <summary>CPU Whisper.net adapter for a locally stored GGML model.</summary>
internal sealed class WhisperNetEngine : IWhisperEngine
{
    private readonly WhisperFactory factory;
    private readonly WhisperProcessor processor;
    private bool disposed;

    public WhisperNetEngine(TranscriptionOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Language);
        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException("The local Whisper model was not found.", options.ModelPath);
        }

        factory = WhisperFactory.FromPath(options.ModelPath);
        processor = factory.CreateBuilder().WithLanguage(options.Language).Build();
    }

    public IEnumerable<WhisperSegment> Transcribe(PcmAudio audio, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(audio);
        if (audio.SampleRate != 16_000 || audio.ChannelCount != 1)
        {
            throw new ArgumentException("Whisper input must be mono 16 kHz PCM audio.", nameof(audio));
        }

        using var wave = CreateWaveStream(audio);
        var enumerator = processor.ProcessAsync(wave, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                var segment = enumerator.Current;
                yield return new WhisperSegment(segment.Start, segment.End, segment.Text);
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        processor.Dispose();
        factory.Dispose();
    }

    private static MemoryStream CreateWaveStream(PcmAudio audio)
    {
        var sampleBytes = checked(audio.Samples.Length * sizeof(short));
        var stream = new MemoryStream(44 + sampleBytes);
        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36 + sampleBytes);
        "WAVEfmt "u8.CopyTo(header[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], (short)audio.ChannelCount);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], audio.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], audio.SampleRate * audio.ChannelCount * sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], (short)(audio.ChannelCount * sizeof(short)));
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], sampleBytes);
        stream.Write(header);
        stream.Write(MemoryMarshal.AsBytes(audio.Samples.Span));

        stream.Position = 0;
        return stream;
    }
}

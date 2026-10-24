namespace SubtitleSyncer.Vad;

/// <summary>Mono or multi-channel signed 16-bit PCM positioned on the movie timeline.</summary>
public sealed record PcmAudio
{
    private readonly short[] samples;

    public PcmAudio(int sampleRate, int channelCount, TimeSpan start, short[] samples)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (channelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount));
        }

        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Length % channelCount != 0)
        {
            throw new ArgumentException("The sample count must contain complete sample frames.", nameof(samples));
        }

        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Start = start;
        this.samples = samples.ToArray();
    }

    public int SampleRate { get; }
    public int ChannelCount { get; }
    public TimeSpan Start { get; }
    public ReadOnlyMemory<short> Samples => samples;
    public TimeSpan Duration => TimeSpan.FromSeconds((double)samples.Length / ChannelCount / SampleRate);
    public TimeSpan End => Start + Duration;

    /// <summary>Returns audio for an absolute movie-timeline interval.</summary>
    public PcmAudio Slice(TimeSpan start, TimeSpan end)
    {
        if (start < Start || end > End || end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The requested interval must be inside this audio.");
        }

        var firstFrame = (int)Math.Floor((start - Start).TotalSeconds * SampleRate);
        var endFrame = (int)Math.Ceiling((end - Start).TotalSeconds * SampleRate);
        var totalFrames = samples.Length / ChannelCount;
        firstFrame = Math.Clamp(firstFrame, 0, totalFrames);
        endFrame = Math.Clamp(endFrame, firstFrame, totalFrames);

        var selected = new short[(endFrame - firstFrame) * ChannelCount];
        Array.Copy(samples, firstFrame * ChannelCount, selected, 0, selected.Length);
        return new PcmAudio(SampleRate, ChannelCount,
            Start + TimeSpan.FromSeconds((double)firstFrame / SampleRate), selected);
    }
}

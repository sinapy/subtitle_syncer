namespace SubtitleSyncer.Vad;

using SubtitleSyncer.Audio;

/// <summary>Local TEN VAD adapter for mono 16 kHz signed-16-bit PCM audio.</summary>
public sealed class TenVadEngine(TenVadOptions? options = null) : IVadEngine
{
    private const int RequiredSampleRate = 16_000;
    private readonly TenVadOptions options = options ?? new TenVadOptions();

    public IReadOnlyList<VoiceActivity> Detect(PcmAudio audio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        options.Validate();
        if (audio.SampleRate != RequiredSampleRate || audio.ChannelCount != 1)
        {
            throw new ArgumentException("TEN VAD requires mono 16 kHz PCM audio.", nameof(audio));
        }

        TenVadNative.EnsureLoaded();
        var result = TenVadNative.Create(out var handle, (nuint)options.HopSize, options.SpeechThreshold);
        if (result != 0 || handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("TEN VAD could not create a detector.");
        }

        try
        {
            var speechFrames = DetectSpeechFrames(handle, audio, cancellationToken);
            return ToActivities(speechFrames, audio);
        }
        finally
        {
            TenVadNative.Destroy(ref handle);
        }
    }

    private List<bool> DetectSpeechFrames(IntPtr handle, PcmAudio audio, CancellationToken cancellationToken)
    {
        var samples = audio.Samples.Span;
        var frame = new short[options.HopSize];
        var results = new List<bool>((samples.Length + frame.Length - 1) / frame.Length);

        for (var offset = 0; offset < samples.Length; offset += frame.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Array.Clear(frame);
            samples.Slice(offset, Math.Min(frame.Length, samples.Length - offset)).CopyTo(frame);
            var result = TenVadNative.Process(handle, frame, (nuint)frame.Length, out _, out var flag);
            if (result != 0)
            {
                throw new InvalidOperationException("TEN VAD could not process an audio frame.");
            }

            results.Add(flag == 1);
        }

        return results;
    }

    private IReadOnlyList<VoiceActivity> ToActivities(IReadOnlyList<bool> speechFrames, PcmAudio audio)
    {
        var minimumSpeechFrames = ToFrameCount(options.MinimumSpeechDuration);
        var minimumSilenceFrames = ToFrameCount(options.MinimumSilenceDuration);
        var activities = new List<VoiceActivity>();
        var speechStart = -1;
        var silenceStart = -1;

        for (var frame = 0; frame < speechFrames.Count; frame++)
        {
            if (speechFrames[frame])
            {
                speechStart = speechStart < 0 ? frame : speechStart;
                silenceStart = -1;
                continue;
            }

            if (speechStart < 0)
            {
                continue;
            }

            silenceStart = silenceStart < 0 ? frame : silenceStart;
            if (frame - silenceStart + 1 >= minimumSilenceFrames)
            {
                AddIfLongEnough(activities, audio, speechStart, silenceStart, minimumSpeechFrames);
                speechStart = -1;
                silenceStart = -1;
            }
        }

        if (speechStart >= 0)
        {
            AddIfLongEnough(activities, audio, speechStart, speechFrames.Count, minimumSpeechFrames);
        }

        return activities;
    }

    private void AddIfLongEnough(List<VoiceActivity> activities, PcmAudio audio, int firstFrame,
        int exclusiveEndFrame, int minimumSpeechFrames)
    {
        if (exclusiveEndFrame - firstFrame < minimumSpeechFrames)
        {
            return;
        }

        var start = Max(audio.Start, audio.Start + TimeSpan.FromSeconds((double)firstFrame * options.HopSize / RequiredSampleRate) - options.SpeechPadding);
        var end = Min(audio.End, audio.Start + TimeSpan.FromSeconds((double)exclusiveEndFrame * options.HopSize / RequiredSampleRate) + options.SpeechPadding);
        if (activities.Count > 0 && start <= activities[^1].End)
        {
            activities[^1] = activities[^1] with { End = Max(activities[^1].End, end) };
            return;
        }

        activities.Add(new VoiceActivity(start, end));
    }

    private int ToFrameCount(TimeSpan duration) => Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * RequiredSampleRate / options.HopSize));
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}

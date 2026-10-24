namespace SubtitleSyncer.Vad;

using SherpaOnnx;
using SubtitleSyncer.Audio;

/// <summary>Local Silero VAD adapter that returns padded movie-timeline intervals.</summary>
public sealed class SileroVadEngine(SileroVadOptions? options = null) : IVadEngine
{
    private const int RequiredSampleRate = 16_000;
    private const int WindowSize = 512;
    private readonly SileroVadOptions options = options ?? new SileroVadOptions();

    public IReadOnlyList<VoiceActivity> Detect(PcmAudio audio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        options.Validate();
        if (audio.SampleRate != RequiredSampleRate || audio.ChannelCount != 1)
        {
            throw new ArgumentException("Silero VAD requires mono 16 kHz PCM audio.", nameof(audio));
        }

        var modelPath = options.ModelPath ?? Path.Combine(AppContext.BaseDirectory, "models", "silero_vad.onnx");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("The bundled Silero VAD model was not found.", modelPath);
        }

        var config = new VadModelConfig();
        config.SileroVad.Model = modelPath;
        config.SileroVad.Threshold = options.SpeechThreshold;
        config.SileroVad.MinSilenceDuration = (float)options.MinimumSilenceDuration.TotalSeconds;
        config.SileroVad.MinSpeechDuration = (float)options.MinimumSpeechDuration.TotalSeconds;
        config.SileroVad.MaxSpeechDuration = (float)options.MaximumSpeechDuration.TotalSeconds;
        config.SileroVad.WindowSize = WindowSize;
        config.SampleRate = RequiredSampleRate;
        config.NumThreads = options.InferenceThreads;
        config.Provider = "cpu";

        using var detector = new SherpaOnnx.VoiceActivityDetector(config, 30);
        var activities = new List<VoiceActivity>();
        var source = audio.Samples.Span;
        for (var offset = 0; offset < source.Length; offset += WindowSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = new float[WindowSize];
            var length = Math.Min(WindowSize, source.Length - offset);
            for (var index = 0; index < length; index++) frame[index] = source[offset + index] / 32768f;
            detector.AcceptWaveform(frame);
            Drain(detector, audio, activities);
        }

        detector.Flush();
        Drain(detector, audio, activities);
        return PadAndMerge(activities, audio);
    }

    private static void Drain(SherpaOnnx.VoiceActivityDetector detector, PcmAudio audio, ICollection<VoiceActivity> activities)
    {
        while (!detector.IsEmpty())
        {
            var segment = detector.Front();
            var start = audio.Start + TimeSpan.FromSeconds(segment.Start / (double)RequiredSampleRate);
            var end = start + TimeSpan.FromSeconds(segment.Samples.Length / (double)RequiredSampleRate);
            if (end > start) activities.Add(new VoiceActivity(start, end));
            detector.Pop();
        }
    }

    private IReadOnlyList<VoiceActivity> PadAndMerge(IEnumerable<VoiceActivity> detected, PcmAudio audio)
    {
        var padded = new List<VoiceActivity>();
        foreach (var activity in detected.OrderBy(activity => activity.Start))
        {
            var start = Max(audio.Start, activity.Start - options.SpeechPadding);
            var end = Min(audio.End, activity.End + options.SpeechPadding);
            if (end <= start) continue;
            if (padded.Count > 0 && start <= padded[^1].End)
            {
                padded[^1] = padded[^1] with { End = Max(padded[^1].End, end) };
            }
            else
            {
                padded.Add(new VoiceActivity(start, end));
            }
        }
        return padded;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}

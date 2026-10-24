using System.Diagnostics;
using System.Globalization;

namespace SubtitleSyncer.Vad;

using SubtitleSyncer.Audio;

/// <summary>Uses a local FFmpeg executable to decode a bounded movie prefix to mono 16 kHz PCM.</summary>
public sealed class FfmpegAudioPreparation(string ffmpegPath = "ffmpeg") : IAudioPreparation
{
    public PcmAudio Prepare(string moviePath, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moviePath);
        if (!File.Exists(moviePath))
        {
            throw new FileNotFoundException("The movie file was not found.", moviePath);
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(moviePath);
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:a:0");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("16000");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("s16le");
        startInfo.ArgumentList.Add("pipe:1");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("FFmpeg could not be started.");
        using var registration = cancellationToken.Register(() => KillIfRunning(process));
        using var output = new MemoryStream();
        var copyOutput = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
        var readError = process.StandardError.ReadToEndAsync(cancellationToken);

        Task.WhenAll(copyOutput, readError, process.WaitForExitAsync(cancellationToken)).GetAwaiter().GetResult();
        var error = readError.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}: {error.Trim()}");
        }

        var pcmBytes = output.ToArray();
        if (pcmBytes.Length % sizeof(short) != 0)
        {
            throw new InvalidOperationException("FFmpeg returned incomplete signed 16-bit PCM audio.");
        }

        var samples = new short[pcmBytes.Length / sizeof(short)];
        Buffer.BlockCopy(pcmBytes, 0, samples, 0, pcmBytes.Length);

        // FFmpeg's frame-oriented decoder may emit the final partial frame just
        // past -t. Trim it so the VAD input is strictly bounded by the caller's
        // analysis duration.
        var maximumSampleCount = checked((int)Math.Floor(duration.TotalSeconds * 16_000));
        if (samples.Length > maximumSampleCount)
        {
            Array.Resize(ref samples, maximumSampleCount);
        }

        return new PcmAudio(16_000, 1, TimeSpan.Zero, samples);
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between checking and killing it.
        }
    }
}

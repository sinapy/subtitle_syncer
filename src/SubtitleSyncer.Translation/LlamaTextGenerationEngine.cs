using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace SubtitleSyncer.Translation;

/// <summary>CPU-only LLamaSharp adapter for an already downloaded GGUF model.</summary>
internal sealed class LlamaTextGenerationEngine : ITextGenerationEngine
{
    private const string SystemPrompt = "You are a precise Persian-to-English subtitle translator.";

    private readonly LLamaWeights weights;
    private readonly StatelessExecutor executor;
    private readonly InferenceParams inferenceParams;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;

    public LlamaTextGenerationEngine(TranslationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelPath);
        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException("The local translation GGUF model was not found.", options.ModelPath);
        }

        if (options.ContextSize is < 256 or > 32_768)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ContextSize must be between 256 and 32768.");
        }

        if (options.MaxOutputTokens is < 1 or > 2_048)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxOutputTokens must be between 1 and 2048.");
        }

        var modelParameters = new ModelParams(options.ModelPath) { ContextSize = options.ContextSize };
        weights = LLamaWeights.LoadFromFile(modelParameters);
        executor = new StatelessExecutor(weights, modelParameters)
        {
            ApplyTemplate = true,
            SystemMessage = SystemPrompt,
        };
        inferenceParams = new InferenceParams
        {
            MaxTokens = options.MaxOutputTokens,
            SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.0f },
        };
    }

    public async Task<string> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = new StringBuilder();
            await foreach (var token in executor.InferAsync(text, inferenceParams)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                result.Append(token);
            }

            return result.ToString();
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        gate.Dispose();
        weights.Dispose();
    }
}

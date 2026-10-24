using LLama.Native;

namespace SubtitleSyncer.Translation;

/// <summary>Controls process-wide diagnostics emitted by the local translation runtime.</summary>
public static class TranslationRuntime
{
    /// <summary>Suppresses native model diagnostics except errors.</summary>
    /// <remarks>Call before constructing a <see cref="PersianEnglishTranslator"/>.</remarks>
    public static void ShowNativeErrorsOnly(Action<string>? writeError = null)
    {
        NativeLogConfig.llama_log_set((level, message) =>
        {
            if (level >= LLamaLogLevel.Error)
            {
                writeError?.Invoke(message);
            }
        });
    }
}

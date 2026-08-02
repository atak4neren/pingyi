namespace PingYi.Core;

public sealed record LocalLlmPreset(
    string Id,
    string DisplayName,
    string ChatCompletionsEndpoint,
    string SuggestedModel = "")
{
    public override string ToString() => DisplayName;
}

public static class LocalLlmPresets
{
    public static IReadOnlyList<LocalLlmPreset> All { get; } =
    [
        new(
            "llama-cpp",
            "llama.cpp",
            AppSettings.DefaultCustomTranslationEndpoint,
            AppSettings.DefaultCustomTranslationModel),
        new(
            "ollama",
            "Ollama",
            "http://127.0.0.1:11434/v1/chat/completions"),
        new(
            "lm-studio",
            "LM Studio",
            "http://127.0.0.1:1234/v1/chat/completions"),
        new(
            "vllm",
            "vLLM / 其他兼容服务",
            "http://127.0.0.1:8000/v1/chat/completions")
    ];

    public static LocalLlmPreset Default => All[0];

    public static LocalLlmPreset? MatchEndpoint(string? endpoint)
    {
        var normalized = AppSettings.NormalizeChatCompletionsEndpoint(endpoint);
        return All.FirstOrDefault(preset =>
            string.Equals(preset.ChatCompletionsEndpoint, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

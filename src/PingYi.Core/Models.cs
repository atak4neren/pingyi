namespace PingYi.Core;

public enum ProviderExecutionLocation
{
    Local,
    Cloud,
    Configurable
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public sealed record ImageFrame(
    byte[] PngBytes,
    int Width,
    int Height,
    PixelRect DesktopBounds);

public sealed record OcrBlock(
    string Text,
    PixelRect Bounds,
    double Confidence);

public sealed record OcrOptions(
    string SourceLanguage = "auto",
    bool PreserveLayout = true);

public sealed record OcrResult(
    IReadOnlyList<OcrBlock> Blocks,
    string PlainText,
    string DetectedLanguage);

public sealed record TranslationRequest(
    string Text,
    string SourceLanguage,
    string TargetLanguage);

public sealed record TranslationResult(
    string Text,
    string SourceLanguage,
    string TargetLanguage);

public sealed record ProviderMetadata(
    string Id,
    string DisplayName,
    ProviderExecutionLocation Location,
    bool UploadsImage,
    bool RequiresSecret,
    IReadOnlyList<string> SupportedLanguages);

public sealed record ProviderAvailability(bool IsAvailable, string? Message = null)
{
    public static ProviderAvailability Available { get; } = new(true);
}

public sealed class ProviderException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

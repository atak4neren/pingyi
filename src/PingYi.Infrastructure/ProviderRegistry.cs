using PingYi.Core;

namespace PingYi.Infrastructure;

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IOcrProvider> _ocrProviders;
    private readonly Dictionary<string, ITranslationProvider> _translationProviders;

    public ProviderRegistry(IEnumerable<IOcrProvider> ocrProviders, IEnumerable<ITranslationProvider> translationProviders)
    {
        _ocrProviders = ocrProviders.ToDictionary(provider => provider.Metadata.Id, StringComparer.Ordinal);
        _translationProviders = translationProviders.ToDictionary(provider => provider.Metadata.Id, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IOcrProvider> OcrProviders => _ocrProviders.Values;
    public IReadOnlyCollection<ITranslationProvider> TranslationProviders => _translationProviders.Values;

    public IOcrProvider GetOcrProvider(string id) =>
        _ocrProviders.TryGetValue(id, out var provider)
            ? provider
            : _ocrProviders["local-paddle"];

    public ITranslationProvider GetTranslationProvider(string id) =>
        _translationProviders.TryGetValue(id, out var provider)
            ? provider
            : _translationProviders["local-argos"];
}

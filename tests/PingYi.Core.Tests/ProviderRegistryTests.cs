using PingYi.Core;
using PingYi.Infrastructure;

namespace PingYi.Core.Tests;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void Registry_SwitchesProvidersByStableId()
    {
        var localOcr = new StubOcrProvider("local-paddle");
        var cloudOcr = new StubOcrProvider("cloud");
        var localTranslation = new StubTranslationProvider("local-argos");
        var cloudTranslation = new StubTranslationProvider("cloud");
        var registry = new ProviderRegistry(
            [localOcr, cloudOcr],
            [localTranslation, cloudTranslation]);

        Assert.Same(cloudOcr, registry.GetOcrProvider("cloud"));
        Assert.Same(localTranslation, registry.GetTranslationProvider("local-argos"));
        Assert.Same(localOcr, registry.GetOcrProvider("missing"));
    }

    private sealed class StubOcrProvider(string id) : IOcrProvider
    {
        public ProviderMetadata Metadata { get; } = new(
            id, id, ProviderExecutionLocation.Local, false, false, ["zh", "en"]);

        public ValueTask<ProviderAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ProviderAvailability.Available);

        public Task<OcrResult> RecognizeAsync(
            ImageFrame image,
            OcrOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OcrResult([], string.Empty, "unknown"));
    }

    private sealed class StubTranslationProvider(string id) : ITranslationProvider
    {
        public ProviderMetadata Metadata { get; } = new(
            id, id, ProviderExecutionLocation.Local, false, false, ["zh", "en"]);

        public ValueTask<ProviderAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ProviderAvailability.Available);

        public Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TranslationResult(request.Text, request.SourceLanguage, request.TargetLanguage));
    }
}

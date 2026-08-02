using PingYi.Infrastructure;

namespace PingYi.Core.Tests;

public sealed class BaiduProviderIntegrationTests
{
    [Fact]
    [Trait("Category", "BaiduCredentials")]
    public async Task StoredTranslationCredentials_PassRealBaiduValidationWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PINGYI_RUN_BAIDU_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var secretStore = new PlatformSecretStore(new AppDataPaths());
        var provider = new BaiduTranslationProvider(httpClient, secretStore);

        var availability = await provider.GetAvailabilityAsync();
        Assert.True(availability.IsAvailable, availability.Message);
        await provider.ValidateCredentialsAsync();
    }
}

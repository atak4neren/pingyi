using PingYi.Core;

namespace PingYi.Core.Tests;

public sealed class SecretDisplayTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("a", "**")]
    [InlineData("ab", "**")]
    [InlineData("abcdef", "a****f")]
    [InlineData("1234567890abcdef", "1234********cdef")]
    public void Mask_PreservesOnlyARecognizablePrefixAndSuffix(string? value, string expected)
    {
        Assert.Equal(expected, SecretDisplay.Mask(value));
    }

    [Fact]
    public void Mask_DoesNotContainTheCompleteSecret()
    {
        const string secret = "sensitive-api-key-value";

        var masked = SecretDisplay.Mask(secret);

        Assert.DoesNotContain(secret, masked, StringComparison.Ordinal);
        Assert.Contains('*', masked);
    }
}

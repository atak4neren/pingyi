namespace PingYi.Core;

public static class SecretDisplay
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= 2)
        {
            return "**";
        }

        if (value.Length <= 8)
        {
            return value[..1] + "****" + value[^1..];
        }

        return value[..4] + "********" + value[^4..];
    }
}

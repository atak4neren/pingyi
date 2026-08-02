using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;
using PingYi.Core;

namespace PingYi.Infrastructure;

public sealed class PlatformSecretStore : ISecretStore
{
    private ISecretStore _inner;
    private readonly MemorySecretStore _memoryFallback = new();

    public PlatformSecretStore(AppDataPaths paths)
    {
        _inner = OperatingSystem.IsWindows()
            ? new WindowsDpapiSecretStore(paths)
            : LinuxSecretServiceStore.IsAvailable()
                ? new LinuxSecretServiceStore()
                : new MemorySecretStore();
    }

    public bool IsPersistent => _inner is not MemorySecretStore;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _inner.GetAsync(key, cancellationToken);
        }
        catch when (!OperatingSystem.IsWindows() && _inner is LinuxSecretServiceStore)
        {
            _inner = _memoryFallback;
            return await _inner.GetAsync(key, cancellationToken);
        }
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.SetAsync(key, value, cancellationToken);
        }
        catch when (!OperatingSystem.IsWindows() && _inner is LinuxSecretServiceStore)
        {
            _inner = _memoryFallback;
            await _inner.SetAsync(key, value, cancellationToken);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.DeleteAsync(key, cancellationToken);
        }
        catch when (!OperatingSystem.IsWindows() && _inner is LinuxSecretServiceStore)
        {
            _inner = _memoryFallback;
            await _inner.DeleteAsync(key, cancellationToken);
        }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsDpapiSecretStore(AppDataPaths paths) : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PingYi.SecretStore.v1");

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        var encrypted = await File.ReadAllBytesAsync(path, cancellationToken);
        var clear = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(clear);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.SecretsDirectory);
        var clear = Encoding.UTF8.GetBytes(value);
        var encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(GetPath(key), encrypted, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string key)
    {
        var safeName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(paths.SecretsDirectory, safeName + ".bin");
    }
}

internal sealed class LinuxSecretServiceStore : ISecretStore
{
    public static bool IsAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("secret-tool", "--help")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync($"lookup app pingyi key {EscapeArgument(key)}", null, cancellationToken);
        return result.ExitCode == 0 ? result.Output.TrimEnd('\r', '\n') : null;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            $"store --label=PingYi app pingyi key {EscapeArgument(key)}",
            value,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("无法写入 Linux Secret Service。");
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await RunAsync($"clear app pingyi key {EscapeArgument(key)}", null, cancellationToken);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string arguments,
        string? input,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo("secret-tool", arguments)
        {
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动 secret-tool。");

        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output);
    }

    private static string EscapeArgument(string value) =>
        '"' + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
}

internal sealed class MemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.GetValueOrDefault(key));

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}

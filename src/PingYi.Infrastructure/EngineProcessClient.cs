using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PingYi.Infrastructure;

public sealed class EngineProcessClient(AppDataPaths paths) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private int _nextId;

    public async Task<JsonElement> CallAsync(
        string method,
        JsonObject? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureStarted();
            var id = Interlocked.Increment(ref _nextId);
            var request = new JsonObject
            {
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters ?? new JsonObject()
            }.ToJsonString();
            await _process!.StandardInput.WriteLineAsync(request.AsMemory(), cancellationToken);
            await _process.StandardInput.FlushAsync(cancellationToken);

            while (true)
            {
                var line = await _process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    var detail = _process.HasExited ? $"退出码 {_process.ExitCode}" : "输出已关闭";
                    ResetProcess();
                    throw new InvalidOperationException($"本地引擎意外停止：{detail}。");
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (document)
                {
                    var root = document.RootElement;
                    if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id)
                    {
                        continue;
                    }

                    if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                    {
                        var code = error.TryGetProperty("code", out var codeElement)
                            ? codeElement.GetString() ?? "engine_error"
                            : "engine_error";
                        var message = error.TryGetProperty("message", out var messageElement)
                            ? messageElement.GetString() ?? "本地引擎调用失败。"
                            : "本地引擎调用失败。";
                        throw new Core.ProviderException(code, message);
                    }

                    return root.GetProperty("result").Clone();
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureStarted()
    {
        if (_process is { HasExited: false })
        {
            return;
        }

        var launch = ResolveLaunchCommand();
        var startInfo = new ProcessStartInfo(launch.FileName, launch.Arguments)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["PINGYI_MODEL_DIR"] = paths.ModelDirectory;
        startInfo.Environment["PINGYI_BUNDLED_MODEL_DIR"] = paths.BundledModelDirectory;
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动屏译本地引擎。");
        _ = DrainStandardErrorAsync(_process);
    }

    private (string FileName, string Arguments) ResolveLaunchCommand()
    {
        var configured = Environment.GetEnvironmentVariable("PINGYI_ENGINE_HOST");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return (configured, string.Empty);
        }

        var executableName = OperatingSystem.IsWindows() ? "pingyi-engine.exe" : "pingyi-engine";
        var packaged = Path.Combine(AppContext.BaseDirectory, "engine-host", executableName);
        if (File.Exists(packaged))
        {
            return (packaged, string.Empty);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var script = Path.Combine(directory.FullName, "engine_host", "main.py");
            if (File.Exists(script))
            {
                var escaped = '"' + script.Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
                var configuredPython = Environment.GetEnvironmentVariable("PINGYI_ENGINE_PYTHON");
                if (!string.IsNullOrWhiteSpace(configuredPython) && File.Exists(configuredPython))
                {
                    return (configuredPython, escaped);
                }

                var root = directory.FullName;
                var virtualEnvironmentPython = OperatingSystem.IsWindows()
                    ? Path.Combine(root, ".venv-engine", "Scripts", "python.exe")
                    : Path.Combine(root, ".venv-engine", "bin", "python");
                if (File.Exists(virtualEnvironmentPython))
                {
                    return (virtualEnvironmentPython, escaped);
                }

                return OperatingSystem.IsWindows()
                    ? ("py", $"-3 {escaped}")
                    : ("python3", escaped);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "未找到本地引擎。开发环境请保留 engine_host/main.py，发布环境请放置 engine-host/pingyi-engine。");
    }

    private static async Task DrainStandardErrorAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync() is not null)
            {
                // Intentionally discard engine diagnostics: OCR text and credentials must never enter app logs.
            }
        }
        catch
        {
            // Process shutdown races are harmless here.
        }
    }

    private void ResetProcess()
    {
        _process?.Dispose();
        _process = null;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    await _process.StandardInput.WriteLineAsync("{\"id\":0,\"method\":\"shutdown\",\"params\":{}}");
                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    _process.Kill(true);
                }
            }

            ResetProcess();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}

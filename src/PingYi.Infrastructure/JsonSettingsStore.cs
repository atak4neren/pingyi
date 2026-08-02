using System.Text.Json;
using PingYi.Core;

namespace PingYi.Infrastructure;

public sealed class JsonSettingsStore(AppDataPaths paths) : ISettingsStore
{
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(paths.SettingsFile);
            var settings = await JsonSerializer.DeserializeAsync(
                stream,
                PingYiJsonContext.Default.AppSettings,
                cancellationToken);
            return (settings ?? new AppSettings()).Normalize();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ConfigDirectory);
        var temporaryPath = paths.SettingsFile + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings.Normalize(),
                PingYiJsonContext.Default.AppSettings,
                cancellationToken);
        }

        File.Move(temporaryPath, paths.SettingsFile, true);
    }
}

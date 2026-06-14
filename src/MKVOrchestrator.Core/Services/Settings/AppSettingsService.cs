using System.Text.Json;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string SettingsDirectory { get; } = CrossPlatformRuntime.AppDataDirectory;

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return Migrate(settings);
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        settings.SettingsSchemaVersion = AppSettings.CurrentSettingsSchemaVersion;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings Migrate(AppSettings settings)
    {
        if (settings.SettingsSchemaVersion <= 0)
        {
            // Legacy settings existed before schema versioning. Existing properties are still
            // deserialized by name, so the first migration only stamps the current version.
            settings.SettingsSchemaVersion = AppSettings.CurrentSettingsSchemaVersion;
        }

        if (settings.SettingsSchemaVersion < AppSettings.CurrentSettingsSchemaVersion)
        {
            // Future migrations should be added here in ascending version order.
            settings.SettingsSchemaVersion = AppSettings.CurrentSettingsSchemaVersion;
        }

        return settings;
    }
}

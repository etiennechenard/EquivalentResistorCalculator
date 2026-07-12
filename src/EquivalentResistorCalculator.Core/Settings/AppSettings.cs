using System.Text.Json;
using System.Text.Json.Serialization;
using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Core.Settings;

public sealed class AppSettings
{
    public int MaxDepth { get; set; } = 3;
    public PackageType? PackageFilter { get; set; }
    public string? StockFile { get; set; }

    public static AppSettings Defaults => new();

    public static AppSettings Load(string path)
    {
        AppSettings? settings;
        try
        {
            string json = File.ReadAllText(path);
            settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            settings = null;
        }

        if (settings is null)
        {
            var defaults = Defaults;
            defaults.Save(path);
            return defaults;
        }

        settings.MaxDepth = Math.Clamp(settings.MaxDepth, 1, 20);
        return settings;
    }

    public void Save(string path)
    {
        string json = JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings);
        File.WriteAllText(path, json);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

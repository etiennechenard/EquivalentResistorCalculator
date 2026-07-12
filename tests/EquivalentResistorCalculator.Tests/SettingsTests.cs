using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Settings;

namespace EquivalentResistorCalculator.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly string _settingsPath;

    public SettingsTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "EquivalentResistorCalculatorTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempFolder);
        _settingsPath = Path.Combine(_tempFolder, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, recursive: true);
    }

    [Fact]
    public void Defaults_AreCanonical()
    {
        var defaults = AppSettings.Defaults;

        Assert.Equal(3, defaults.MaxDepth);
        Assert.Null(defaults.PackageFilter);
        Assert.Null(defaults.StockFile);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultsAndWritesFile()
    {
        Assert.False(File.Exists(_settingsPath));

        var settings = AppSettings.Load(_settingsPath);

        Assert.Equal(3, settings.MaxDepth);
        Assert.Null(settings.PackageFilter);
        Assert.Null(settings.StockFile);
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsAndRewritesFile()
    {
        File.WriteAllText(_settingsPath, "{ this is not valid json");

        var settings = AppSettings.Load(_settingsPath);

        Assert.Equal(3, settings.MaxDepth);
        Assert.Null(settings.PackageFilter);
        Assert.Null(settings.StockFile);

        string rewritten = File.ReadAllText(_settingsPath);
        Assert.Contains("\"MaxDepth\": 3", rewritten);
    }

    [Fact]
    public void Load_MaxDepthAboveRange_ClampsTo20WithoutResettingOtherValues()
    {
        File.WriteAllText(_settingsPath, """{"MaxDepth":50,"PackageFilter":"SMD","StockFile":"bench.csv"}""");

        var settings = AppSettings.Load(_settingsPath);

        Assert.Equal(20, settings.MaxDepth);
        Assert.Equal(PackageType.SMD, settings.PackageFilter);
        Assert.Equal("bench.csv", settings.StockFile);
    }

    [Fact]
    public void Load_MaxDepthBelowRange_ClampsTo1()
    {
        File.WriteAllText(_settingsPath, """{"MaxDepth":0,"PackageFilter":null,"StockFile":null}""");

        var settings = AppSettings.Load(_settingsPath);

        Assert.Equal(1, settings.MaxDepth);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsPersistedValues()
    {
        var settings = new AppSettings
        {
            MaxDepth = 7,
            PackageFilter = PackageType.ThroughHole,
            StockFile = "custom.csv",
        };

        settings.Save(_settingsPath);
        var reloaded = AppSettings.Load(_settingsPath);

        Assert.Equal(7, reloaded.MaxDepth);
        Assert.Equal(PackageType.ThroughHole, reloaded.PackageFilter);
        Assert.Equal("custom.csv", reloaded.StockFile);
    }
}

using EquivalentResistorCalculator.Core.Settings;

namespace EquivalentResistorCalculator.Gui.Settings;

internal sealed class SettingsService
{
    private readonly string _path;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EquivalentResistorCalculator");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Current = AppSettings.Load(_path);
    }

    public void Save() => Current.Save(_path);
}

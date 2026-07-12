using System.Globalization;

namespace EquivalentResistorCalculator.Core.Stock;

public sealed class StockRepository
{
    private static readonly (double Value, string Label)[] DefaultSeed =
    {
        (10, "10"),
        (22, "22"),
        (47, "47"),
        (100, "100"),
        (220, "220"),
        (330, "330"),
        (470, "470"),
        (680, "680"),
        (1_000, "1K"),
        (1_500, "1.5K"),
        (2_200, "2.2K"),
        (3_300, "3.3K"),
        (4_700, "4.7K"),
        (6_800, "6.8K"),
        (10_000, "10K"),
        (15_000, "15K"),
        (22_000, "22K"),
        (33_000, "33K"),
        (47_000, "47K"),
        (68_000, "68K"),
        (100_000, "100K"),
        (150_000, "150K"),
        (220_000, "220K"),
        (330_000, "330K"),
        (470_000, "470K"),
        (680_000, "680K"),
        (1_000_000, "1M"),
        (2_200_000, "2.2M"),
        (4_700_000, "4.7M"),
        (10_000_000, "10M"),
    };

    private readonly string _stocksFolder;

    public StockRepository(string stocksFolder)
    {
        _stocksFolder = stocksFolder;
        Directory.CreateDirectory(_stocksFolder);
    }

    public string StocksFolder => _stocksFolder;

    public string? SelectedFileName { get; private set; }

    public IReadOnlyList<string> EnumerateStockFiles()
    {
        SeedIfEmpty();

        return Directory.EnumerateFiles(_stocksFolder, "*.csv", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string Select(string? requestedFileName)
    {
        var files = EnumerateStockFiles();

        string selected = requestedFileName is not null
            && files.Any(f => string.Equals(f, requestedFileName, StringComparison.OrdinalIgnoreCase))
            ? files.First(f => string.Equals(f, requestedFileName, StringComparison.OrdinalIgnoreCase))
            : files[0];

        SelectedFileName = selected;
        return selected;
    }

    private void SeedIfEmpty()
    {
        bool anyCsv = Directory.EnumerateFiles(_stocksFolder, "*.csv", SearchOption.TopDirectoryOnly).Any();
        if (anyCsv)
            return;

        string path = Path.Combine(_stocksFolder, "default.csv");
        using var writer = new StreamWriter(path);
        writer.WriteLine("Value,Label,Package");
        foreach (var (value, label) in DefaultSeed)
        {
            string formattedValue = value.ToString(CultureInfo.InvariantCulture);
            writer.WriteLine($"{formattedValue},{label},ThroughHole");
            writer.WriteLine($"{formattedValue},{label},SMD");
        }
    }
}

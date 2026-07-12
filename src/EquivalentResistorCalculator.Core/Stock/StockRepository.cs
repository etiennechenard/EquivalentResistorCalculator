using System.Globalization;
using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Parsing;

namespace EquivalentResistorCalculator.Core.Stock;

public sealed record StockLoadResult(
    string FileName,
    IReadOnlyList<Resistor> Resistors,
    int SkippedRowCount);

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
    private HashSet<string> _lastKnownFileNames = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastLoadedFileName;
    private DateTime? _lastLoadedWriteTimeUtc;

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

        var names = Directory.EnumerateFiles(_stocksFolder, "*.csv", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _lastKnownFileNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

        return names;
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

    public StockLoadResult Load(string fileName)
    {
        SeedIfEmpty();

        var existingNames = Directory.EnumerateFiles(_stocksFolder, "*.csv", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!existingNames.Any(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)))
        {
            fileName = existingNames[0];
        }

        SelectedFileName = fileName;
        string path = Path.Combine(_stocksFolder, fileName);
        var resistors = new List<Resistor>();
        int skipped = 0;

        foreach (string line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] columns = line.Split(',');
            if (columns.Length != 3)
            {
                skipped++;
                continue;
            }

            if (!double.TryParse(columns[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                skipped++;
                continue;
            }

            if (!Enum.TryParse(columns[2], ignoreCase: false, out PackageType package)
                || !Enum.IsDefined(package))
            {
                skipped++;
                continue;
            }

            string label = string.IsNullOrEmpty(columns[1]) ? ResistanceParser.Format(value) : columns[1];
            resistors.Add(new Resistor(value, label, package));
        }

        _lastLoadedFileName = fileName;
        _lastLoadedWriteTimeUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTime?)null;

        return new StockLoadResult(fileName, resistors, skipped);
    }

    public static IReadOnlyList<Resistor> FilterByPackage(IReadOnlyList<Resistor> resistors, PackageType? packageFilter)
        => packageFilter is null
            ? resistors
            : resistors.Where(r => r.Package == packageFilter.Value).ToList();

    public bool HasSelectedFileContentChanged()
    {
        if (_lastLoadedFileName is null)
            return false;

        string path = Path.Combine(_stocksFolder, _lastLoadedFileName);
        DateTime? currentWriteTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTime?)null;
        return currentWriteTime != _lastLoadedWriteTimeUtc;
    }

    public bool HasFolderListingChanged()
    {
        var currentNames = Directory.EnumerateFiles(_stocksFolder, "*.csv", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return !currentNames.SetEquals(_lastKnownFileNames);
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

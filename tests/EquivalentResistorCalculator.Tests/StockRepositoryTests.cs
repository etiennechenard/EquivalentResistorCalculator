using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Stock;

namespace EquivalentResistorCalculator.Tests;

public class StockRepositoryTests : IDisposable
{
    private readonly string _tempFolder;

    public StockRepositoryTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "EquivalentResistorCalculatorTests_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, recursive: true);
    }

    [Fact]
    public void Constructor_CreatesStocksFolderIfMissing()
    {
        Assert.False(Directory.Exists(_tempFolder));

        _ = new StockRepository(_tempFolder);

        Assert.True(Directory.Exists(_tempFolder));
    }

    [Fact]
    public void EnumerateStockFiles_SeedsExact60RowDefaultWhenFolderEmpty()
    {
        var repo = new StockRepository(_tempFolder);

        var files = repo.EnumerateStockFiles();

        Assert.Equal(new[] { "default.csv" }, files);

        string[] lines = File.ReadAllLines(Path.Combine(_tempFolder, "default.csv"));
        Assert.Equal("Value,Label,Package", lines[0]);
        Assert.Equal(61, lines.Length); // header + 60 rows

        Assert.Equal("10,10,ThroughHole", lines[1]);
        Assert.Equal("10,10,SMD", lines[2]);
        Assert.Equal("10000000,10M,ThroughHole", lines[59]);
        Assert.Equal("10000000,10M,SMD", lines[60]);
    }

    [Fact]
    public void EnumerateStockFiles_ReseedsAfterAllFilesDeleted()
    {
        var repo = new StockRepository(_tempFolder);
        repo.EnumerateStockFiles();
        File.Delete(Path.Combine(_tempFolder, "default.csv"));

        var files = repo.EnumerateStockFiles();

        Assert.Equal(new[] { "default.csv" }, files);
        Assert.Equal(61, File.ReadAllLines(Path.Combine(_tempFolder, "default.csv")).Length);
    }

    [Fact]
    public void EnumerateStockFiles_DoesNotReseedWhenAnyCsvPresent()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllText(Path.Combine(_tempFolder, "custom.csv"), "Value,Label,Package\n");

        var repo = new StockRepository(_tempFolder);
        var files = repo.EnumerateStockFiles();

        Assert.Equal(new[] { "custom.csv" }, files);
        Assert.False(File.Exists(Path.Combine(_tempFolder, "default.csv")));
    }

    [Fact]
    public void EnumerateStockFiles_SortedCaseInsensitivelyByFilename()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllText(Path.Combine(_tempFolder, "Banana.csv"), "Value,Label,Package\n");
        File.WriteAllText(Path.Combine(_tempFolder, "apple.csv"), "Value,Label,Package\n");
        File.WriteAllText(Path.Combine(_tempFolder, "Cherry.csv"), "Value,Label,Package\n");

        var repo = new StockRepository(_tempFolder);
        var files = repo.EnumerateStockFiles();

        Assert.Equal(new[] { "apple.csv", "Banana.csv", "Cherry.csv" }, files);
    }

    [Fact]
    public void Select_PersistedFileExists_ReturnsIt()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllText(Path.Combine(_tempFolder, "bench.csv"), "Value,Label,Package\n");
        File.WriteAllText(Path.Combine(_tempFolder, "other.csv"), "Value,Label,Package\n");

        var repo = new StockRepository(_tempFolder);
        string selected = repo.Select("bench.csv");

        Assert.Equal("bench.csv", selected);
        Assert.Equal("bench.csv", repo.SelectedFileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("does-not-exist.csv")]
    public void Select_MissingOrNullOrNonexistent_FallsBackToFirstAlphabetically(string? requested)
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllText(Path.Combine(_tempFolder, "zeta.csv"), "Value,Label,Package\n");
        File.WriteAllText(Path.Combine(_tempFolder, "alpha.csv"), "Value,Label,Package\n");

        var repo = new StockRepository(_tempFolder);
        string selected = repo.Select(requested);

        Assert.Equal("alpha.csv", selected);
    }

    [Fact]
    public void Select_SelectedFileDisappears_FallbackReappliesOnNextSelect()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllText(Path.Combine(_tempFolder, "alpha.csv"), "Value,Label,Package\n");
        File.WriteAllText(Path.Combine(_tempFolder, "bravo.csv"), "Value,Label,Package\n");

        var repo = new StockRepository(_tempFolder);
        string firstSelection = repo.Select("bravo.csv");
        Assert.Equal("bravo.csv", firstSelection);

        File.Delete(Path.Combine(_tempFolder, "bravo.csv"));
        string secondSelection = repo.Select(firstSelection);

        Assert.Equal("alpha.csv", secondSelection);
    }

    [Fact]
    public void Load_ParsesValidRows()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllLines(Path.Combine(_tempFolder, "bench.csv"), new[]
        {
            "Value,Label,Package",
            "10000,10K,ThroughHole",
            "220,220,SMD",
        });

        var repo = new StockRepository(_tempFolder);
        var result = repo.Load("bench.csv");

        Assert.Equal("bench.csv", result.FileName);
        Assert.Equal(0, result.SkippedRowCount);
        Assert.Equal(2, result.Resistors.Count);
        Assert.Equal(new Resistor(10_000, "10K", PackageType.ThroughHole), result.Resistors[0]);
        Assert.Equal(new Resistor(220, "220", PackageType.SMD), result.Resistors[1]);
    }

    [Fact]
    public void Load_SkipsMalformedRowsAndCountsThem()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllLines(Path.Combine(_tempFolder, "bench.csv"), new[]
        {
            "Value,Label,Package",
            "10000,10K,ThroughHole",        // valid
            "not-a-number,Bad,ThroughHole", // unparseable value
            "220,220,NotAPackage",          // unparseable package
            "330,three,thirty,ThroughHole", // wrong column count (label with comma)
            "470,470",                      // wrong column count
        });

        var repo = new StockRepository(_tempFolder);
        var result = repo.Load("bench.csv");

        Assert.Single(result.Resistors);
        Assert.Equal(4, result.SkippedRowCount);
    }

    [Fact]
    public void Load_EmptyLabelFallsBackToFormattedValue()
    {
        Directory.CreateDirectory(_tempFolder);
        File.WriteAllLines(Path.Combine(_tempFolder, "bench.csv"), new[]
        {
            "Value,Label,Package",
            "10200,,ThroughHole",
        });

        var repo = new StockRepository(_tempFolder);
        var result = repo.Load("bench.csv");

        Assert.Equal("10.2K", result.Resistors[0].Label);
    }

    [Fact]
    public void FilterByPackage_NullReturnsAllResistorsUnfiltered()
    {
        var resistors = new List<Resistor>
        {
            new(10, "10", PackageType.ThroughHole),
            new(20, "20", PackageType.SMD),
        };

        var filtered = StockRepository.FilterByPackage(resistors, null);

        Assert.Equal(resistors, filtered);
    }

    [Theory]
    [InlineData(PackageType.ThroughHole)]
    [InlineData(PackageType.SMD)]
    public void FilterByPackage_FiltersToRequestedPackageOnly(PackageType filter)
    {
        var resistors = new List<Resistor>
        {
            new(10, "10", PackageType.ThroughHole),
            new(20, "20", PackageType.SMD),
        };

        var filtered = StockRepository.FilterByPackage(resistors, filter);

        Assert.All(filtered, r => Assert.Equal(filter, r.Package));
        Assert.Single(filtered);
    }
}

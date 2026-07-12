using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Search;

namespace EquivalentResistorCalculator.Tests;

public class CombinationFinderTests
{
    [Fact]
    public void SeriesAndParallel_ComputeCorrectTotalsAndDescriptions()
    {
        var stock = new List<Resistor>
        {
            new(10_000, "10K", PackageType.ThroughHole),
            new(220, "220", PackageType.ThroughHole),
        };

        var results = CombinationFinder.Find(stock, targetOhms: 10_220, maxDepth: 2);

        var series = results.Single(r => r.Description == "(10K + 220)");
        Assert.Equal(10_220, series.TotalResistance, precision: 6);
        Assert.Equal(0, series.ErrorPercent, precision: 6);
        Assert.Equal(2, series.Depth);

        var parallel = results.Single(r => r.Description == "(10K || 220)");
        double expectedParallel = 10_000 * 220.0 / (10_000 + 220.0);
        Assert.Equal(expectedParallel, parallel.TotalResistance, precision: 6);
        Assert.Equal(2, parallel.Depth);
    }

    [Fact]
    public void Dedupe_CollapsesIdenticalDescriptionsToOneResult()
    {
        var stock = new List<Resistor>
        {
            new(1_000, "A", PackageType.ThroughHole),
            new(1_000, "A", PackageType.ThroughHole),
        };

        var results = CombinationFinder.Find(stock, targetOhms: 1_000, maxDepth: 1);

        Assert.Single(results);
        Assert.Equal("A", results[0].Description);
    }

    [Fact]
    public void Results_OrderedByAbsoluteErrorAscending()
    {
        var stock = new List<Resistor>
        {
            new(1_500, "C", PackageType.ThroughHole),
            new(1_000, "A", PackageType.ThroughHole),
            new(1_100, "B", PackageType.ThroughHole),
        };

        var results = CombinationFinder.Find(stock, targetOhms: 1_000, maxDepth: 1);

        Assert.Equal(new[] { "A", "B", "C" }, results.Select(r => r.Description));
    }

    [Fact]
    public void Results_TruncatedToTop20()
    {
        var stock = Enumerable.Range(1, 30)
            .Select(i => new Resistor(1_000 + i, $"R{i}", PackageType.ThroughHole))
            .ToList();

        var results = CombinationFinder.Find(stock, targetOhms: 1_000, maxDepth: 1);

        Assert.Equal(20, results.Count);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => $"R{i}"), results.Select(r => r.Description));
    }

    [Fact]
    public void Level1_BypassesThe500PercentPrune()
    {
        var stock = new List<Resistor> { new(1_000_000, "Far", PackageType.ThroughHole) };

        var results = CombinationFinder.Find(stock, targetOhms: 1, maxDepth: 1);

        var result = Assert.Single(results);
        Assert.Equal("Far", result.Description);
        Assert.True(Math.Abs(result.ErrorPercent) > 500);
    }

    [Fact]
    public void DeeperLevels_ExcludedWhenErrorExceedsThe500PercentPrune()
    {
        var stock = new List<Resistor> { new(1_000_000, "Far", PackageType.ThroughHole) };

        var results = CombinationFinder.Find(stock, targetOhms: 1, maxDepth: 2);

        var result = Assert.Single(results);
        Assert.Equal("Far", result.Description);
    }

    [Fact]
    public void BeamLimit_KeepsSearchBoundedWithLargeStock()
    {
        var stock = Enumerable.Range(1, 600)
            .Select(i => new Resistor(i, $"R{i}", PackageType.ThroughHole))
            .ToList();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = CombinationFinder.Find(stock, targetOhms: 1_000, maxDepth: 3);
        stopwatch.Stop();

        Assert.True(results.Count <= 20);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"Search took too long ({stopwatch.Elapsed}) — beam limit may not be capping candidates per level.");
    }
}

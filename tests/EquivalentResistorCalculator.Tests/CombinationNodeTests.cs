using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Tests;

public class CombinationNodeTests
{
    [Fact]
    public void Leaf_DescriptionIsLabel()
    {
        var resistor = new Resistor(10_000, "10K", PackageType.ThroughHole);

        var node = CombinationNode.Leaf(resistor);

        Assert.Equal("10K", node.Description);
    }

    [Fact]
    public void Leaf_EmptyLabelFallsBackToFormattedValue()
    {
        var resistor = new Resistor(10_200, "", PackageType.ThroughHole);

        var node = CombinationNode.Leaf(resistor);

        Assert.Equal("10.2K", node.Description);
    }

    [Fact]
    public void Series_WrapsPreviousDescriptionWithPlus()
    {
        var node = CombinationNode.Leaf(new Resistor(10_000, "10K", PackageType.ThroughHole));
        node = CombinationNode.Series(node, new Resistor(220, "220", PackageType.ThroughHole));

        Assert.Equal("(10K + 220)", node.Description);
    }

    [Fact]
    public void Parallel_WrapsPreviousDescriptionWithDoublePipe()
    {
        var node = CombinationNode.Leaf(new Resistor(22_000, "22K", PackageType.ThroughHole));
        node = CombinationNode.Parallel(node, new Resistor(20_000, "20K", PackageType.ThroughHole));

        Assert.Equal("(22K || 20K)", node.Description);
    }

    [Fact]
    public void NestedSeriesAndParallel_MatchesExpectedFormat()
    {
        var node = CombinationNode.Leaf(new Resistor(22_000, "22K", PackageType.ThroughHole));
        node = CombinationNode.Parallel(node, new Resistor(20_000, "20K", PackageType.ThroughHole));
        node = CombinationNode.Series(node, new Resistor(100, "100", PackageType.ThroughHole));

        Assert.Equal("((22K || 20K) + 100)", node.Description);
    }
}

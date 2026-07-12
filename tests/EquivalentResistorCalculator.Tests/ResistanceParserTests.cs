using EquivalentResistorCalculator.Core.Parsing;

namespace EquivalentResistorCalculator.Tests;

public class ResistanceParserTests
{
    [Theory]
    [InlineData("10k", 10_000)]
    [InlineData("4.7K", 4_700)]
    [InlineData("1meg", 1_000_000)]
    [InlineData("2M", 2_000_000)]
    [InlineData("100R", 100)]
    [InlineData("470", 470)]
    public void TryParse_AcceptsSuffixMatrix(string input, double expectedOhms)
    {
        bool ok = ResistanceParser.TryParse(input, out double ohms);

        Assert.True(ok);
        Assert.Equal(expectedOhms, ohms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("10X")]
    [InlineData("K10")]
    public void TryParse_RejectsInvalidInput(string? input)
    {
        bool ok = ResistanceParser.TryParse(input, out double ohms);

        Assert.False(ok);
        Assert.Equal(0, ohms);
    }

    [Theory]
    [InlineData(470, "470")]
    [InlineData(999, "999")]
    [InlineData(1000, "1K")]
    [InlineData(10_200, "10.2K")]
    [InlineData(999_900, "999.9K")]
    [InlineData(1_000_000, "1M")]
    [InlineData(2_500_000, "2.5M")]
    public void Format_UsesG4WithThresholdSuffixes(double ohms, string expected)
    {
        string actual = ResistanceParser.Format(ohms);

        Assert.Equal(expected, actual);
    }
}

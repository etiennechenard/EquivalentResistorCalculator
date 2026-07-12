using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Core.Search;

public static class CombinationFinder
{
    private const int BeamLimit = 500;
    private const int MaxResults = 20;
    private const double PruneThresholdPercent = 500.0;

    public static IReadOnlyList<CombinationResult> Find(
        IReadOnlyList<Resistor> stock,
        double targetOhms,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        maxDepth = Math.Clamp(maxDepth, 1, 20);

        var allResults = new List<CombinationResult>();
        var seenDescriptions = new HashSet<string>();

        var currentLevel = new List<CombinationResult>();
        foreach (var resistor in stock)
        {
            var node = CombinationNode.Leaf(resistor);
            string description = node.Description;
            var result = new CombinationResult(
                resistor.Value,
                CalculateErrorPercent(resistor.Value, targetOhms),
                description,
                node,
                new List<Resistor> { resistor },
                Depth: 1);

            currentLevel.Add(result);
            if (seenDescriptions.Add(description))
                allResults.Add(result);
        }

        for (int depth = 2; depth <= maxDepth; depth++)
        {
            var nextLevel = new List<CombinationResult>();

            foreach (var prev in currentLevel)
            {
                foreach (var resistor in stock)
                {
                    double seriesTotal = prev.TotalResistance + resistor.Value;
                    double seriesError = CalculateErrorPercent(seriesTotal, targetOhms);
                    if (Math.Abs(seriesError) < PruneThresholdPercent)
                    {
                        var seriesNode = CombinationNode.Series(prev.Tree, resistor);
                        string seriesDescription = seriesNode.Description;
                        if (seenDescriptions.Add(seriesDescription))
                        {
                            var seriesResult = new CombinationResult(
                                seriesTotal,
                                seriesError,
                                seriesDescription,
                                seriesNode,
                                new List<Resistor>(prev.ResistorsUsed) { resistor },
                                depth);

                            nextLevel.Add(seriesResult);
                            allResults.Add(seriesResult);
                        }
                    }

                    double parallelTotal = prev.TotalResistance * resistor.Value / (prev.TotalResistance + resistor.Value);
                    double parallelError = CalculateErrorPercent(parallelTotal, targetOhms);
                    if (Math.Abs(parallelError) < PruneThresholdPercent)
                    {
                        var parallelNode = CombinationNode.Parallel(prev.Tree, resistor);
                        string parallelDescription = parallelNode.Description;
                        if (seenDescriptions.Add(parallelDescription))
                        {
                            var parallelResult = new CombinationResult(
                                parallelTotal,
                                parallelError,
                                parallelDescription,
                                parallelNode,
                                new List<Resistor>(prev.ResistorsUsed) { resistor },
                                depth);

                            nextLevel.Add(parallelResult);
                            allResults.Add(parallelResult);
                        }
                    }
                }
            }

            currentLevel = nextLevel
                .OrderBy(r => Math.Abs(r.ErrorPercent))
                .Take(BeamLimit)
                .ToList();
        }

        return allResults
            .OrderBy(r => Math.Abs(r.ErrorPercent))
            .Take(MaxResults)
            .ToList();
    }

    private static double CalculateErrorPercent(double actual, double target)
    {
        if (target == 0)
            return actual == 0 ? 0 : double.MaxValue;
        return (actual - target) / target * 100.0;
    }
}

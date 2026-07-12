namespace EquivalentResistorCalculator.Core.Models;

public sealed record CombinationResult(
    double TotalResistance,
    double ErrorPercent,
    string Description,
    CombinationNode Tree,
    IReadOnlyList<Resistor> ResistorsUsed,
    int Depth);

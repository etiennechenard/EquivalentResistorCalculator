using EquivalentResistorCalculator.Core.Parsing;

namespace EquivalentResistorCalculator.Core.Models;

public enum CombinationOperation
{
    Series,
    Parallel,
}

public sealed class CombinationNode
{
    public Resistor Resistor { get; }
    public CombinationNode? Previous { get; }
    public CombinationOperation? Operation { get; }

    private CombinationNode(Resistor resistor, CombinationNode? previous, CombinationOperation? operation)
    {
        Resistor = resistor;
        Previous = previous;
        Operation = operation;
    }

    public static CombinationNode Leaf(Resistor resistor) => new(resistor, previous: null, operation: null);

    public static CombinationNode Series(CombinationNode previous, Resistor resistor) =>
        new(resistor, previous, CombinationOperation.Series);

    public static CombinationNode Parallel(CombinationNode previous, Resistor resistor) =>
        new(resistor, previous, CombinationOperation.Parallel);

    public string Description
    {
        get
        {
            string label = string.IsNullOrEmpty(Resistor.Label)
                ? ResistanceParser.Format(Resistor.Value)
                : Resistor.Label;

            if (Previous is null)
                return label;

            return Operation == CombinationOperation.Series
                ? $"({Previous.Description} + {label})"
                : $"({Previous.Description} || {label})";
        }
    }
}

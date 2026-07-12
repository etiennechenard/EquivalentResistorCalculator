using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Core.Search;

public enum SearchStatus
{
    Idle,
    Searching,
    Done,
    NoStock,
}

public sealed record SearchSnapshot(SearchStatus Status, IReadOnlyList<CombinationResult> Results)
{
    public static SearchSnapshot Initial { get; } = new(SearchStatus.Idle, Array.Empty<CombinationResult>());
}

using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Core.Search;

public sealed class SearchCoordinator
{
    private readonly object _lock = new();
    private readonly SemaphoreSlim _searchGate = new(1, 1);
    private SearchSnapshot _snapshot = SearchSnapshot.Initial;

    public SearchSnapshot Snapshot
    {
        get { lock (_lock) return _snapshot; }
    }

    public Task Submit(double targetOhms, int depth, IReadOnlyList<Resistor> stock)
    {
        if (stock.Count == 0)
        {
            lock (_lock)
                _snapshot = new SearchSnapshot(SearchStatus.NoStock, Array.Empty<CombinationResult>());
            return Task.CompletedTask;
        }

        lock (_lock)
            _snapshot = _snapshot with { Status = SearchStatus.Searching };

        return Task.Run(async () =>
        {
            await _searchGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var results = CombinationFinder.Find(stock, targetOhms, depth);
                lock (_lock)
                    _snapshot = new SearchSnapshot(SearchStatus.Done, results);
            }
            finally
            {
                _searchGate.Release();
            }
        });
    }
}

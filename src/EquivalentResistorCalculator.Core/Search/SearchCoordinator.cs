using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Core.Search;

public sealed class SearchCoordinator
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _lock = new();
    private readonly SemaphoreSlim _searchGate = new(1, 1);
    private readonly ISearchDispatchScheduler _scheduler;
    private SearchSnapshot _snapshot = SearchSnapshot.Initial;
    private CancellationTokenSource? _debounceCts;

    public SearchCoordinator(ISearchDispatchScheduler? scheduler = null)
    {
        _scheduler = scheduler ?? new ThreadTimerDispatchScheduler();
    }

    public SearchSnapshot Snapshot
    {
        get { lock (_lock) return _snapshot; }
    }

    public void Submit(double targetOhms, int depth, IReadOnlyList<Resistor> stock)
    {
        _debounceCts?.Cancel();

        if (stock.Count == 0)
        {
            _debounceCts = null;
            lock (_lock)
                _snapshot = new SearchSnapshot(SearchStatus.NoStock, Array.Empty<CombinationResult>());
            return;
        }

        lock (_lock)
            _snapshot = _snapshot with { Status = SearchStatus.Searching };

        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        _scheduler.Schedule(() => Dispatch(targetOhms, depth, stock), DebounceDelay, cts.Token);
    }

    private async Task Dispatch(double targetOhms, int depth, IReadOnlyList<Resistor> stock)
    {
        await _searchGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var results = await Task.Run(() => CombinationFinder.Find(stock, targetOhms, depth)).ConfigureAwait(false);
            lock (_lock)
                _snapshot = new SearchSnapshot(SearchStatus.Done, results);
        }
        finally
        {
            _searchGate.Release();
        }
    }
}

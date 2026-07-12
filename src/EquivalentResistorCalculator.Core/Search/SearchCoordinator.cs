using EquivalentResistorCalculator.Core.Models;

namespace EquivalentResistorCalculator.Core.Search;

public sealed class SearchCoordinator : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _lock = new();
    private readonly SemaphoreSlim _searchGate = new(1, 1);
    private readonly ISearchDispatchScheduler _scheduler;
    private readonly List<Task> _outstandingDispatches = new();
    private SearchSnapshot _snapshot = SearchSnapshot.Initial;
    private CancellationTokenSource? _activeCts;

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
        _activeCts?.Cancel();

        if (stock.Count == 0)
        {
            _activeCts = null;
            lock (_lock)
                _snapshot = new SearchSnapshot(SearchStatus.NoStock, Array.Empty<CombinationResult>());
            return;
        }

        lock (_lock)
            _snapshot = _snapshot with { Status = SearchStatus.Searching };

        var cts = new CancellationTokenSource();
        _activeCts = cts;

        var schedule = _scheduler.Schedule(token => Dispatch(targetOhms, depth, stock, token), DebounceDelay, cts.Token);
        TrackOutstanding(schedule);
    }

    public void Dispose()
    {
        _activeCts?.Cancel();

        Task[] outstanding;
        lock (_lock)
            outstanding = _outstandingDispatches.ToArray();

        foreach (var task in outstanding)
        {
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        _searchGate.Dispose();
    }

    private void TrackOutstanding(Task task)
    {
        lock (_lock)
        {
            _outstandingDispatches.RemoveAll(t => t.IsCompleted);
            _outstandingDispatches.Add(task);
        }
    }

    private async Task Dispatch(double targetOhms, int depth, IReadOnlyList<Resistor> stock, CancellationToken cancellationToken)
    {
        bool gateAcquired = false;
        try
        {
            await _searchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateAcquired = true;

            var results = await Task.Run(
                () => CombinationFinder.Find(stock, targetOhms, depth, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (!cancellationToken.IsCancellationRequested)
            {
                lock (_lock)
                    _snapshot = new SearchSnapshot(SearchStatus.Done, results);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled search — discard silently, never publish.
        }
        finally
        {
            if (gateAcquired)
                _searchGate.Release();
        }
    }
}

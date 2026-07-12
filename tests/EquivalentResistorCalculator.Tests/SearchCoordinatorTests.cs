using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Search;

namespace EquivalentResistorCalculator.Tests;

public sealed class ManualDispatchScheduler : ISearchDispatchScheduler
{
    private Func<CancellationToken, Task>? _pendingDispatch;
    private CancellationToken _pendingToken;
    private TaskCompletionSource? _pendingTcs;
    public int ScheduleCallCount { get; private set; }

    public Task Schedule(Func<CancellationToken, Task> dispatch, TimeSpan delay, CancellationToken cancellationToken)
    {
        ScheduleCallCount++;
        _pendingDispatch = dispatch;
        _pendingToken = cancellationToken;
        _pendingTcs = new TaskCompletionSource();
        return _pendingTcs.Task;
    }

    public async Task Fire()
    {
        var dispatch = _pendingDispatch ?? throw new InvalidOperationException("No pending dispatch to fire.");
        var token = _pendingToken;
        var tcs = _pendingTcs!;
        _pendingDispatch = null;
        _pendingTcs = null;

        await dispatch(token).ConfigureAwait(false);
        tcs.SetResult();
    }
}

public class SearchCoordinatorTests
{
    [Fact]
    public void Snapshot_InitialState_IsIdleWithNoResults()
    {
        var coordinator = new SearchCoordinator();

        Assert.Equal(SearchStatus.Idle, coordinator.Snapshot.Status);
        Assert.Empty(coordinator.Snapshot.Results);
    }

    [Fact]
    public async Task Submit_TransitionsIdleToSearchingToDone()
    {
        var scheduler = new ManualDispatchScheduler();
        var coordinator = new SearchCoordinator(scheduler);
        var stock = new List<Resistor> { new(100, "100", PackageType.ThroughHole) };

        coordinator.Submit(120, 3, stock);

        Assert.Equal(SearchStatus.Searching, coordinator.Snapshot.Status);

        await scheduler.Fire();

        Assert.Equal(SearchStatus.Done, coordinator.Snapshot.Status);
        Assert.NotEmpty(coordinator.Snapshot.Results);
    }

    [Fact]
    public void Submit_EmptyStock_PublishesNoStockStatusWithoutRunningFinder()
    {
        var scheduler = new ManualDispatchScheduler();
        var coordinator = new SearchCoordinator(scheduler);

        coordinator.Submit(100, 3, Array.Empty<Resistor>());

        Assert.Equal(SearchStatus.NoStock, coordinator.Snapshot.Status);
        Assert.Empty(coordinator.Snapshot.Results);
        Assert.Equal(0, scheduler.ScheduleCallCount);
    }

    [Fact]
    public async Task Submit_RapidSuccessiveRequests_CoalesceIntoOneDispatchCarryingLatestParameters()
    {
        var scheduler = new ManualDispatchScheduler();
        var coordinator = new SearchCoordinator(scheduler);
        var stock = new List<Resistor>
        {
            new(100, "100", PackageType.ThroughHole),
            new(220, "220", PackageType.ThroughHole),
        };

        coordinator.Submit(100, 1, stock);
        coordinator.Submit(150, 1, stock);
        coordinator.Submit(220, 1, stock);

        Assert.Equal(3, scheduler.ScheduleCallCount);

        await scheduler.Fire();

        Assert.Equal(SearchStatus.Done, coordinator.Snapshot.Status);
        var topResult = coordinator.Snapshot.Results[0];
        Assert.Equal(220, topResult.TotalResistance);
    }

    [Fact]
    public async Task Submit_NewerRequestCancelsInFlightOlderSearch_CancelledResultsNeverPublished_LatestRequestWins()
    {
        var scheduler = new ManualDispatchScheduler();
        var coordinator = new SearchCoordinator(scheduler);

        var bigStock = Enumerable.Range(1, 300)
            .Select(i => new Resistor(i, $"R{i}", PackageType.ThroughHole))
            .ToList();
        var smallStock = new List<Resistor> { new(220, "220", PackageType.ThroughHole) };

        coordinator.Submit(1_000, 3, bigStock);
        var dispatch1 = scheduler.Fire();

        coordinator.Submit(220, 1, smallStock);

        await dispatch1;

        var dispatch2 = scheduler.Fire();
        await dispatch2;

        Assert.Equal(SearchStatus.Done, coordinator.Snapshot.Status);
        Assert.Single(coordinator.Snapshot.Results);
        Assert.Equal(220, coordinator.Snapshot.Results[0].TotalResistance);
    }

    [Fact]
    public void Dispose_CancelsInFlightSearchAndJoinsItBeforeReturning()
    {
        var scheduler = new ManualDispatchScheduler();
        var coordinator = new SearchCoordinator(scheduler);

        var bigStock = Enumerable.Range(1, 300)
            .Select(i => new Resistor(i, $"R{i}", PackageType.ThroughHole))
            .ToList();

        coordinator.Submit(1_000, 3, bigStock);
        _ = scheduler.Fire();

        coordinator.Dispose();

        Assert.Equal(SearchStatus.Searching, coordinator.Snapshot.Status);
    }
}

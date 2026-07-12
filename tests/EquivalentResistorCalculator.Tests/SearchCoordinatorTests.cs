using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Search;

namespace EquivalentResistorCalculator.Tests;

public sealed class ManualDispatchScheduler : ISearchDispatchScheduler
{
    private Func<Task>? _pending;
    public int ScheduleCallCount { get; private set; }

    public void Schedule(Func<Task> dispatch, TimeSpan delay, CancellationToken cancellationToken)
    {
        ScheduleCallCount++;
        _pending = dispatch;
    }

    public Task Fire()
    {
        var dispatch = _pending ?? throw new InvalidOperationException("No pending dispatch to fire.");
        _pending = null;
        return dispatch();
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
}

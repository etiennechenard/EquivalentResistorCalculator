using EquivalentResistorCalculator.Core.Models;
using EquivalentResistorCalculator.Core.Search;

namespace EquivalentResistorCalculator.Tests;

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
        var coordinator = new SearchCoordinator();
        var stock = new List<Resistor> { new(100, "100", PackageType.ThroughHole) };

        var task = coordinator.Submit(120, 3, stock);

        Assert.Equal(SearchStatus.Searching, coordinator.Snapshot.Status);

        await task;

        Assert.Equal(SearchStatus.Done, coordinator.Snapshot.Status);
        Assert.NotEmpty(coordinator.Snapshot.Results);
    }

    [Fact]
    public async Task Submit_EmptyStock_PublishesNoStockStatusWithoutRunningFinder()
    {
        var coordinator = new SearchCoordinator();

        var task = coordinator.Submit(100, 3, Array.Empty<Resistor>());
        await task;

        Assert.Equal(SearchStatus.NoStock, coordinator.Snapshot.Status);
        Assert.Empty(coordinator.Snapshot.Results);
    }
}

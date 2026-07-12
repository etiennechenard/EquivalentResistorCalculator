namespace EquivalentResistorCalculator.Core.Search;

public interface ISearchDispatchScheduler
{
    Task Schedule(Func<CancellationToken, Task> dispatch, TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class ThreadTimerDispatchScheduler : ISearchDispatchScheduler
{
    public async Task Schedule(Func<CancellationToken, Task> dispatch, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await dispatch(cancellationToken).ConfigureAwait(false);
    }
}

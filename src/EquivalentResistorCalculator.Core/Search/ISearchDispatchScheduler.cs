namespace EquivalentResistorCalculator.Core.Search;

public interface ISearchDispatchScheduler
{
    void Schedule(Func<Task> dispatch, TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class ThreadTimerDispatchScheduler : ISearchDispatchScheduler
{
    public void Schedule(Func<Task> dispatch, TimeSpan delay, CancellationToken cancellationToken)
    {
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await dispatch().ConfigureAwait(false);
        }
    }
}

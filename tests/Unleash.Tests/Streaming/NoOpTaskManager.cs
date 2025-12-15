using Unleash.Scheduling;

namespace Unleash.Tests;

internal class NoOpTaskManager : IUnleashScheduledTaskManager
{
    public void Configure(IEnumerable<IUnleashScheduledTask> tasks, CancellationToken cancellationToken)
    {
    }

    public void Dispose()
    {
        // Mock dispose method
    }
}

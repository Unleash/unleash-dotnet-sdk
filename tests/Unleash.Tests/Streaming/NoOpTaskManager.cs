using Unleash.Scheduling;

namespace Unleash.Tests;

internal class NoOpTaskManager : IUnleashScheduledTaskManager
{
    public void ConfigureTask(IUnleashScheduledTask task, CancellationToken cancellationToken, bool start)
    {
    }


    public void Dispose()
    {
        // Mock dispose method
    }

    public void Start(IUnleashScheduledTask task)
    {
    }

    public void Stop(IUnleashScheduledTask task)
    {
    }
}

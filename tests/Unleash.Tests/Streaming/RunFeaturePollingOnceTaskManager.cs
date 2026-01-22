using Unleash.Scheduling;

namespace Unleash.Tests;

internal class RunFeaturePollingOnceTaskManager : IUnleashScheduledTaskManager
{
    private TaskFactory taskFactory = new TaskFactory(CancellationToken.None,
                                                    TaskCreationOptions.None,
                                                    TaskContinuationOptions.None,
                                                    TaskScheduler.Default);


    public void ConfigureTask(IUnleashScheduledTask task, CancellationToken cancellationToken, bool start)
    {
    }


    public void Dispose()
    {
        // Mock dispose method
    }

    public void Start(IUnleashScheduledTask task)
    {
        if (task.Name == "fetch-feature-toggles-task")
        {
            taskFactory
                .StartNew(() => task.ExecuteAsync(CancellationToken.None))
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }
    }

    public void Stop(IUnleashScheduledTask task)
    {
    }
}

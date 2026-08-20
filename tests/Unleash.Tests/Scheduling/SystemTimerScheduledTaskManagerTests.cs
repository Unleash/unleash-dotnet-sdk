using FluentAssertions;
using NUnit.Framework;
using Unleash.Scheduling;

namespace Unleash.Tests.Scheduling;

[NonParallelizable]
public class SystemTimerScheduledTaskManagerTests
{
    [Test]
    public async Task Cancellation_stops_a_repeating_timer_without_disposing_the_manager()
    {
        var activeTimersBeforeTest = Timer.ActiveCount;
        using var cancellationTokenSource = new CancellationTokenSource();
        using var manager = new SystemTimerScheduledTaskManager();
        var task = new CancellationAwareTask();

        manager.ConfigureTask(task, cancellationTokenSource.Token, true);
        await task.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellationTokenSource.Cancel();
        await task.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await WaitUntilAsync(
            () => Timer.ActiveCount == activeTimersBeforeTest,
            TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Callback_tolerates_disposal_winning_the_cancellation_race()
    {
        var activeTimersBeforeTest = Timer.ActiveCount;
        using var cancellationTokenSource = new CancellationTokenSource();
        var manager = new SystemTimerScheduledTaskManager();
        var task = new BlockingTask();

        manager.ConfigureTask(task, cancellationTokenSource.Token, true);
        await task.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        manager.Dispose();
        cancellationTokenSource.Cancel();
        task.Release.TrySetResult();
        await task.Completed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(50);

        await WaitUntilAsync(
            () => Timer.ActiveCount == activeTimersBeforeTest,
            TimeSpan.FromSeconds(1));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }

    private sealed class CancellationAwareTask : IUnleashScheduledTask
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => nameof(CancellationAwareTask);
        public TimeSpan Interval => TimeSpan.FromMilliseconds(10);
        public bool ExecuteDuringStartup => true;

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                Cancelled.TrySetResult();
            }
        }
    }

    private sealed class BlockingTask : IUnleashScheduledTask
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => nameof(BlockingTask);
        public TimeSpan Interval => TimeSpan.FromMilliseconds(10);
        public bool ExecuteDuringStartup => true;

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task;
            Completed.TrySetResult();
        }
    }
}

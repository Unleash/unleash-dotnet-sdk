using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Internal;
using Unleash.Logging;

namespace Unleash.Scheduling
{
    /// <inheritdoc />
    /// <summary>
    /// Default task manager based on System.Threading.Timers.
    /// </summary>
    internal class SystemTimerScheduledTaskManager : IUnleashScheduledTaskManager
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(SystemTimerScheduledTaskManager));

        private readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        public void Configure(IEnumerable<IUnleashScheduledTask> tasks, CancellationToken cancellationToken)
        {
            foreach (var task in tasks)
            {
                ConfigureTask(task, cancellationToken);
            }
        }

        private void ConfigureTask(IUnleashScheduledTask task, CancellationToken cancellationToken)
        {
            var name = task.Name;

            async void Callback(object state)
            {
                if (_shuttingDown) return;

                try
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await task.ExecuteAsync(cancellationToken);
                    }
                }
                catch (TaskCanceledException taskCanceledException)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.Warn(() => $"UNLEASH: Task '{name}' cancelled ...", taskCanceledException);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(() => $"UNLEASH: Unhandled exception from background task '{name}'.", ex);
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Stop the timer.
                        if (timers.TryGetValue(name, out var timerToStop))
                        {
                            timerToStop.SafeTimerChange(Timeout.Infinite, Timeout.Infinite, ref _disposed);
                        }
                    }
                }
            }

            var dueTime = task.ExecuteDuringStartup
                ? TimeSpan.Zero
                : task.Interval;

            var period = task.Interval == TimeSpan.Zero
                ? Timeout.InfiniteTimeSpan
                : task.Interval;

            // Don't start the timer before it has been added to the dictionary.
            var timer = new Timer(
                callback: Callback,
                state: null,
                dueTime: Timeout.Infinite,
                period: Timeout.Infinite);

            timers.Add(name, timer);

            // Now it's ok to start the timer.
            timer.SafeTimerChange(dueTime, period, ref _disposed);
        }

        private volatile bool _shuttingDown;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;

            _shuttingDown = true;
            var timeout = TimeSpan.FromSeconds(1);
            var timerNames = new List<string>(timers.Keys.AsEnumerable());

            foreach (var name in timerNames)
            {
                timers[name]?.Change(Timeout.Infinite, Timeout.Infinite);
            }

            foreach (var name in timerNames)
            {
                var t = timers[name];
                if (t is null) continue;

                using (var done = new ManualResetEvent(false))
                {
                    var willSignal = t.Dispose(done);
                    if (willSignal)
                    {
                        if (!done.WaitOne(timeout))
                            throw new TimeoutException($"Timeout waiting for timer '{name}' to stop.");
                    }
                }
            }

            timers.Clear();
            _disposed = true;
        }
    }
}


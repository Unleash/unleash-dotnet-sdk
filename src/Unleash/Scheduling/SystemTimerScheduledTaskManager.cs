using System;
using System.Collections.Generic;
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
						if (timers.ContainsKey(name))
						{
							timers[name].SafeTimerChange(Timeout.Infinite, Timeout.Infinite, ref disposeEnded);
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


			if (timers.ContainsKey(name))
				timers.Remove(name);

			timers.Add(name, timer);

			// Now it's ok to start the timer.
			timer.SafeTimerChange(dueTime, period, ref disposeEnded);
		}

		private bool disposeEnded;
		public void Dispose()
		{
			if (disposeEnded)
				return;

			var timeout = TimeSpan.FromSeconds(1);

			using (var waitHandle = new ManualResetEvent(false))
			{
				foreach (var task in timers)
				{
					// Returns false on second dispose
					if (task.Value.Dispose(waitHandle))
					{
						if (!waitHandle.WaitOne(timeout))
						{
							throw new TimeoutException($"UNLEASH: Timeout waiting for task '{task.Key}' to stop..");
						}
					}
				}
			}

			disposeEnded = true;
			timers.Clear();
		}
	}
}

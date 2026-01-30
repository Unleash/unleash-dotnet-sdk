using System;
using System.Threading;

namespace Unleash.Scheduling
{
    /// <inheritdoc />
    /// <summary>
    /// Task manager for scheduling tasks on a background thread. 
    /// </summary>
    public interface IUnleashScheduledTaskManager : IDisposable
    {
        /// <summary>
        /// Configures a task to execute in the background.
        /// </summary>
        /// <param name="task">Task to be executed</param>
        /// <param name="cancellationToken">Cancellation token which will be passed during shutdown (Dispose).</param>
        /// <param name="startAfterConfigure">If the configured task should also be started.</param>
        void ConfigureTask(IUnleashScheduledTask task, CancellationToken cancellationToken, bool start);

        /// <summary>
        /// Start a preconfigured task
        /// </summary>
        /// <param name="task"></param>
        void Start(IUnleashScheduledTask task);

        /// <summary>
        /// Stop a preconfigured running task
        /// </summary>
        /// <param name="task"></param>
        void Stop(IUnleashScheduledTask task);
    }
}
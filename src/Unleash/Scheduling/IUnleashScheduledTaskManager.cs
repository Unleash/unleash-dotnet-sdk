using System;
using System.Collections.Generic;
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
        /// Configures a set of tasks to execute in the background.
        /// </summary>
        /// <param name="tasks">Tasks to be executed</param>
        /// <param name="cancellationToken">Cancellation token which will be passed during shutdown (Dispose).</param>
        [Obsolete("This API will change in version 6. Details can be found in the v6_MIGRATION_GUIDE.md in the SDK repository.", false)]
        void Configure(IEnumerable<IUnleashScheduledTask> tasks, CancellationToken cancellationToken);
    }
}
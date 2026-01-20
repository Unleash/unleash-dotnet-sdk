using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Logging;
using Yggdrasil;

namespace Unleash.Scheduling
{
    internal class PollingFeatureFetcher : IDisposable
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(PollingFeatureFetcher));
        private TaskFactory TaskFactory;
        private bool ready;
        private readonly IUnleashScheduledTaskManager scheduledTaskManager;
        private readonly bool synchronousInitialization;
        private FetchFeatureTogglesTask fetchFeatureTogglesTask;
        private Action<string> ModeChange { get; }


        public PollingFeatureFetcher(
            UnleashConfig config,
            string backupResultInitialETag,
            CancellationToken cancellationToken,
            Action<string> modeChange
        )
        {
            this.scheduledTaskManager = config.ScheduledTaskManager;
            this.synchronousInitialization = config.SynchronousInitialization && !config.ExperimentalUseStreaming;
            this.TaskFactory = config.TaskFactory;
            ModeChange = modeChange;
            fetchFeatureTogglesTask = new FetchFeatureTogglesTask(
                config
            )
            {
                ExecuteDuringStartup = config.ScheduleFeatureToggleFetchImmediatly,
                Interval = config.FetchTogglesInterval,
                Etag = backupResultInitialETag,
            };
            fetchFeatureTogglesTask.OnReady += HandleReady;
            scheduledTaskManager.ConfigureTask(fetchFeatureTogglesTask, cancellationToken, false);
        }


        public void Start()
        {
            if (synchronousInitialization && !ready)
            {
                TaskFactory
                        .StartNew(() => fetchFeatureTogglesTask.ExecuteAsync(CancellationToken.None))
                        .Unwrap()
                        .GetAwaiter()
                        .GetResult();
            }
            scheduledTaskManager.Start(fetchFeatureTogglesTask);
        }

        public void Stop()
        {
            scheduledTaskManager.Stop(fetchFeatureTogglesTask);
        }

        public void Dispose()
        {
            fetchFeatureTogglesTask.OnReady -= HandleReady;
        }

        internal event EventHandler OnReady;

        private void HandleReady(object sender, EventArgs e)
        {
            ready = true;
            OnReady?.Invoke(this, e);
        }
    }
}
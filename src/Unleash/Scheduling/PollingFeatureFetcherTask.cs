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
            UnleashSettings settings,
            IUnleashScheduledTaskManager scheduledTaskManager,
            YggdrasilEngine engine,
            IUnleashApiClient apiClient,
            EventCallbackConfig eventConfig,
            IBackupManager backupManager,
            bool throwOnInitialLoadFail,
            bool synchronousInitialization,
            CancellationToken cancellationToken,
            string backupResultInitialETag,
            Action<string> modeChange,
            TaskFactory taskFactory
        )
        {
            this.scheduledTaskManager = scheduledTaskManager;
            this.synchronousInitialization = synchronousInitialization;
            this.TaskFactory = taskFactory;
            ModeChange = modeChange;
            fetchFeatureTogglesTask = new FetchFeatureTogglesTask(
                engine,
                apiClient,
                eventConfig,
                backupManager,
                throwOnInitialLoadFail
            )
            {
                ExecuteDuringStartup = settings.ScheduleFeatureToggleFetchImmediatly,
                Interval = settings.FetchTogglesInterval,
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
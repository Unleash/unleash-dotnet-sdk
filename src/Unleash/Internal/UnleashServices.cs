using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Communication;
using Unleash.Events;
using Unleash.Internal;
using Unleash.Logging;
using Unleash.Scheduling;
using Unleash.Strategies;
using Unleash.Streaming;
using Yggdrasil;

namespace Unleash
{
    internal class UnleashServices : IDisposable
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(UnleashServices));
        private int ready = 0;

        private readonly IUnleashScheduledTaskManager scheduledTaskManager;

        private PollingFeatureFetcher PollingFeatureFetcher;
        private StreamingFeatureFetcher StreamingFeatureFetcher;
        private EventCallbackConfig EventConfig { get; }
        private bool IsCustomScheduledTaskManager { get { return scheduledTaskManager != null && !(scheduledTaskManager is SystemTimerScheduledTaskManager); } }


        internal IUnleashContextProvider ContextProvider { get; }
        internal YggdrasilEngine engine { get; }

        /// <summary>
        /// Triggers on Polling/Streaming first proper hydration
        /// </summary>
        internal event EventHandler OnHydrated;

        internal UnleashServices(UnleashConfig config, List<Strategies.IStrategy> strategies = null)
        {
            EventConfig = config.EventConfig;

            var backupResult = config.BackupManager.Load();

            if (!string.IsNullOrEmpty(backupResult.InitialState))
            {
                try
                {
                    engine.TakeState(backupResult.InitialState);
                }
                catch (Exception ex)
                {
                    Logger.Error(() => $"UNLEASH: Failed to load initial state from file: {ex.Message}");
                    EventConfig.RaiseError(new ErrorEvent() { Error = ex, ErrorType = ErrorType.FileCache });
                }
            }

            PollingFeatureFetcher = new PollingFeatureFetcher(
                config,
                backupResult.InitialETag,
                config.CancellationToken,
                HandleModeChange);
            PollingFeatureFetcher.OnReady += OnHydrationSourceReadyHandler;

            StreamingFeatureFetcher = new StreamingFeatureFetcher(
                config,
                HandleModeChange);
            StreamingFeatureFetcher.OnReady += OnHydrationSourceReadyHandler;

            try
            {
                if (config.ExperimentalUseStreaming)
                {
                    config.TaskFactory
                        .StartNew(() => StreamingFeatureFetcher.StartAsync().ConfigureAwait(false))
                        .GetAwaiter()
                        .GetResult();
                }
                else
                {
                    PollingFeatureFetcher.Start();
                }
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }
        }

        internal void OnHydrationSourceReadyHandler(object sender, EventArgs e)
        {
            var raiseReady = Interlocked.Exchange(ref ready, 1) == 0;
            if (raiseReady)
            {
                // internal update first
                OnHydrated?.Invoke(this, new EventArgs());
                EventConfig.RaiseReady(new ReadyEvent());
            }
        }

        private void HandleModeChange(string newMode)
        {
            if (newMode == "polling")
            {
                Task.Run(() => StreamingFeatureFetcher.StopAsync().ConfigureAwait(false));
                PollingFeatureFetcher.Start();
            }
            else if (newMode == "streaming")
            {
                PollingFeatureFetcher.Stop();
                Task.Run(() => StreamingFeatureFetcher.StartAsync().ConfigureAwait(false));
            }
        }

        public void Dispose()
        {
            engine?.Dispose();
            if (IsCustomScheduledTaskManager)
            {
                Logger.Warn(() => $"UNLEASH: Disposing ScheduledTaskManager of type {scheduledTaskManager.GetType().Name}");
            }

            PollingFeatureFetcher.OnReady -= OnHydrationSourceReadyHandler;
            PollingFeatureFetcher.Dispose();
            StreamingFeatureFetcher.OnReady -= OnHydrationSourceReadyHandler;
            StreamingFeatureFetcher.Dispose();
            scheduledTaskManager?.Dispose();
        }
    }
}
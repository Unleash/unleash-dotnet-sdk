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
        private static readonly TaskFactory TaskFactory =
            new TaskFactory(CancellationToken.None,
                          TaskCreationOptions.None,
                          TaskContinuationOptions.None,
                          TaskScheduler.Default);

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly IUnleashScheduledTaskManager scheduledTaskManager;
        private readonly string connectionId = Guid.NewGuid().ToString();

        private PollingFeatureFetcher PollingFeatureFetcher;
        private StreamingFeatureFetcher StreamingFeatureFetcher;
        private EventCallbackConfig EventConfig { get; }
        private bool IsCustomScheduledTaskManager { get { return scheduledTaskManager != null && !(scheduledTaskManager is SystemTimerScheduledTaskManager); } }

        public const string supportedSpecVersion = "5.1.9";

        internal CancellationToken CancellationToken { get; }
        internal IUnleashContextProvider ContextProvider { get; }
        internal bool IsMetricsDisabled { get; }
        internal YggdrasilEngine engine { get; }

        /// <summary>
        /// Triggers on Polling/Streaming first proper hydration
        /// </summary>
        internal event EventHandler OnHydrated;

        private static readonly IList<string> DefaultStrategyNames = new List<string> {
            "applicationHostname",
            "default",
            "flexibleRollout",
            "gradualRolloutRandom",
            "gradualRolloutSessionId",
            "gradualRolloutUserId",
            "remoteAddress",
            "userWithId"
        };
        public UnleashServices(UnleashSettings settings, EventCallbackConfig eventConfig, List<Strategies.IStrategy> strategies = null) :
            this(settings, eventConfig, false, strategies)
        {
        }

        internal UnleashServices(UnleashSettings settings, EventCallbackConfig eventConfig, bool synchronousInitialization, List<Strategies.IStrategy> strategies = null)
        {
            if (settings.FileSystem == null)
            {
                settings.FileSystem = new FileSystem(settings.Encoding);
            }

            scheduledTaskManager = settings.ScheduledTaskManager ?? new SystemTimerScheduledTaskManager();

            List<Yggdrasil.IStrategy> yggdrasilStrategies = strategies?.Select(s => new CustomStrategyAdapter(s)).Cast<Yggdrasil.IStrategy>().ToList();

            engine = new YggdrasilEngine(yggdrasilStrategies);
            EventConfig = eventConfig;

            // Cancellation
            CancellationToken = cancellationTokenSource.Token;
            ContextProvider = settings.UnleashContextProvider;

            var backupManager = new CachedFilesLoader(settings, eventConfig);
            var backupResult = backupManager.Load();

            if (!string.IsNullOrEmpty(backupResult.InitialState))
            {
                try
                {
                    engine.TakeState(backupResult.InitialState);
                }
                catch (Exception ex)
                {
                    Logger.Error(() => $"UNLEASH: Failed to load initial state from file: {ex.Message}");
                    eventConfig.RaiseError(new ErrorEvent() { Error = ex, ErrorType = ErrorType.FileCache });
                }
            }

            IUnleashApiClient apiClient;
            if (settings.UnleashApiClient == null)
            {
                var uri = settings.UnleashApi;
                if (!uri.AbsolutePath.EndsWith("/"))
                {
                    uri = new Uri($"{uri.AbsoluteUri}/");
                }

                var httpClient = settings.HttpClientFactory.Create(uri);
                apiClient = new UnleashApiClient(httpClient, new UnleashApiClientRequestHeaders()
                {
                    AppName = settings.AppName,
                    InstanceTag = settings.InstanceTag,
                    ConnectionId = connectionId,
                    SdkVersion = settings.SdkVersion,
                    CustomHttpHeaders = settings.CustomHttpHeaders,
                    CustomHttpHeaderProvider = settings.UnleashCustomHttpHeaderProvider,
                    SupportedSpecVersion = supportedSpecVersion
                }, eventConfig, settings.ProjectId);
            }
            else
            {
                // Mocked backend: fill instance collection
                apiClient = settings.UnleashApiClient;
            }

            IsMetricsDisabled = settings.SendMetricsInterval == null;
            if (!IsMetricsDisabled)
            {
                var strategyNames = (strategies == null ? DefaultStrategyNames : DefaultStrategyNames.Concat(strategies.Select(s => s.Name))).ToList();

                var clientRegistrationBackgroundTask = new ClientRegistrationBackgroundTask(
                    apiClient,
                    settings,
                    strategyNames)
                {
                    Interval = TimeSpan.Zero,
                    ExecuteDuringStartup = true
                };

                scheduledTaskManager.ConfigureTask(clientRegistrationBackgroundTask, cancellationTokenSource.Token, true);

                var clientMetricsBackgroundTask = new ClientMetricsBackgroundTask(
                    engine,
                    apiClient,
                    settings
                    )
                {
                    Interval = settings.SendMetricsInterval.Value
                };

                scheduledTaskManager.ConfigureTask(clientMetricsBackgroundTask, cancellationTokenSource.Token, true);
            }

            PollingFeatureFetcher = new PollingFeatureFetcher(
                settings,
                scheduledTaskManager,
                engine,
                apiClient,
                eventConfig,
                backupManager,
                settings.ThrowOnInitialFetchFail,
                synchronousInitialization && !settings.ExperimentalUseStreaming,
                CancellationToken,
                backupResult.InitialETag,
                HandleModeChange,
                TaskFactory);
            PollingFeatureFetcher.OnReady += OnHydrationSourceReadyHandler;

            StreamingFeatureFetcher = new StreamingFeatureFetcher(
                settings,
                apiClient,
                engine,
                eventConfig,
                backupManager,
                HandleModeChange);
            StreamingFeatureFetcher.OnReady += OnHydrationSourceReadyHandler;

            try
            {
                if (settings.ExperimentalUseStreaming)
                {
                    TaskFactory
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
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

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
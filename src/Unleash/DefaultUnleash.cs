namespace Unleash
{
    using Internal;
    using Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Unleash.Communication;
    using Unleash.Scheduling;
    using Unleash.Strategies;
    using Unleash.Utilities;

    /// <inheritdoc />
    public class DefaultUnleash : IUnleash
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(DefaultUnleash));
        private readonly string connectionId = Guid.NewGuid().ToString();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private UnleashConfig config;


        private static int InitializedInstanceCount = 0;

        private const int ErrorOnInstanceCount = 10;

        private readonly UnleashSettings settings;

        internal readonly UnleashServices services;

        internal readonly MetricsService metrics;

        public const string supportedSpecVersion = "5.1.9";

        ///// <summary>
        ///// Initializes a new instance of Unleash client.
        ///// </summary>
        ///// <param name="settings">Unleash settings</param>
        ///// <param name="callback">Callback that called during the constructor to configure event listeners/callbacks</param>
        ///// <param name="strategies">Custom strategies.</param>
        public DefaultUnleash(UnleashSettings settings, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies) :
            this(settings, false, callback, strategies)
        { }

        internal DefaultUnleash(UnleashSettings settings, bool synchronousInitialization, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)
        {
            var currentInstanceNo = Interlocked.Increment(ref InitializedInstanceCount);

            this.settings = settings;

            ConfigureEvents(callback);

            var settingsValidator = new UnleashSettingsValidator();
            settingsValidator.Validate(settings);

            config = BuildUnleashConfig(settings, synchronousInitialization, EventConfig, strategies);
            metrics = new MetricsService(config);
            services = new UnleashServices(config, strategies?.ToList());

            Logger.Info(() => $"UNLEASH: Unleash instance number {currentInstanceNo} is initialized and configured with: {settings}");

            if (!settings.DisableSingletonWarning && currentInstanceNo >= ErrorOnInstanceCount)
            {
                Logger.Error(() => $"UNLEASH: Unleash instance count for this process is now {currentInstanceNo}.");
                Logger.Error(() => "Ideally you should only need 1 instance of Unleash per app/process, we strongly recommend setting up Unleash as a singleton.");
            }
        }

        private EventCallbackConfig EventConfig { get; } = new EventCallbackConfig();

        /// <inheritdoc />
        public bool IsEnabled(string toggleName)
        {
            return IsEnabled(toggleName, false);
        }

        /// <inheritdoc />
        public bool IsEnabled(string toggleName, bool defaultSetting)
        {
            return IsEnabled(toggleName, config.ContextProvider.Context, defaultSetting);
        }

        public bool IsEnabled(string toggleName, UnleashContext context)
        {
            return IsEnabled(toggleName, context, false);
        }

        public bool IsEnabled(string toggleName, UnleashContext context, bool defaultSetting)
        {
            var enhancedContext = context.ApplyStaticFields(settings);
            var response = config.Engine.IsEnabled(toggleName, enhancedContext);
            var enabled = response.HasEnabled ? response.Enabled : defaultSetting;

            if (response.ImpressionData)
            {
                EmitImpressionEvent("isEnabled", enhancedContext, enabled, toggleName);
            }

            return enabled;
        }

        public ICollection<ToggleDefinition> ListKnownToggles()
        {
            return config.Engine.ListKnownToggles().Select(ToggleDefinition.FromYggdrasilDef).ToList();
        }

        public Variant GetVariant(string toggleName)
        {
            return GetVariant(toggleName, config.ContextProvider.Context, Variant.DISABLED_VARIANT);
        }

        public Variant GetVariant(string toggleName, Variant defaultVariant)
        {
            return GetVariant(toggleName, config.ContextProvider.Context, defaultVariant);
        }

        public Variant GetVariant(string toggleName, UnleashContext context)
        {
            return GetVariant(toggleName, context, Variant.DISABLED_VARIANT);
        }

        public Variant GetVariant(string toggleName, UnleashContext context, Variant defaultValue)
        {
            var enhancedContext = context.ApplyStaticFields(settings);

            var variant = config.Engine.GetVariant(toggleName, enhancedContext) ?? defaultValue;
            var enabled = config.Engine.IsEnabled(toggleName, enhancedContext);
            variant.FeatureEnabled = enabled.Enabled;

            if (enabled.ImpressionData)
            {
                EmitImpressionEvent("getVariant", enhancedContext, variant.Enabled, toggleName, variant.Name);
            }

            return Variant.UpgradeVariant(variant);
        }

        private UnleashConfig BuildUnleashConfig(
            UnleashSettings settings,
            bool synchronousInitialization,
            EventCallbackConfig eventConfig,
            params IStrategy[] strategies)
        {
            var taskFactory = new TaskFactory(CancellationToken.None,
                          TaskCreationOptions.None,
                          TaskContinuationOptions.None,
                          TaskScheduler.Default);

            var fileSystem = settings.FileSystem ?? new FileSystem(settings.Encoding);
            var scheduledTaskManager = settings.ScheduledTaskManager ?? new SystemTimerScheduledTaskManager();
            var yggdrasilStrategies = strategies?.Select(s => new CustomStrategyAdapter(s)).Cast<Yggdrasil.IStrategy>().ToList();
            var apiClient = settings.UnleashApiClient ?? BuildApiClient(connectionId, supportedSpecVersion, eventConfig);
            return new UnleashConfig
            {
                ApiClient = apiClient,
                BackupManager = new CachedFilesLoader(settings, eventConfig, fileSystem),
                ContextProvider = settings.UnleashContextProvider,
                Engine = new Yggdrasil.YggdrasilEngine(yggdrasilStrategies),
                EventConfig = eventConfig,
                FileSystem = fileSystem,
                ScheduledTaskManager = scheduledTaskManager,
                TaskFactory = taskFactory,
                SynchronousInitialization = synchronousInitialization,
                AppName = settings.AppName,
                ExperimentalUseStreaming = settings.ExperimentalUseStreaming,
                FetchTogglesInterval = settings.FetchTogglesInterval,
                InstanceTag = settings.InstanceTag,
                ScheduleFeatureToggleFetchImmediatly = settings.ScheduleFeatureToggleFetchImmediatly,
                SdkVersion = settings.SdkVersion,
                SendMetricsInterval = settings.SendMetricsInterval,
                ThrowOnInitialFetchFail = settings.ThrowOnInitialFetchFail,
                UnleashApi = settings.UnleashApi,
                CancellationToken = cancellationTokenSource.Token
            };
        }

        private IUnleashApiClient BuildApiClient(string connectionId, string supportedSpecVersion, EventCallbackConfig eventConfig)
        {
            var uri = settings.UnleashApi;
            if (!uri.AbsolutePath.EndsWith("/"))
            {
                uri = new Uri($"{uri.AbsoluteUri}/");
            }

            var httpClient = settings.HttpClientFactory.Create(uri);
            return new UnleashApiClient(httpClient, new UnleashApiClientRequestHeaders()
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

        private void EmitImpressionEvent(string type, UnleashContext context, bool enabled, string name, string variant = null)
        {
            try
            {
                EventConfig.EmitImpressionEvent(type, context, enabled, name, variant);
            }
            catch (Exception ex)
            {
                Logger.Error(() => $"UNLEASH: Emitting impression event callback threw exception: {ex.Message}");
            }
        }

        private void ConfigureEvents(Action<EventCallbackConfig> callback)
        {
            try
            {
                callback?.Invoke(EventConfig);
            }
            catch (Exception ex)
            {
                Logger.Error(() => $"UNLEASH: Unleash->ConfigureEvents executing callback threw exception: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            services?.Dispose();
        }
    }
}

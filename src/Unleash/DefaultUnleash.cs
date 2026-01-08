namespace Unleash
{
    using Internal;
    using Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Unleash.Strategies;
    using Unleash.Utilities;

    /// <inheritdoc />
    public class DefaultUnleash : IUnleash
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(DefaultUnleash));

        private static int InitializedInstanceCount = 0;

        private const int ErrorOnInstanceCount = 10;

        private readonly UnleashSettings settings;

        internal readonly UnleashServices services;

        ///// <summary>
        ///// Initializes a new instance of Unleash client.
        ///// </summary>
        ///// <param name="settings">Unleash settings</param>
        ///// <param name="callback">Callback that called during the constructor to configure event listeners/callbacks</param>
        ///// <param name="strategies">Custom strategies.</param>
        public DefaultUnleash(UnleashSettings settings, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)
        {
            var currentInstanceNo = Interlocked.Increment(ref InitializedInstanceCount);

            this.settings = settings;

            ConfigureEvents(callback);

            var settingsValidator = new UnleashSettingsValidator();
            settingsValidator.Validate(settings);

            services = new UnleashServices(settings, EventConfig, strategies?.ToList());

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
            return IsEnabled(toggleName, services.ContextProvider.Context, defaultSetting);
        }

        public bool IsEnabled(string toggleName, UnleashContext context)
        {
            return IsEnabled(toggleName, context, false);
        }

        public bool IsEnabled(string toggleName, UnleashContext context, bool defaultSetting)
        {
            var enhancedContext = context.ApplyStaticFields(settings);
            var response = services.engine.IsEnabled(toggleName, enhancedContext);
            var enabled = response.HasEnabled ? response.Enabled : defaultSetting;

            if (response.ImpressionData)
            {
                EmitImpressionEvent("isEnabled", enhancedContext, enabled, toggleName);
            }

            return enabled;
        }

        public ICollection<ToggleDefinition> ListKnownToggles()
        {
            return services.engine.ListKnownToggles().Select(ToggleDefinition.FromYggdrasilDef).ToList();
        }

        public Variant GetVariant(string toggleName)
        {
            return GetVariant(toggleName, services.ContextProvider.Context, Variant.DISABLED_VARIANT);
        }

        public Variant GetVariant(string toggleName, Variant defaultVariant)
        {
            return GetVariant(toggleName, services.ContextProvider.Context, defaultVariant);
        }

        public Variant GetVariant(string toggleName, UnleashContext context)
        {
            return GetVariant(toggleName, context, Variant.DISABLED_VARIANT);
        }

        public Variant GetVariant(string toggleName, UnleashContext context, Variant defaultValue)
        {
            var enhancedContext = context.ApplyStaticFields(settings);

            var variant = services.engine.GetVariant(toggleName, enhancedContext) ?? defaultValue;
            var enabled = services.engine.IsEnabled(toggleName, enhancedContext);
            variant.FeatureEnabled = enabled.Enabled;

            if (enabled.ImpressionData)
            {
                EmitImpressionEvent("getVariant", enhancedContext, variant.Enabled, toggleName, variant.Name);
            }

            return Variant.UpgradeVariant(variant);
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
            services?.Dispose();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Internal;
using Unleash.Logging;
using Unleash.Strategies;

namespace Unleash.ClientFactory
{
    /// <inheritdoc />
    public class UnleashClientFactory : IUnleashClientFactory
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(UnleashClientFactory));

        /// <summary>
        /// Initializes a new instance of Unleash client.
        /// </summary>
        /// <param name="settings">Unleash settings.</param>
        /// <param name="synchronousInitialization">If true, fetch and cache toggles before returning. If false, allow the unleash client schedule an initial poll of features in the background</param>
        /// <param name="callback">Subscribe to Unleash events by adding a callback that accepts an EventCallbackConfig object.</param>
        /// <param name="strategies">Custom strategies, added in addtion to builtIn strategies.</param>
        public IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)
        {
            if (synchronousInitialization)
            {
                settings.ScheduleFeatureToggleFetchImmediatly = false;
                settings.ThrowOnInitialFetchFail = true;
                try
                {
                    return new DefaultUnleash(settings, true, callback, strategies);
                }
                catch (Exception ex)
                {
                    Logger.Error(() => $"UNLEASH: Exception in UnleashClientFactory when initializing synchronously", ex);
                    throw;
                }
            }
            return new DefaultUnleash(settings, callback, strategies);
        }

        /// <summary>
        /// Initializes a new instance of Unleash client.
        /// </summary>
        /// <param name="settings">Unleash settings.</param>
        /// <param name="synchronousInitialization">If true, fetch and cache toggles before returning. If false, allow the unleash client schedule an initial poll of features in the background</param>
        /// <param name="callback">Subscribe to Unleash events by adding a callback that accepts an EventCallbackConfig object.</param>
        /// <param name="strategies">Custom strategies, added in addtion to builtIn strategies.</param>
        public async Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, Action<EventCallbackConfig> callback = null, params IStrategy[] strategies)
        {
            if (synchronousInitialization)
            {
                settings.ScheduleFeatureToggleFetchImmediatly = false;
                settings.ThrowOnInitialFetchFail = true;
                try
                {
                    return new DefaultUnleash(settings, true, callback, strategies);
                }
                catch (Exception ex)
                {
                    Logger.Error(() => $"UNLEASH: Exception in UnleashClientFactory when initializing synchronously", ex);
                    throw;
                }
            }
            return new DefaultUnleash(settings, callback, strategies);
        }
    }
}

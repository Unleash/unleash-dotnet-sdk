using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Strategies;

namespace Unleash.ClientFactory
{
    /// <inheritdoc />
    public class UnleashClientFactory : IUnleashClientFactory
    {
        private static readonly TaskFactory TaskFactory =
            new TaskFactory(CancellationToken.None,
                          TaskCreationOptions.None,
                          TaskContinuationOptions.None,
                          TaskScheduler.Default);

        /// <summary>
        /// Initializes a new instance of Unleash client.
        /// </summary>
        /// <param name="synchronousInitialization">If true, fetch and cache toggles before returning. If false, allow the unleash client schedule an initial poll of features in the background</param>
        /// <param name="strategies">Custom strategies, added in addtion to builtIn strategies.</param>
        public IUnleash CreateClient(UnleashSettings settings, bool synchronousInitialization = false, params IStrategy[] strategies)
        {
            if (synchronousInitialization)
            {
                settings.ScheduleFeatureToggleFetchImmediatly = false;
                settings.ThrowOnInitialFetchFail = true;
                var unleash = new DefaultUnleash(settings, strategies);
                try
                {
                    TaskFactory
                        .StartNew(() => unleash.services.FetchFeatureTogglesTask.ExecuteAsync(CancellationToken.None))
                        .Unwrap()
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    unleash.Dispose();
                    throw;
                }

                return unleash;
            }
            return new DefaultUnleash(settings, strategies);
        }


        /// <summary>
        /// Initializes a new instance of Unleash client.
        /// </summary>
        /// <param name="synchronousInitialization">If true, fetch and cache toggles before returning. If false, allow the unleash client schedule an initial poll of features in the background</param>
        /// <param name="strategies">Custom strategies, added in addtion to builtIn strategies.</param>
        public async Task<IUnleash> CreateClientAsync(UnleashSettings settings, bool synchronousInitialization = false, params IStrategy[] strategies)
        {
            if (synchronousInitialization)
            {
                settings.ScheduleFeatureToggleFetchImmediatly = false;
                settings.ThrowOnInitialFetchFail = true;
                var unleash = new DefaultUnleash(settings, strategies);
                try
                {
                    await unleash.services.FetchFeatureTogglesTask.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    unleash.Dispose();
                    throw;
                }
                return unleash;
            }
            return new DefaultUnleash(settings, strategies);
        }
    }
}

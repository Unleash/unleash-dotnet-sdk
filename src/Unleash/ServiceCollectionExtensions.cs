#if NET8_0_OR_GREATER
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unleash.ClientFactory;

namespace Unleash
{
    /// <summary>
    /// Extension methods for configuring Unleash with Microsoft DI.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Unleash as a singleton in the DI container.
        /// Feature toggles are fetched in the background after registration.
        /// ILoggerFactory is automatically resolved from the container if not explicitly set.
        /// </summary>
        public static IServiceCollection AddUnleash(this IServiceCollection services, Action<UnleashSettings> configure)
        {
            return AddUnleash(services, configure, synchronousInitialization: false);
        }

        /// <summary>
        /// Registers Unleash as a singleton in the DI container.
        /// ILoggerFactory is automatically resolved from the container if not explicitly set.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Action to configure UnleashSettings.</param>
        /// <param name="synchronousInitialization">
        /// If true, blocks until the initial feature toggle fetch completes and throws on failure.
        /// If false, toggles are fetched in the background.
        /// </param>
        public static IServiceCollection AddUnleash(
            this IServiceCollection services,
            Action<UnleashSettings> configure,
            bool synchronousInitialization)
        {
            services.AddSingleton<IUnleash>(sp =>
            {
                var settings = new UnleashSettings();
                configure(settings);

                settings.LoggerFactory ??= sp.GetService<ILoggerFactory>();

                var factory = new UnleashClientFactory();
                return factory.CreateClient(settings, synchronousInitialization);
            });

            return services;
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Logging;
using Unleash.Metrics;

namespace Unleash.Scheduling
{
    internal class ClientRegistrationBackgroundTask : IUnleashScheduledTask
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(ClientRegistrationBackgroundTask));

        private readonly IUnleashApiClient apiClient;
        private ClientRegistration clientRegistration;
        private TimeSpan? SendMetricsInterval;

        public ClientRegistrationBackgroundTask(
            UnleashConfig config,
            List<string> strategies)
        {
            this.apiClient = config.ApiClient;
            this.SendMetricsInterval = config.SendMetricsInterval;
            this.clientRegistration = new ClientRegistration
            {
                AppName = config.AppName,
                InstanceId = config.InstanceTag,
                Interval = (long)config.SendMetricsInterval.Value.TotalMilliseconds,
                SdkVersion = config.SdkVersion,
                Started = DateTimeOffset.UtcNow,
                Strategies = strategies
            };
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (SendMetricsInterval == null)
                return;

            var result = await apiClient.RegisterClient(clientRegistration, cancellationToken).ConfigureAwait(false);
            if (!result)
            {
                // Already logged..    
            }
        }

        public string Name => "register-client-task";

        public TimeSpan Interval { get; set; }
        public bool ExecuteDuringStartup { get; set; }
    }
}

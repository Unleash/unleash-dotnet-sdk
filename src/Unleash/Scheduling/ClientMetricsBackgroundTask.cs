using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Logging;
using Yggdrasil;

namespace Unleash.Scheduling
{
    internal class ClientMetricsBackgroundTask : IUnleashScheduledTask
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(ClientMetricsBackgroundTask));
        private readonly YggdrasilEngine engine;
        private readonly IUnleashApiClient apiClient;
        private TimeSpan? sendMetricsInterval;

        public ClientMetricsBackgroundTask(UnleashConfig config)
        {
            this.engine = config.Engine;
            this.apiClient = config.ApiClient;
            this.sendMetricsInterval = config.SendMetricsInterval;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (sendMetricsInterval == null)
                return;

            var result = await apiClient.SendMetrics(engine.GetMetrics(), cancellationToken).ConfigureAwait(false);

            // Ignore return value    
            if (!result)
            {
                // Logged elsewhere.
            }
        }

        public string Name => "report-metrics-task";
        public TimeSpan Interval { get; set; }
        public bool ExecuteDuringStartup { get; set; }
    }
}
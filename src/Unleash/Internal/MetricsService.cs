
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unleash.Communication;
using Unleash.Scheduling;
using Yggdrasil;

namespace Unleash.Internal
{
    internal class MetricsService
    {
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

        internal bool IsMetricsDisabled { get; }
        internal MetricsService(
            UnleashConfig config,
            List<Strategies.IStrategy> strategies = null)
        {
            IsMetricsDisabled = config.SendMetricsInterval == null;
            if (!IsMetricsDisabled)
            {
                var strategyNames = (strategies == null ? DefaultStrategyNames : DefaultStrategyNames.Concat(strategies.Select(s => s.Name))).ToList();

                var clientRegistrationBackgroundTask = new ClientRegistrationBackgroundTask(
                    config,
                    strategyNames)
                {
                    Interval = TimeSpan.Zero,
                    ExecuteDuringStartup = true
                };

                config.ScheduledTaskManager.ConfigureTask(clientRegistrationBackgroundTask, config.CancellationToken, true);

                var clientMetricsBackgroundTask = new ClientMetricsBackgroundTask(config);

                config.ScheduledTaskManager.ConfigureTask(clientMetricsBackgroundTask, config.CancellationToken, true);
            }
        }
    }
}

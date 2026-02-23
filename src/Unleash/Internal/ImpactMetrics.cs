using System.Collections.Generic;

namespace Unleash.Internal
{
    internal class ImpactMetrics : IImpactMetrics
    {
        private readonly UnleashConfig config;
        private readonly Dictionary<string, string> baseLabels;

        internal ImpactMetrics(UnleashConfig config)
        {
            this.config = config;
            this.baseLabels = GetBaseLabels();
        }

        public void DefineCounter(string name, string description) =>
            config.Engine.DefineCounter(name, description);

        public void IncrementCounter(string name, long value = 1) =>
            config.Engine.IncCounter(name, value, baseLabels);

        public void DefineGauge(string name, string description) =>
            config.Engine.DefineGauge(name, description);

        public void UpdateGauge(string name, double value) =>
            config.Engine.SetGauge(name, value, baseLabels);

        public void DefineHistogram(string name, string description, double[] buckets = null) =>
            config.Engine.DefineHistogram(name, description, buckets);

        public void ObserveHistogram(string name, double value) =>
            config.Engine.ObserveHistogram(name, value, baseLabels);

        private Dictionary<string, string> GetBaseLabels()
        {
            return new Dictionary<string, string>
            {
                ["appName"] = config.AppName,
                ["environment"] = config.Environment()
            };
        }
    }
}

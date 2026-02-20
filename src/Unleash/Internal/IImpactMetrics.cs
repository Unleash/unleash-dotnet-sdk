namespace Unleash
{
    public interface IImpactMetrics
    {
        void DefineCounter(string name, string description);
        void IncrementCounter(string name, long value);
        void DefineGauge(string name, string description);
        void UpdateGauge(string name, double value);
        void DefineHistogram(string name, string description, double[] buckets);
        void ObserveHistogram(string name, double value);

    }
}
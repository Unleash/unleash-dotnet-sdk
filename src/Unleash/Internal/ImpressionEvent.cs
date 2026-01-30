namespace Unleash.Internal
{
    public class ImpressionEvent
    {
        public string Type { get; set; }
        public string EventId { get; set; }
        public UnleashContext Context { get; set; }
        public bool Enabled { get; set; }
        public string FeatureName { get; set; }
        public string Variant { get; set; }
    }
}

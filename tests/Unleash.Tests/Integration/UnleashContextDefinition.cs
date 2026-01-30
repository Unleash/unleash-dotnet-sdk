namespace Unleash.Tests.Specifications
{
    public class UnleashContextDefinition
    {
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public string RemoteAddress { get; set; }
        public string Environment { get; set; }
        public string AppName { get; set; }
        public DateTimeOffset? CurrentTime { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}

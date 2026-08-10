#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using Unleash.Metrics;

namespace Unleash.Serialization
{
    [JsonSerializable(typeof(ClientRegistration))]
    [JsonSerializable(typeof(ClientMetrics))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        IncludeFields = true)]
    internal partial class UnleashJsonSerializerContext : JsonSerializerContext
    {
    }
}
#endif

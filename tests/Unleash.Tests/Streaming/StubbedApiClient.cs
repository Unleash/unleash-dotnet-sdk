using Unleash.Streaming;
using Unleash.Communication;
using Unleash.Metrics;
using Yggdrasil;

namespace Unleash.Tests;

internal class StubbedApiClient : IUnleashApiClient
{
    public StreamingFeatureFetcher StreamingEventHandler { get; private set; }

    public Task<FetchTogglesResult> FetchToggles(string etag, CancellationToken cancellationToken, bool throwOnFail = false)
    {
        return Task.FromResult(new FetchTogglesResult());
    }

    public Task<bool> RegisterClient(ClientRegistration registration, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task<bool> SendMetrics(MetricsBucket metrics, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task StartStreamingAsync(Uri apiUri, StreamingFeatureFetcher streamingEventHandler)
    {
        StreamingEventHandler = streamingEventHandler;
        return Task.CompletedTask;
    }

    public void StopStreaming()
    {
    }
}

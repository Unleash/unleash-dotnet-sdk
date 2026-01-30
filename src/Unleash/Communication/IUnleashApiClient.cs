using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Metrics;
using Unleash.Streaming;

namespace Unleash.Communication
{
    internal interface IUnleashApiClient
    {
        Task<FetchTogglesResult> FetchToggles(string etag, CancellationToken cancellationToken, bool throwOnFail = false);
        Task<bool> RegisterClient(ClientRegistration registration, CancellationToken cancellationToken);
        // TODO: Can be simplified to `using Yggdrasil;` once MetricsBucket is dropped from Unleash.Metrics
        Task<bool> SendMetrics(Yggdrasil.MetricsBucket metrics, CancellationToken cancellationToken);

        Task StartStreamingAsync(Uri apiUri, StreamingFeatureFetcher streamingEventHandler);

        void StopStreaming();
    }
}
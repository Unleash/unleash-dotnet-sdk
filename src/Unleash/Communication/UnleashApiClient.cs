using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using Unleash.Events;
using Unleash.Internal;
using Unleash.Logging;
using Unleash.Metrics;
using Unleash.Streaming;

namespace Unleash.Communication
{
    internal class UnleashApiClient : IUnleashApiClient
    {

        private static readonly ILog Logger = LogProvider.GetLogger(typeof(UnleashApiClient));

        private readonly HttpClient httpClient;
        private readonly UnleashApiClientRequestHeaders clientRequestHeaders;
        private readonly EventCallbackConfig eventConfig;
        private EventSource EventSource { get; set; }
        private StreamingFeatureFetcher StreamingEventHandler { get; set; }

        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private readonly string projectId;
        private int featureRequestsToSkip = 0;
        private int featureRequestsSkipped = 0;
        private int metricsRequestsToSkip = 0;
        private int metricsRequestsSkipped = 0;
        private readonly int[] backoffResponses =
            new int[]
                {
                    429,
                    500,
                    502,
                    503,
                    504
                };
        private readonly int[] configurationErrorResponses =
            new int[]
                {
                    401,
                    403,
                    404,
                };
        public UnleashApiClient(
            HttpClient httpClient,
            UnleashApiClientRequestHeaders clientRequestHeaders,
            EventCallbackConfig eventConfig,
            string projectId = null)
        {
            this.httpClient = httpClient;
            this.clientRequestHeaders = clientRequestHeaders;
            this.eventConfig = eventConfig;
            this.projectId = projectId;
        }

        public async Task<FetchTogglesResult> FetchToggles(string etag, CancellationToken cancellationToken, bool throwOnFail = false)
        {
            if (featureRequestsToSkip > featureRequestsSkipped)
            {
                featureRequestsSkipped++;
                return new FetchTogglesResult
                {
                    HasChanged = false,
                    Etag = null,
                };
            }
            featureRequestsSkipped = 0;

            string resourceUri = "client/features";
            if (!string.IsNullOrWhiteSpace(this.projectId))
                resourceUri += "?project=" + this.projectId;

            using (var request = new HttpRequestMessage(HttpMethod.Get, resourceUri))
            {
                SetRequestHeaders(request, clientRequestHeaders);
                request.Headers.TryAddWithoutValidation("Unleash-Interval", clientRequestHeaders.FetchTogglesInterval.TotalMilliseconds.ToString());

                if (EntityTagHeaderValue.TryParse(etag, out var etagHeaderValue))
                    request.Headers.IfNoneMatch.Add(etagHeaderValue);

                using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotModified)
                    {
                        return await HandleErrorResponse(response, resourceUri, throwOnFail);
                    }

                    return await HandleSuccessResponse(response, etag);
                }
            }
        }

        private async Task<FetchTogglesResult> HandleErrorResponse(HttpResponseMessage response, string resourceUri, bool shouldThrow = false)
        {
            if (backoffResponses.Contains((int)response.StatusCode))
            {
                Backoff(response);
            }

            if (configurationErrorResponses.Contains((int)response.StatusCode))
            {
                ConfigurationError(response, resourceUri);
            }

            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Logger.Trace(() => $"UNLEASH: Error {response.StatusCode} from server in '{nameof(FetchToggles)}': " + error);
            eventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, StatusCode = response.StatusCode, Resource = resourceUri });

            if (shouldThrow)
            {
                throw new UnleashException($"Unleash: {response.StatusCode} from server in '{nameof(FetchToggles)}': " + error);
            }

            return new FetchTogglesResult
            {
                HasChanged = false,
                Etag = null,
            };
        }
        private void Backoff(HttpResponseMessage response)
        {
            featureRequestsToSkip = Math.Min(10, featureRequestsToSkip + 1);
            Logger.Warn(() => $"UNLEASH: Backing off due to {response.StatusCode} from server in '{nameof(FetchToggles)}'.");
        }

        private void ConfigurationError(HttpResponseMessage response, string requestUri)
        {
            featureRequestsToSkip = 10;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Error(() => $"UNLEASH: Error when fetching toggles, {requestUri} responded NOT_FOUND (404) which means your API url most likely needs correction.'.");
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                Logger.Error(() => $"UNLEASH: Error when fetching toggles, {requestUri} responded FORBIDDEN (403) which means your API token is not valid.");
            }
            else
            {
                Logger.Error(() => $"UNLEASH: Configuration error due to {response.StatusCode} from server in '{nameof(FetchToggles)}'.");
            }
        }

        private async Task<FetchTogglesResult> HandleSuccessResponse(HttpResponseMessage response, string etag)
        {
            featureRequestsToSkip = Math.Max(0, featureRequestsToSkip - 1);

            var newEtag = response.Headers.ETag?.ToString();
            if (newEtag == etag || response.StatusCode == HttpStatusCode.NotModified)
            {
                return new FetchTogglesResult
                {
                    HasChanged = false,
                    Etag = newEtag,
                };
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(content))
            {
                return new FetchTogglesResult
                {
                    HasChanged = false
                };
            }

            // Success
            return new FetchTogglesResult
            {
                HasChanged = true,
                Etag = newEtag,
                State = content
            };
        }

        public async Task<bool> RegisterClient(ClientRegistration registration, CancellationToken cancellationToken)
        {
            const string requestUri = "client/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                registration.ConnectionId = clientRequestHeaders.ConnectionId;
                request.Content = new StringContent(JsonSerializer.Serialize(registration, options), Encoding.UTF8, "application/json");

                SetRequestHeaders(request, clientRequestHeaders);

                using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                        return true;

                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Logger.Trace(() => $"UNLEASH: Error {response.StatusCode} from request '{requestUri}' in '{nameof(UnleashApiClient)}': " + error);
                    eventConfig?.RaiseError(new ErrorEvent() { Resource = requestUri, ErrorType = ErrorType.Client, StatusCode = response.StatusCode });

                    return false;
                }
            }
        }

        public async Task<bool> SendMetrics(Yggdrasil.MetricsBucket metrics, CancellationToken cancellationToken)
        {
            if (metricsRequestsToSkip > metricsRequestsSkipped)
            {
                metricsRequestsSkipped++;
                return false;
            }

            metricsRequestsSkipped = 0;

            const string requestUri = "client/metrics";

            var clientMetrics = new ClientMetrics
            {
                AppName = clientRequestHeaders.AppName,
                InstanceId = clientRequestHeaders.InstanceTag,
                ConnectionId = clientRequestHeaders.ConnectionId,
                Bucket = metrics ?? new Yggdrasil.MetricsBucket(new Dictionary<string, Yggdrasil.FeatureCount>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                request.Content = new StringContent(JsonSerializer.Serialize(clientMetrics, options), Encoding.UTF8, "application/json");

                SetRequestHeaders(request, clientRequestHeaders);
                request.Headers.TryAddWithoutValidation("Unleash-Interval", clientRequestHeaders.SendMetricsInterval.TotalMilliseconds.ToString());
                using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
                    {
                        HandleMetricsSuccessResponse(response);
                        return true;
                    }

                    await HandleMetricsErrorResponse(response, requestUri);
                    return false;
                }
            }
        }

        public async Task StartStreamingAsync(
            Uri apiUri,
            StreamingFeatureFetcher streamingEventHandler
        )
        {
            const string requestUri = "client/streaming";
            StreamingEventHandler = streamingEventHandler;
            EventSource = new EventSource(
                Configuration.Builder(new Uri(apiUri, requestUri))
                .HttpRequestModifier((requestMessage) =>
                {
                    SetRequestHeaders(requestMessage, clientRequestHeaders);
                })
                .HttpClient(httpClient)
                .ReadTimeout(TimeSpan.FromSeconds(60))
                .ResponseStartTimeout(TimeSpan.FromSeconds(10))
                .Method(HttpMethod.Get)
                .Build()
            );
            EventSource.MessageReceived += streamingEventHandler.HandleMessage;
            EventSource.Error += streamingEventHandler.HandleError;
            await EventSource.StartAsync().ConfigureAwait(false);
        }

        public void StopStreaming()
        {
            if (EventSource == null)
                return;

            EventSource.MessageReceived -= StreamingEventHandler.HandleMessage;
            EventSource.Error -= StreamingEventHandler.HandleError;
            EventSource.Close();
            EventSource = null;
        }

        private async Task HandleMetricsErrorResponse(HttpResponseMessage response, string requestUri)
        {
            if (backoffResponses.Contains((int)response.StatusCode))
            {
                metricsRequestsToSkip = Math.Min(10, metricsRequestsToSkip + 1);
            }

            if (configurationErrorResponses.Contains((int)response.StatusCode))
            {
                metricsRequestsToSkip = 10;
            }

            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Logger.Trace(() => $"UNLEASH: Error {response.StatusCode} from request '{requestUri}' in '{nameof(UnleashApiClient)}': " + error);
            eventConfig?.RaiseError(new ErrorEvent() { Resource = requestUri, ErrorType = ErrorType.Client, StatusCode = response.StatusCode });
        }

        private void HandleMetricsSuccessResponse(HttpResponseMessage response)
        {
            metricsRequestsToSkip = Math.Max(0, metricsRequestsToSkip - 1);
        }

        private static void SetRequestHeaders(HttpRequestMessage requestMessage, UnleashApiClientRequestHeaders headers)
        {
            const string userAgentHeader = "User-Agent";
            const string instanceIdHeader = "UNLEASH-INSTANCEID";

            const string identifyConnectionHeader = "unleash-connection-id";
            const string identifyAppNameHeader = "unleash-appname";
            const string identifySdkHeader = "unleash-sdk";

            const string supportedSpecVersionHeader = "Unleash-Client-Spec";
            requestMessage.Headers.TryAddWithoutValidation(userAgentHeader, headers.AppName);
            requestMessage.Headers.TryAddWithoutValidation(instanceIdHeader, headers.InstanceTag);
            requestMessage.Headers.TryAddWithoutValidation(supportedSpecVersionHeader, headers.SupportedSpecVersion);

            requestMessage.Headers.TryAddWithoutValidation(identifyAppNameHeader, headers.AppName);
            requestMessage.Headers.TryAddWithoutValidation(identifySdkHeader, headers.SdkVersion);

            SetCustomHeaders(requestMessage, headers.CustomHttpHeaders);
            SetCustomHeaders(requestMessage, headers.CustomHttpHeaderProvider?.CustomHeaders);

            requestMessage.Headers.TryAddWithoutValidation(identifyConnectionHeader, headers.ConnectionId);
        }

        private static void SetCustomHeaders(HttpRequestMessage requestMessage, Dictionary<string, string> headers)
        {
            if (headers == null)
                return;

            if (headers.Count == 0)
                return;

            foreach (var header in headers)
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}

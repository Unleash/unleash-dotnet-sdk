using Unleash.Communication;
using Unleash.Metrics;
using Unleash.Streaming;
using Yggdrasil;

namespace Unleash.Tests.Mock
{
    internal class MockApiClient : IUnleashApiClient
    {
        private static readonly string State = @"
        {
            ""version"": 2,
            ""features"": [
              {
                ""name"": ""one-enabled"",
                ""type"": ""release"",
                ""enabled"": true,
                ""impressionData"": false,
                ""strategies"": [
                  {
                    ""name"": ""userWithId"",
                    ""parameters"": {
                      ""userIds"": ""userA""
                    }
                  }
                ],
                ""variants"": [
                  {
                    ""name"": ""Aa"",
                    ""weight"": 33
                  },
                  {
                    ""name"": ""Aa"",
                    ""weight"": 33
                  },
                  {
                    ""name"": ""Ab"",
                    ""weight"": 34,
                    ""overrides"": [
                      {
                        ""contextName"": ""context"",
                        ""values"": [""a"", ""b""]
                      }
                    ]
                  }
                ]
              },
              {
                ""name"": ""one-disabled"",
                ""type"": ""release"",
                ""enabled"": false,
                ""impression-data"": false,
                ""strategies"": [
                  {
                    ""name"": ""userWithId"",
                    ""parameters"": {
                      ""userIds"": ""userB""
                    }
                  }
                ]
              }
            ]
        }";

        public Task<FetchTogglesResult> FetchToggles(string etag, CancellationToken cancellationToken, bool throwOnFail = false)
        {
            return Task.Run(async delegate
            {
                await Task.Delay(200);
                return new FetchTogglesResult
                {
                    HasChanged = true,
                    Etag = "etag",
                    State = State
                };
            });
        }

        public Task<bool> RegisterClient(ClientRegistration registration, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SendMetrics(MetricsBucket metricsBucket, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task StartStreamingAsync(Uri apiUri, StreamingFeatureFetcher streamingEventHandler)
        {
            throw new NotImplementedException();
        }

        public void StopStreaming()
        {
        }
    }
}
using System.Net;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Unleash.Communication;
using Unleash.Metrics;
using Yggdrasil;

namespace Unleash.Tests.Communication
{
    public class UnleashRequestBodyUnitTest
    {
        private const string ExpectedConnectionId = "00000000-0000-4000-a000-000000000000";

        private MockHttpMessageHandler _messageHandler;
        private IUnleashApiClient _apiClient;

        [SetUp]
        public void SetupTest()
        {
            _messageHandler = new MockHttpMessageHandler();
            _apiClient = CreateApiClient();
        }

        private IUnleashApiClient CreateApiClient()
        {
            var requestParams = new UnleashApiClientRequestHeaders
            {
                ConnectionId = ExpectedConnectionId,
            };

            var httpClient = new HttpClient(_messageHandler)
            {
                BaseAddress = new Uri("http://example.com")
            };

            return new UnleashApiClient(httpClient, requestParams, null);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Calls { get; } = new List<HttpRequestMessage>();
            public List<string> RequestBodies { get; } = new List<string>();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls.Add(request);
                if (request.Content != null)
                {
                    string requestBody = await request.Content.ReadAsStringAsync();
                    RequestBodies.Add(requestBody);
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        [Test]
        public async Task Should_Send_Correct_ConnectionId_In_Body_When_Sending_Metrics_And_Registering()
        {
            var engine = new YggdrasilEngine();
            var metricsResult = await _apiClient.SendMetrics(engine.GetMetrics(), CancellationToken.None);
            var registerResult = await _apiClient.RegisterClient(new ClientRegistration(), CancellationToken.None);

            _messageHandler.Calls.Count.Should().Be(2);
            _messageHandler.RequestBodies.All(body => MatchesExpectedConnectionId(body)).Should().BeTrue();
        }

        private bool MatchesExpectedConnectionId(string requestBody)
        {
            using (JsonDocument doc = JsonDocument.Parse(requestBody))
            {
                return doc.RootElement.TryGetProperty("connectionId", out JsonElement connectionIdElement) &&
                       connectionIdElement.ValueKind == JsonValueKind.String &&
                       connectionIdElement.GetString() == ExpectedConnectionId;
            }
        }
    }
}

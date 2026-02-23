using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Unleash.Communication;
using Yggdrasil;

namespace Unleash.Tests.Communication
{
    public class CustomHeadersUnitTest
    {
        private static string FEATURES_PATH = Path.Combine(TestContext.CurrentContext.TestDirectory, "App_Data", "features-v1-empty.json");

        private IUnleashApiClient CreateApiClient()
        {
            var requestHeaders = new UnleashApiClientRequestHeaders
            {
                AppName = "api-test-client",
                InstanceTag = "instance1",
                ConnectionId = "00000000-0000-4000-a000-000000000000",
                SdkVersion = "unleash-client-mock:0.0.0",
                CustomHttpHeaders = httpHeaders,
                CustomHttpHeaderProvider = httpHeadersProvider,
                FetchTogglesInterval = TimeSpan.FromSeconds(1),
                SendMetricsInterval = TimeSpan.FromSeconds(2),
            };

            var httpClient = new HttpClient(messageHandler)
            {
                BaseAddress = new Uri("http://example.com")
            };
            var client = new UnleashApiClient(httpClient, requestHeaders, null);
            return client;
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> calls
            {
                get;
                set;
            } = new List<HttpRequestMessage>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                calls.Add(request);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(File.ReadAllText(FEATURES_PATH))
                });
            }
        }

        private IUnleashApiClient api
        {
            get => TestExecutionContext.CurrentContext.CurrentTest.Properties.Get("api") as IUnleashApiClient;
            set => TestExecutionContext.CurrentContext.CurrentTest.Properties.Set("api", value);
        }


        Dictionary<string, string> httpHeaders
        {
            get => TestExecutionContext.CurrentContext.CurrentTest.Properties.Get("httpHeaders") as Dictionary<string, string>;
            set => TestExecutionContext.CurrentContext.CurrentTest.Properties.Set("httpHeaders", value);
        }


        IUnleashCustomHttpHeaderProvider httpHeadersProvider
        {
            get => TestExecutionContext.CurrentContext.CurrentTest.Properties.Get("httpHeadersProvider") as IUnleashCustomHttpHeaderProvider;
            set => TestExecutionContext.CurrentContext.CurrentTest.Properties.Set("httpHeadersProvider", value);
        }

        MockHttpMessageHandler messageHandler
        {
            get => TestExecutionContext.CurrentContext.CurrentTest.Properties.Get("messageHandler") as MockHttpMessageHandler;
            set => TestExecutionContext.CurrentContext.CurrentTest.Properties.Set("messageHandler", value);
        }

        [SetUp]
        public void SetupTest()
        {
            messageHandler = new MockHttpMessageHandler();
        }

        [Test]
        public async Task StaticHttpHeaders()
        {
            httpHeaders = new Dictionary<string, string>
            {
                {"expectedHeader1", "expectedValue1"},
                {"expectedHeader2", "expectedValue2"}
            };
            api = CreateApiClient();
            var engine = new YggdrasilEngine();

            var etag = "";
            await api.FetchToggles(etag, CancellationToken.None);
            await api.RegisterClient(new Unleash.Metrics.ClientRegistration(), CancellationToken.None);
            await api.SendMetrics(engine.CollectMetricsBucket(), CancellationToken.None);

            messageHandler.calls.Count.Should().Be(3);
            foreach (var call in messageHandler.calls)
            {
                call.Headers.Should().ContainEquivalentOf(new KeyValuePair<string, IEnumerable<string>>("User-Agent", new string[] { "api-test-client" }));
                call.Headers.Should().ContainEquivalentOf(new KeyValuePair<string, IEnumerable<string>>("expectedHeader1", new string[] { "expectedValue1" }));
                call.Headers.Should().ContainEquivalentOf(new KeyValuePair<string, IEnumerable<string>>("expectedHeader2", new string[] { "expectedValue2" }));
            }
        }

        class UnleashCustomHttpHeaderProvider : IUnleashCustomHttpHeaderProvider
        {
            public Dictionary<string, string> CustomHeaders => new Dictionary<string, string>
            {
                {"expectedDynamicHeader1", "expectedDynamicValue1"},
                {"expectedDynamicHeader2", "expectedDynamicValue2"}
            };
        }

        [Test]
        public async Task DynamicHttpHeaders()
        {

            httpHeadersProvider = new UnleashCustomHttpHeaderProvider();
            api = CreateApiClient();
            var engine = new YggdrasilEngine();

            var etag = "";
            await api.FetchToggles(etag, CancellationToken.None);
            await api.RegisterClient(new Unleash.Metrics.ClientRegistration(), CancellationToken.None);
            await api.SendMetrics(engine.CollectMetricsBucket(), CancellationToken.None);

            messageHandler.calls.Count.Should().Be(3);
            foreach (var call in messageHandler.calls)
            {
                call.Headers.Should().ContainEquivalentOf(new KeyValuePair<string, IEnumerable<string>>("expectedDynamicHeader1", new string[] { "expectedDynamicValue1" }));
                call.Headers.Should().ContainEquivalentOf(new KeyValuePair<string, IEnumerable<string>>("expectedDynamicHeader2", new string[] { "expectedDynamicValue2" }));
            }
        }

        [Test]
        public async Task IdentificationHttpHeaders()
        {
            api = CreateApiClient();
            var engine = new YggdrasilEngine();

            var etag = "";
            await api.FetchToggles(etag, CancellationToken.None);
            await api.RegisterClient(new Unleash.Metrics.ClientRegistration(), CancellationToken.None);
            await api.SendMetrics(engine.CollectMetricsBucket(), CancellationToken.None);

            messageHandler.calls.Count.Should().Be(3);



            var fetchCall = messageHandler.calls[0];
            fetchCall.Headers.Should().ContainEquivalentOf(
                new KeyValuePair<string, IEnumerable<string>>("Unleash-Interval", new string[] { "1000" })
            );

            var metricsCall = messageHandler.calls[2];
            metricsCall.Headers.Should().ContainEquivalentOf(
                new KeyValuePair<string, IEnumerable<string>>("Unleash-Interval", new string[] { "2000" })
            );

            foreach (var call in messageHandler.calls)
            {
                call.Headers.Should().ContainEquivalentOf(
                    new KeyValuePair<string, IEnumerable<string>>("unleash-connection-id", new string[] { "00000000-0000-4000-a000-000000000000" })
                );
                call.Headers.Should().ContainEquivalentOf(
                    new KeyValuePair<string, IEnumerable<string>>("unleash-appname", new string[] { "api-test-client" })
                );
                call.Headers.Should().ContainEquivalentOf(
                    new KeyValuePair<string, IEnumerable<string>>("unleash-sdk", new string[] { "unleash-client-mock:0.0.0" })
                );
            }
        }

    }
}

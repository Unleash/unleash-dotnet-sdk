using NUnit.Framework;
using RichardSzalay.MockHttp;
using Unleash.Metrics;

namespace Unleash.Tests.Communication
{
    public class UnleashApiClient_RegisterClient_Tests
    {
        private const string BASE_URL = "http://some-mock-url/api/client";

        [Test]
        public async Task RegisterClient_Success()
        {
            var (mockHttp, client) = MockHttpClient.MakeMockClient(BASE_URL);

            mockHttp.When($"{BASE_URL}/register")
                .WithPartialContent("\"appName\":\"SomeTestAppName\"")
                .WithPartialContent("\"interval\":1000")
                .WithPartialContent("\"sdkVersion\":\"1.0.1\"")
                .WithPartialContent("\"strategies\":[\"abc\"]")
                .WithPartialContent("specVersion")
                .WithPartialContent("platformName")
                .WithPartialContent("platformVersion")
                .WithPartialContent("\"yggdrasilVersion\":\"0.21.5\"")
                .Respond("application/json", "{ 'status': 'ok' }");

            var clientRegistration = new ClientRegistration()
            {
                AppName = "SomeTestAppName",
                Interval = 1000,
                SdkVersion = "1.0.1",
                Strategies = new List<string>
                {
                    "abc"
                }
            };

            var result = await client.RegisterClient(clientRegistration, CancellationToken.None);
            Assert.IsTrue(result);
        }
    }
}
using NUnit.Framework;
using RichardSzalay.MockHttp;
using NUnit.Framework.Internal;
using Yggdrasil;

namespace Unleash.Tests.Communication
{
    public class UnleashApiClient_SendMetrics_Tests
    {
        private const string BASE_URL = "http://some-mock-url/api/client";

        [Test]
        public async Task SendMetrics_Success()
        {
            var (mockHttp, client) = MockHttpClient.MakeMockClient(BASE_URL);

            mockHttp.When($"{BASE_URL}/metrics")
                .WithPartialContent("appName")
                .WithPartialContent("instanceId")
                .WithPartialContent("\"no\":1")
                .WithPartialContent("\"yes\":0")
                .WithPartialContent("specVersion")
                .WithPartialContent("platformName")
                .WithPartialContent("platformVersion")
                .WithPartialContent("\"yggdrasilVersion\":\"0.14.0\"")
                .Respond("application/json", "{ 'status': 'ok' }");

            var engine = new YggdrasilEngine();
            engine.IsEnabled("someTestToggle", new Context());

            var result = await client.SendMetrics(engine.GetMetrics(), CancellationToken.None);
            Assert.IsTrue(result);
        }
    }
}

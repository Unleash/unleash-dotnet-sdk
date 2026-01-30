namespace Unleash.Tests.Mock
{
    public class ConfigurableMessageHandlerMock : HttpMessageHandler
    {
        private Dictionary<string, HttpResponseMessage> configuredResponses = new Dictionary<string, HttpResponseMessage>();
        public List<HttpRequestMessage> SentMessages = new List<HttpRequestMessage>();


        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentMessages.Add(request);

            return Task.FromResult(configuredResponses[request.RequestUri.ToString()]);
        }

        public void Configure(string path, HttpResponseMessage response)
        {
            configuredResponses.Add(path, response);
        }
    }
}

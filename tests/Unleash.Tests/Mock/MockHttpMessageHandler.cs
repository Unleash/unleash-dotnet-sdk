using System.Text;

namespace Unleash.Tests.Mock
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> SentMessages { get; } = new List<HttpRequestMessage>();

        public string? ETagToReturn { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentMessages.Add(request);

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(ETagToReturn))
            {
                response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(ETagToReturn);
            }

            return Task.FromResult(response);
        }
    }
}
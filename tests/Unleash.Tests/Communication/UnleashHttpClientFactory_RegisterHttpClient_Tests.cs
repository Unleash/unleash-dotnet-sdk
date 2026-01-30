using NUnit.Framework;

namespace Unleash.Tests.Communication
{
    public class UnleashHttpClientFactory_RegisterHttpClient_Tests
    {
        private readonly Uri apiUri = new Uri("http://unleash.herokuapp.com/api/");

        private readonly Uri newApiUri = new Uri("http://unleash2.herokuapp2.com/api/");

        private readonly DefaultHttpClientFactory httpClientFactory = new DefaultHttpClientFactory();

        [Test]
        public void HttpClientFactory_Should_Create_Single_Instance_By_Dns_Success()
        {
            var httpClient = httpClientFactory.Create(apiUri);
            var expectedHashId = httpClient.GetHashCode();

            var newHttpClient = httpClientFactory.Create(apiUri);
            var actualHashId = newHttpClient.GetHashCode();

            Assert.That(actualHashId, Is.EqualTo(expectedHashId));
        }

        [Test]
        public void HttpClientFactory_Should_Create_One_Instance_Per_Dns_Success()
        {
            var httpClient = httpClientFactory.Create(apiUri);
            var expectedHashId = httpClient.GetHashCode();

            var newHttpClient = httpClientFactory.Create(newApiUri);
            var actualHashId = newHttpClient.GetHashCode();

            Assert.That(actualHashId, Is.Not.EqualTo(expectedHashId));
        }
    }
}

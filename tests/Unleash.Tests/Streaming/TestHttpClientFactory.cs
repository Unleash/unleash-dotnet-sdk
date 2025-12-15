namespace Unleash.Tests;

internal class TestHttpClientFactory : Unleash.IHttpClientFactory
{
    private HttpClient client;

    public TestHttpClientFactory(HttpClient client)
    {
        this.client = client;
    }

    public HttpClient Create(Uri unleashApiUri)
    {
        return client;
    }
}

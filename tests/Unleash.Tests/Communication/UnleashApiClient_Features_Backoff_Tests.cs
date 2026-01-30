using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using Unleash.Communication;
using Unleash.Internal;

public class UnleashApiClient_Features_Backoff_Tests : BaseBackoffTest
{
    [Test]
    public void Should_Not_Skip_First_Call()
    {
        // Arrange
        var messageHandler = new CountingConfigurableHttpMessageHandler
        (
            new List<HttpResponseMessage>
            {
                Ok
            }
        );
        var httpClient = new HttpClient(messageHandler);
        httpClient.BaseAddress = new Uri("http://localhost:8080/");

        var apiClient = new UnleashApiClient(
            httpClient,
            A.Fake<UnleashApiClientRequestHeaders>(),
            new EventCallbackConfig()
        );

        // Act
        var result = Task.Run(() => apiClient.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Assert
        messageHandler.CallCount.Should().Be(1);
    }

    [Test]
    public void NotModified_Should_Not_Skip_Next()
    {
        // Arrange
        var messageHandler = new CountingConfigurableHttpMessageHandler
        (
            new List<HttpResponseMessage>
            {
                NotModified,
                Ok
            }
        );
        var httpClient = new HttpClient(messageHandler);
        httpClient.BaseAddress = new Uri("http://localhost:8080/");

        var apiClient = new UnleashApiClient(
            httpClient,
            A.Fake<UnleashApiClientRequestHeaders>(),
            new EventCallbackConfig()
        );

        // Act
        var result = Task.Run(() => apiClient.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        var result2 = Task.Run(() => apiClient.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Assert
        messageHandler.CallCount.Should().Be(2);
    }

    [Test]
    public void Should_Skip_One_After_First_429()
    {
        // Arrange
        var messageHandler = new CountingConfigurableHttpMessageHandler
        (
            new List<HttpResponseMessage>
            {
                TooManyRequests,
                Ok
            }
        );
        var apiClient = GetClient(messageHandler);

        // Act 1
        var result = Task.Run(() => apiClient.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Assert 1
        messageHandler.CallCount.Should().Be(1);

        // Act 2
        var result2 = Task.Run(() => apiClient.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Assert 2
        messageHandler.CallCount.Should().Be(1);

        // Act 3
        var result3 = Task.Run(() => apiClient.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Assert 3
        messageHandler.CallCount.Should().Be(2);
    }

    [Test]
    public void Backoff_Caps_Out_At_10()
    {
        // Arrange
        var messageHandler = new CountingConfigurableHttpMessageHandler
        (
            new List<HttpResponseMessage>
            {
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                InternalServerError,
                Ok,
                Ok
            }
        );

        var client = GetClient(messageHandler);

        // Wind up call count to 10
        for (var i = 0; i < 55; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        messageHandler.CallCount.Should().Be(10);

        // Get the 11th bad response
        for (var i = 0; i < 11; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        messageHandler.CallCount.Should().Be(11);

        var resultAfter = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Make 10 attempts for the 11th bad response, these should be skipped
        for (var i = 0; i < 10; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        // Calls API on the 11th attempt
        messageHandler.CallCount.Should().Be(12);

        for (var i = 0; i < 9; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        // Shouldn't have changed
        messageHandler.CallCount.Should().Be(12);

        var resultAfter2 = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Should have made 1 additional attempt as part of decrease
        messageHandler.CallCount.Should().Be(13);
    }

    [Test]
    public void Backoff_Gradually_Decreases()
    {
        // Arrange
        var messageHandler = new CountingConfigurableHttpMessageHandler
        (
            new List<HttpResponseMessage>
            {
                TooManyRequests,
                InternalServerError,
                GatewayTimeout,
                TooManyRequests,
                ServiceUnavailable,
                BadGateway,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                TooManyRequests,
                Ok,
                Ok,
                Ok,
                Ok
            }
        );

        var client = GetClient(messageHandler);

        // Act
        for (var i = 0; i < 55; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        for (var i = 0; i < 11; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        // Still takes 11 fetches to get a new GET, this one will return 200 so the backoff should decrease
        messageHandler.CallCount.Should().Be(11);

        // Make 9 more fetches
        for (var i = 0; i < 9; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        // Backoff should be at 9, so this should not have increased CallCount
        messageHandler.CallCount.Should().Be(11);

        // Make the 10th fetch, this one should cause a GET
        var resultAfter2 = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        // Should have made 1 additional attempt
        messageHandler.CallCount.Should().Be(12);

        // Make 8 more fetches
        for (var i = 0; i < 8; i++)
        {
            var loopResult = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }

        // Backoff should be at 8, so this should not have increased CallCount
        messageHandler.CallCount.Should().Be(12);

        // Make the 9th fetch, this one should cause a GET
        var resultAfter3 = Task.Run(() => client.FetchToggles("etag", CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        messageHandler.CallCount.Should().Be(13);
    }
}
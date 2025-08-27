using Unleash;
using Unleash.Streaming;
using Unleash.Communication;
using NUnit.Framework;
using Unleash.Metrics;
using Yggdrasil;
using Unleash.Scheduling;
using LaunchDarkly.EventSource;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NUnit.Framework.Internal;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

internal class MockedTaskManager : Unleash.Scheduling.IUnleashScheduledTaskManager
{
    public void Configure(IEnumerable<IUnleashScheduledTask> tasks, CancellationToken cancellationToken)
    {
    }

    public void Dispose()
    {
        // Mock dispose method
    }
}

internal class StubbedApiClient : IUnleashApiClient
{
    public StreamingFeatureFetcher StreamingEventHandler { get; private set; }

    public Task<FetchTogglesResult> FetchToggles(string etag, CancellationToken cancellationToken, bool throwOnFail = false)
    {
        return Task.FromResult(new FetchTogglesResult());
    }

    public Task<bool> RegisterClient(ClientRegistration registration, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task<bool> SendMetrics(MetricsBucket metrics, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task StartStreamingAsync(Uri apiUri, StreamingFeatureFetcher streamingEventHandler)
    {
        StreamingEventHandler = streamingEventHandler;
        return Task.CompletedTask;
    }

    public void StopStreaming()
    {
    }
}

public class StreamingFeatureFetcherTests
{
    [Test]
    public void Handles_Messages()
    {
        // Arrange
        var apiClient = new StubbedApiClient();
        var uri = new Uri("http://example.com/streaming");
        var settings = new UnleashSettings
        {
            UnleashApiClient = apiClient,
            UnleashApi = uri,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new MockedTaskManager(),
            ExperimentalUseStreaming = true,
        };
        var unleash = new DefaultUnleash(settings);
        var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";

        // Act
        apiClient.StreamingEventHandler.HandleMessage(null, new MessageReceivedEventArgs(new MessageEvent("unleash-connected", payload, uri)));
        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
    }

    [Test]
    public async Task Receives_Updated_Events_From_Sse_Server()
    {
        // Arrange
        var updated = false;
        var server = new TestServer(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddRouting();
        })
        .Configure(app =>
        {
            app.UseRouter(router =>
            {
                router.MapGet("/streaming", async context =>
                {
                    var updateData = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\"feature\":{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}}]}";
                    context.Response.Headers["Content-Type"] = "text/event-stream";
                    await context.Response.WriteAsync("event: unleash-updated\n");
                    await context.Response.WriteAsync($"data: {updateData}\n\n");
                    await context.Response.Body.FlushAsync();

                });
            });
        }));

        var client = server.CreateClient();
        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/streaming");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new MockedTaskManager(),
            ExperimentalUseStreaming = true,
        };

        // Act
        var unleash = new DefaultUnleash(settings);
        unleash.ConfigureEvents(events =>
        {
            events.TogglesUpdatedEvent = ev => { updated = true; };
        });
        var timer = Stopwatch.StartNew();
        while (!updated && timer.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2));
        }
        timer.Stop();

        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
    }

    [Test]
    public async Task Receives_Successive_Events_From_Sse_Server()
    {
        // Arrange
        var updated = 0;
        var server = new TestServer(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddRouting();
        })
        .Configure(app =>
        {
            app.UseRouter(router =>
            {
                router.MapGet("/streaming", async context =>
                {
                    var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":false,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
                    var updateData = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\"feature\":{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}}]}";
                    context.Response.Headers["Content-Type"] = "text/event-stream";
                    await context.Response.WriteAsync("event: unleash-connected\n");
                    await context.Response.WriteAsync($"data: {payload}\n\n");
                    await context.Response.Body.FlushAsync();
                    await context.Response.WriteAsync("event: unleash-updated\n");
                    await context.Response.WriteAsync($"data: {updateData}\n\n");
                    await context.Response.Body.FlushAsync();

                });
            });
        }));

        var client = server.CreateClient();
        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/streaming");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new MockedTaskManager(),
            ExperimentalUseStreaming = true,
        };

        // Act
        var unleash = new DefaultUnleash(settings);
        unleash.ConfigureEvents(events =>
        {
            events.TogglesUpdatedEvent = ev => { updated++; };
        });
        var timer = Stopwatch.StartNew();
        while (updated < 2 && timer.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2));
        }
        timer.Stop();

        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
    }

    [Test]
    public async Task Receives_Hydration_Events_From_Sse_Server()
    {
        // Arrange


        var updated = false;
        var server = new TestServer(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddRouting();
        })
        .Configure(app =>
        {
            app.UseRouter(router =>
            {
                router.MapGet("/streaming", async context =>
                {
                    var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
                    context.Response.Headers["Content-Type"] = "text/event-stream";
                    await context.Response.WriteAsync("event: unleash-connected\n");
                    await context.Response.WriteAsync($"data: {payload}\n\n");
                    await context.Response.Body.FlushAsync();
                });
            });
        }));

        var client = server.CreateClient();
        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/streaming");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new MockedTaskManager(),
            ExperimentalUseStreaming = true,
        };

        // Act
        var unleash = new DefaultUnleash(settings);
        unleash.ConfigureEvents(events =>
        {
            events.TogglesUpdatedEvent = ev => { updated = true; };
        });
        var timer = Stopwatch.StartNew();
        while (!updated && timer.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2));
        }
        timer.Stop();

        var enabled = unleash.IsEnabled("deltaFeature");
        var backupPath = settings.GetFeatureToggleFilePath();
        var fileContent = File.ReadAllText(backupPath);

        // Assert
        Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
        Assert.NotNull(fileContent, "File content should not be null");
        Assert.IsTrue(fileContent.StartsWith('{'));
        Assert.IsTrue(fileContent.IndexOf("deltaFeature") > -1, "Feature flag not present in engine state");
    }
}

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

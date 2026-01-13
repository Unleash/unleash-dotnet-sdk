using Unleash;
using NUnit.Framework;
using LaunchDarkly.EventSource;
using Microsoft.AspNetCore.Http;
using NUnit.Framework.Internal;
using System.Diagnostics;
using Unleash.Tests.Mock;
using static Unleash.Tests.StreamingServer;

namespace Unleash.Tests;

public class StreamingFeatureFetcherTests
{
    [Test]
    public async Task Handles_Messages()
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
            ScheduledTaskManager = new NoOpTaskManager(),
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
        var updateData = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\"feature\":{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}}]}";

        var client = GetStreamingTestServerClient(async context =>
        {
            await WriteEvents(context, new List<ServerSentEvent>()
            {
                new ServerSentEvent { Id = "2", Payload = updateData, Name = "unleash-updated" }
            });
        });

        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new NoOpTaskManager(),
            ExperimentalUseStreaming = true,
            UnleashApi = uri
        };

        // Act
        var unleash = new DefaultUnleash(settings, callback: events =>
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
    public async Task Reconnects_On_Bad_Event()
    {
        var requestEventIdHeaders = new List<string>();
        var firstSent = false;

        var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":false,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
        var badUpdate = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\" and then some junk that isn't valid JSON";
        var updatedPayload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":2,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";

        var client = GetStreamingTestServerClient(async context =>
        {
            var lastEventIdHeader = context.Request.Headers["last-event-id"];
            requestEventIdHeaders.Add(lastEventIdHeader);

            if (!firstSent)
            {
                firstSent = true;
                await WriteEvents(context, new List<ServerSentEvent>()
                {
                    new ServerSentEvent { Id = "1", Payload = payload, Name = "unleash-connected" },
                    new ServerSentEvent { Id = "2", Payload = badUpdate, Name = "unleash-updated" }
                });
            }
            else
            {
                await WriteEvents(context, new List<ServerSentEvent>()
                {
                    new ServerSentEvent { Id = "2", Payload = updatedPayload, Name = "unleash-connected" }
                });
            }
        });

        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new NoOpTaskManager(),
            ExperimentalUseStreaming = true,
            UnleashApi = uri,
        };

        // Act
        var unleash = new DefaultUnleash(settings);
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2));
        }
        timer.Stop();

        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        Assert.That(enabled, Is.EqualTo(true));
        Assert.That(requestEventIdHeaders.Count, Is.EqualTo(2));
        Assert.IsTrue(string.IsNullOrWhiteSpace(requestEventIdHeaders[0]));
        Assert.IsTrue(string.IsNullOrWhiteSpace(requestEventIdHeaders[1]));
    }

    [Test]
    public async Task Reconnects_When_Server_Connection_Resets()
    {
        var requestEventIdHeaders = new List<string>();
        var firstSent = false;

        var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":false,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
        var updatedPayload = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\"feature\":{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}}]}";

        var server = GetStreamingTestServer(async context =>
        {
            var lastEventIdHeader = context.Request.Headers["last-event-id"];
            requestEventIdHeaders.Add(lastEventIdHeader);

            if (!firstSent)
            {
                firstSent = true;
                await WriteEvents(context, new List<ServerSentEvent>()
                {
                    new ServerSentEvent { Id = "1", Payload = payload, Name = "unleash-connected" },
                });
            }
            else
            {
                await WriteEvents(context, new List<ServerSentEvent>()
                {
                    new ServerSentEvent { Id = "2", Payload = updatedPayload, Name = "unleash-updated" }
                });
            }
        });
        var client = server.CreateClient();
        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new NoOpTaskManager(),
            ExperimentalUseStreaming = true,
            UnleashApi = uri,
        };

        // Act
        var unleash = new DefaultUnleash(settings);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        // Simulate a connection drop
        await server.Host.StopAsync();
        await server.Host.StartAsync();

        await Task.Delay(TimeSpan.FromMilliseconds(1000));

        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        //Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
        Assert.That(requestEventIdHeaders.Count, Is.EqualTo(2));
        Assert.IsTrue(string.IsNullOrWhiteSpace(requestEventIdHeaders[0]));
        Assert.That(requestEventIdHeaders[1], Is.EqualTo("1"));
    }

    [Test]
    public async Task Receives_Successive_Events_From_Sse_Server()
    {
        var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":false,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
        var updateData = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\"feature\":{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}}]}";
        // Arrange
        var updated = 0;
        var client = GetStreamingTestServerClient(async context =>
        {
            await WriteEvents(context, new List<ServerSentEvent>()
            {
                new ServerSentEvent { Id = "1", Payload = payload, Name = "unleash-connected" },
                new ServerSentEvent { Id = "2", Payload = updateData, Name = "unleash-updated" }
            });
        });

        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new NoOpTaskManager(),
            ExperimentalUseStreaming = true,
            UnleashApi = uri,
        };

        // Act
        var unleash = new DefaultUnleash(settings, callback: events =>
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
        var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
        var client = GetStreamingTestServerClient(async context =>
        {
            await WriteEvents(context, new List<ServerSentEvent>()
            {
                new ServerSentEvent { Id = "1", Payload = payload, Name = "unleash-connected" },
            });
        });

        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com");
        var fileSystem = new MockFileSystem();

        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            FileSystem = fileSystem,
            ScheduledTaskManager = new NoOpTaskManager(),
            ExperimentalUseStreaming = true,
            UnleashApi = uri,
        };

        // Act
        var unleash = new DefaultUnleash(settings, callback: events =>
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
        var fileContent = fileSystem.ReadAllText(fileSystem.ListFiles().Find(f => f.Contains("toggles"))!);
        // Assert
        Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
        Assert.NotNull(fileContent, "File content should not be null");
        Assert.IsTrue(fileContent.StartsWith('{'));
        Assert.IsTrue(fileContent.IndexOf("deltaFeature") > -1, "Feature flag not present in engine state");
    }
}

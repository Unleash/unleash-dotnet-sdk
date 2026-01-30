using NUnit.Framework;
using LaunchDarkly.EventSource;
using NUnit.Framework.Internal;
using System.Diagnostics;
using Unleash.Tests.Mock;
using static Unleash.Tests.StreamingServer;

namespace Unleash.Tests;

public class StreamingFeatureFetcherTests
{
    private object GetPollingState()
    {
        return new
        {
            version = 2,
            features = new[] {
                    new {
                        name = "deltaFeature",
                        type = "release",
                        enabled = true,
                        project = "DavidTest",
                        stale = false,
                        strategies = new string[] {},
                        variants = new string[] {},
                        description = (string?)null,
                        impressionData = false
                    }
                }
        };
    }

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
            await WriteEvents(context, 200, new List<ServerSentEvent>()
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
                await WriteEvents(context, 200, new List<ServerSentEvent>()
                {
                    new ServerSentEvent { Id = "1", Payload = payload, Name = "unleash-connected" },
                    new ServerSentEvent { Id = "2", Payload = badUpdate, Name = "unleash-updated" }
                });
            }
            else
            {
                await WriteEvents(context, 200, new List<ServerSentEvent>()
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
        Assert.That(requestEventIdHeaders.Count, Is.GreaterThanOrEqualTo(2));
        Assert.IsTrue(string.IsNullOrWhiteSpace(requestEventIdHeaders[0]));
        Assert.IsTrue(string.IsNullOrWhiteSpace(requestEventIdHeaders[1]));
    }

    [Test]
    public async Task Switches_To_Polling_When_Too_Many_Http_Errors()
    {
        var streamingErrors = 0;
        var pollingSent = false;
        var updated = 0;
        TaskCompletionSource updatesDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var server = GetStreamingPollingTestServer(
            async context =>
            {
                streamingErrors++;
                await WriteEvents(context, 500, new List<ServerSentEvent>());
            },
            async context =>
            {
                pollingSent = true;
                updatesDone.SetResult();
                await WriteState(context, 200, GetPollingState());
            }
        );
        var client = server.CreateClient();
        var clientFactory = new TestHttpClientFactory(client);

        var uri = new Uri("http://example.com/");
        var settings = new UnleashSettings
        {
            HttpClientFactory = clientFactory,
            AppName = "TestApp",
            InstanceTag = "TestInstance",
            ScheduledTaskManager = new RunFeaturePollingOnceTaskManager(),
            ExperimentalUseStreaming = true,
            UnleashApi = uri,
            SendMetricsInterval = null,
        };

        // Act
        var unleash = new DefaultUnleash(settings, callback: events =>
        {
            events.TogglesUpdatedEvent = ev => { updated++; };
        });

        await updatesDone.Task;
        await Task.Delay(100);
        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        Assert.That(streamingErrors, Is.EqualTo(5), "Has not attempted to connect 5 times before failing over");
        Assert.That(updated, Is.EqualTo(1), "Has not received a polling update");
        Assert.That(pollingSent == true, "Polling endpoint has not called");
        Assert.IsTrue(enabled, "Feature should be enabled after handling the message.");
    }

    [Test]
    public async Task Receives_Successive_Events_From_Sse_Server()
    {
        var payload = "{\"events\":[{\"type\":\"hydration\",\"eventId\":1,\"features\":[{\"name\":\"deltaFeature\",\"enabled\":false,\"strategies\":[],\"variants\":[]}],\"segments\":[]}]}";
        var updateData = "{\"events\":[{\"type\":\"feature-updated\",\"eventId\":2,\"feature\":{\"name\":\"deltaFeature\",\"enabled\":true,\"strategies\":[],\"variants\":[]}}]}";
        // Arrange
        var updated = 0;
        var sent = false;
        var client = GetStreamingTestServerClient(async context =>
        {
            if (!sent)
            {
                sent = true;
                await WriteEvents(context, 200, new List<ServerSentEvent>()
                {
                    new ServerSentEvent { Id = "1", Payload = payload, Name = "unleash-connected" },
                    new ServerSentEvent { Id = "2", Payload = updateData, Name = "unleash-updated" }
                });
            }
            else
            {
                await WriteEvents(context, 500, new List<ServerSentEvent>());
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

        await Task.Delay(TimeSpan.FromMilliseconds(2000));
        var enabled = unleash.IsEnabled("deltaFeature");

        // Assert
        Assert.That(updated, Is.EqualTo(2), "Not had 2 feature update events");
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
            await WriteEvents(context, 200, new List<ServerSentEvent>()
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

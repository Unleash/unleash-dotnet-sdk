using NUnit.Framework;
using Unleash.Internal;
using Unleash.Scheduling;
using Yggdrasil;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Unleash.Communication;
using System.Text.Json.Nodes;

namespace Unleash.Tests.Internal.ImpactMetrics
{
    public class ImpactMetricsTests
    {
        UnleashConfig GetConfig(Func<HttpContext, Task> requestAction)
        {
            var client = GetMetricsHttpClient(requestAction);
            return new UnleashConfig
            {
                Engine = new YggdrasilEngine(),
                AppName = "my-test-app",
                UnleashApi = new Uri("http://example.com/"),
                ApiClient = new UnleashApiClient(client, new UnleashApiClientRequestHeaders(), new EventCallbackConfig()),
                CancellationToken = new CancellationTokenSource().Token,
                SendMetricsInterval = TimeSpan.FromSeconds(1),
                Environment = "production"
            };
        }

        HttpClient GetMetricsHttpClient(Func<HttpContext, Task> requestAction)
        {
            return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
                {
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseRouter(router =>
                    {
                        router.MapPost("client/metrics", async context => await requestAction(context));
                    });
                })).CreateClient();
        }

        [Test]
        public async Task Sends_Counter_Gauge_And_Histogram_Metrics_In_Payload()
        {
            var expected_labels = @"{ ""environment"": ""production"", ""appName"": ""my-test-app"" }";
            var expectedPayload = JsonNode.Parse(
                @"
                    [
                        {
                            ""name"": ""purchases"",
                            ""help"": ""Number of purchases"",
                            ""type"": ""counter"",
                            ""samples"": [{ ""labels"":  " + expected_labels + @", ""value"": 1 }]
                        },
                        {
                            ""name"": ""temperature"",
                            ""help"": ""Current temperature"",
                            ""type"": ""gauge"",
                            ""samples"": [{ ""labels"":  " + expected_labels + @", ""value"": 23.5 }]
                        },
                        {
                            ""name"": ""latency"",
                            ""help"": ""Request latency"",
                            ""type"": ""histogram"",
                            ""samples"": [
                                {
                                    ""labels"":  " + expected_labels + @",
                                    ""count"": 1,
                                    ""sum"": 0.3,
                                    ""buckets"": [
                                        { ""le"": 0.1, ""count"": 0 },
                                        { ""le"": 0.5, ""count"": 1 },
                                        { ""le"": 1.0, ""count"": 1 },
                                        { ""le"": ""+Inf"", ""count"": 1 }
                                    ]
                                }
                            ]
                        }
                    ]");

            TaskCompletionSource<string> updatesDone = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var config = GetConfig(async context =>
            {
                using var streamReader = new StreamReader(context.Request.Body);
                updatesDone.SetResult(await streamReader.ReadToEndAsync().ConfigureAwait(false));
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("").ConfigureAwait(false);
                await context.Response.CompleteAsync().ConfigureAwait(false);
                return;
            });

            //var enabled = config.Engine.IsEnabled("toggle-1", new Context());
            var metricsTask = new ClientMetricsBackgroundTask(config);
            var impactMetrics = new Unleash.Internal.ImpactMetrics(config);

            impactMetrics.DefineCounter("purchases", "Number of purchases");
            impactMetrics.IncrementCounter("purchases", 1);

            impactMetrics.DefineGauge("temperature", "Current temperature");
            impactMetrics.UpdateGauge("temperature", 23.5);

            impactMetrics.DefineHistogram("latency", "Request latency", new[] { 0.1, 0.5, 1.0 });
            impactMetrics.ObserveHistogram("latency", 0.3);
            await metricsTask.ExecuteAsync(config.CancellationToken).ConfigureAwait(false);
            var body = await updatesDone.Task.ConfigureAwait(false);
            var metrics = JsonNode.Parse(body)!["impactMetrics"]!;
            Assert.True(JsonNode.DeepEquals(expectedPayload, metrics));

            /* For debugging and comparing output with expectations when this test fails
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            Assert.AreEqual(expectedPayload!.ToJsonString(options), metrics!.ToJsonString(options));
            */
        }

        [Test]
        public async Task Returns_Metrics_To_Engine_On_Failed_Send()
        {
            var expected_labels = @"{ ""environment"": ""production"", ""appName"": ""my-test-app"" }";
            var expectedPayload = JsonNode.Parse(
                @"
                    [
                        {
                            ""name"": ""purchases"",
                            ""help"": ""Number of purchases"",
                            ""type"": ""counter"",
                            ""samples"": [{ ""labels"":  " + expected_labels + @", ""value"": 3 }]
                        }
                    ]");

            TaskCompletionSource<string> updatesDone = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            int called = 0;
            var actions = new Func<HttpContext, Task>[]
            {
                async context =>
                    {
                        context.Response.StatusCode = 429;
                        await context.Response.WriteAsync("").ConfigureAwait(false);
                        await context.Response.CompleteAsync().ConfigureAwait(false);
                        return;
                    },
                async context =>
                    {
                        using var streamReader = new StreamReader(context.Request.Body);
                        updatesDone.SetResult(await streamReader.ReadToEndAsync().ConfigureAwait(false));
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("").ConfigureAwait(false);
                        await context.Response.CompleteAsync().ConfigureAwait(false);
                        return;
                    }
            };
            var config = GetConfig(async context =>
                    {
                        await actions[called](context);
                        called++;
                        return;
                    }
            );

            var metricsTask = new ClientMetricsBackgroundTask(config);
            var impactMetrics = new Unleash.Internal.ImpactMetrics(config);

            impactMetrics.DefineCounter("purchases", "Number of purchases");
            impactMetrics.IncrementCounter("purchases", 1);
            impactMetrics.IncrementCounter("purchases", 1);
            try
            {
                await metricsTask.ExecuteAsync(config.CancellationToken).ConfigureAwait(false);
            }
            catch { }
            impactMetrics.IncrementCounter("purchases", 1);
            await metricsTask.ExecuteAsync(config.CancellationToken).ConfigureAwait(false);
            // The above call fails before it starts due to metrics backoff
            await metricsTask.ExecuteAsync(config.CancellationToken).ConfigureAwait(false);

            var body = await updatesDone.Task.ConfigureAwait(false);
            var metrics = JsonNode.Parse(body)!["impactMetrics"]!;
            Assert.True(JsonNode.DeepEquals(expectedPayload, metrics));
        }
    }
}
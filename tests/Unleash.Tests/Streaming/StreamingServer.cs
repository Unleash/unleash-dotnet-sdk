using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Unleash.Tests;

internal static class StreamingServer
{
    /// <summary>
    /// Creates a TestServer for SSE and returns it
    /// </summary>
    /// <param name="requestAction">Async Func MapGet callback that takes an HttpContext and processes it</param>
    /// <returns>A TestServer instance</returns>
    public static TestServer GetStreamingTestServer(Func<HttpContext, Task> requestAction)
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
                router.MapGet("client/streaming", async context => await requestAction(context));
            });
        }));
    }

    /// <summary>
    /// Creates a TestServer for SSE and returns an HttpClient to it
    /// </summary>
    /// <param name="requestAction">Async Func MapGet callback that takes an HttpContext and processes it</param>
    /// <returns>An HttpClient instance</returns>
    public static HttpClient GetStreamingTestServerClient(Func<HttpContext, Task> requestAction)
    {
        var server = GetStreamingTestServer(requestAction);
        return server.CreateClient();
    }

    /// <summary>
    /// Writes a single event, sets content header
    /// </summary>
    /// <param name="context">Context to get the response object from</param>
    /// <param name="events">The SSEs to send</param>
    /// <returns>Async task that can be awaited</returns>
    public static async Task WriteEvents(HttpContext context, List<ServerSentEvent> events)
    {
        context.Response.Headers["Content-Type"] = "text/event-stream";
        for (var i = 0; i < events.Count; i++)
        {
            await context.Response.WriteAsync($"event: {events[i].Name}\n");
            await context.Response.WriteAsync($"data: {events[i].Payload}\n");
            await context.Response.WriteAsync($"id: {events[i].Id}\n\n");
            await context.Response.Body.FlushAsync();
        }
    }
}
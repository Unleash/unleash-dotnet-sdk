using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using static Unleash.Tests.Specifications.TestFactory;
using Unleash.Tests.Mock;
using Unleash.Internal;
using Unleash.Scheduling;

namespace Unleash.Tests.Integration
{
    public class YggdrasilContextE2ETests
    {
        private string GetState()
        {
            return @"
            {
                ""version"": 2,
                ""features"": [
                    {
                        ""name"": ""hydration-test"",
                        ""type"": ""release"",
                        ""enabled"": true,
                        ""project"": ""DavidTest"",
                        ""stale"": false,
                        ""strategies"": [
                            {
                                ""name"": ""applicationHostname"",
                                ""constraints"": [],
                                ""parameters"": {
                                    ""hostNames"": ""unit-test""
                                },
                                ""variants"": []
                            }
                        ],
                        ""variants"": [],
                        ""description"": null,
                        ""impressionData"": false
                    }
                ]
            }";
        }

        [Test]
        public void Environment_Variable_Hostname_Is_Set_Is_Enabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("hostname", "unit-test");
            var appname = "endpoint-test";
            var state = GetState();
            var unleash = CreateUnleash(appname, state);

            // Act
            var result = unleash.IsEnabled("hydration-test");
            unleash.Dispose();

            // Assert
            result.Should().BeTrue();
        }

        public static IUnleash CreateUnleash(string name, string state)
        {
            var fakeHttpClientFactory = A.Fake<IHttpClientFactory>();
            var fakeHttpMessageHandler = new TestHttpMessageHandler();
            var httpClient = new HttpClient(fakeHttpMessageHandler) { BaseAddress = new Uri("http://localhost") };
            var fakeScheduler = A.Fake<IUnleashScheduledTaskManager>();
            var fakeFileSystem = new MockFileSystem();

            A.CallTo(() => fakeHttpClientFactory.Create(A<Uri>._)).Returns(httpClient);
            A.CallTo(() => fakeScheduler.Configure(A<IEnumerable<IUnleashScheduledTask>>._, A<CancellationToken>._)).Invokes(action =>
            {
                var task = ((IEnumerable<IUnleashScheduledTask>)action.Arguments[0]).First();
                task.ExecuteAsync((CancellationToken)action.Arguments[1]).Wait();
            });

            fakeHttpMessageHandler.Response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(state, Encoding.UTF8, "application/json"),
                Headers =
                {
                    ETag = new EntityTagHeaderValue("\"123\"")
                }
            };

            var contextBuilder = new UnleashContext.Builder();
            contextBuilder.AddProperty("item-id", "1");

            var settings = new UnleashSettings
            {
                AppName = name,
                UnleashContextProvider = new DefaultUnleashContextProvider(contextBuilder.Build()),
                HttpClientFactory = fakeHttpClientFactory,
                ScheduledTaskManager = fakeScheduler,
                FileSystem = fakeFileSystem,
                DisableSingletonWarning = true
            };

            var unleash = new DefaultUnleash(settings);

            return unleash;
        }
    }
}
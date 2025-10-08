using NUnit.Framework;
using NUnit.Framework.Internal;
using Unleash.Communication;
using FakeItEasy;
using Unleash.ClientFactory;
using FluentAssertions;
using static Unleash.Tests.Specifications.TestFactory;
using System.Net.Http.Headers;
using System.Text;
using Unleash.Scheduling;

namespace Unleash.Tests.ClientFactory
{
    public class SyncStartupUnitTest
    {
        private IUnleashApiClient mockApiClient { get; set; }
        private UnleashSettings settings { get; set; }
        private IUnleashClientFactory unleashFactory { get; set; }

        [SetUp]
        public void Setup()
        {
            mockApiClient = A.Fake<IUnleashApiClient>();
            settings = new MockedUnleashSettings(instanceTag: "test instance SyncStartupUnitTest");
            unleashFactory = new UnleashClientFactory();
        }

        [Test(Description = "Immediate initialization: Should only fetch toggles once")]
        public async Task ImmediateInitializationFetchCount()
        {
            settings.UnleashApiClient = mockApiClient;

            var unleash = await unleashFactory.CreateClientAsync(settings, synchronousInitialization: true);

            A.CallTo(() => mockApiClient.FetchToggles(string.Empty, A<CancellationToken>.Ignored, true))
                .MustHaveHappenedOnceExactly();
        }

        [Test(Description = "Immediate initialization: Should be ready after creation")]
        public async Task ImmediateInitializationReadyAfterConstruction()
        {
            var unleash = await unleashFactory.CreateClientAsync(settings, synchronousInitialization: true);

            unleash.IsEnabled("one-enabled", false)
                .Should().BeTrue();
        }

        [Test(Description = "Immediate initialization: Should bubble up errors")]
        public void ImmediateInitializationBubbleErrors()
        {
            settings.UnleashApiClient = mockApiClient;
            A.CallTo(() => mockApiClient.FetchToggles(A<string>.Ignored, A<CancellationToken>.Ignored, true))
                .Throws<Exception>();

            Assert.ThrowsAsync<Exception>(async () => await unleashFactory.CreateClientAsync(settings, synchronousInitialization: true));
        }

        [Test(Description = "Immediate initialization: Should bubble up async fetch errors")]
        public void ImmediateInitializationBubbleAsyncErrors()
        {
            settings.UnleashApiClient = mockApiClient;
            A.CallTo(() => mockApiClient.FetchToggles(A<string>.Ignored, A<CancellationToken>.Ignored, true))
                .ThrowsAsync(new Exception());

            Assert.ThrowsAsync<Exception>(async () => await unleashFactory.CreateClientAsync(settings, synchronousInitialization: true));
        }

        [Test(Description = "Delayed initialization: Should be ready after creation")]
        public void DelayedInitializationNotReadyAfterConstruction()
        {
            var unleash = unleashFactory.CreateClientAsync(settings).Result;

            unleash.IsEnabled("one-enabled", false)
                .Should().BeFalse();
        }

        [Test]
        public void Synchronous_Initialization_400s_Throws()
        {
            // Act, Assert
            Assert.Throws<UnleashException>(() =>
            {
                var unleash = GetUnleash(new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.Forbidden,
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"123\"")
                    }
                });
            });
        }

        [Test]
        public void Synchronous_Initialization_429_Throws()
        {
            // Act, Assert
            Assert.Throws<UnleashException>(() =>
            {
                var unleash = GetUnleash(new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.Forbidden,
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"123\"")
                    }
                });
            });
        }

        [Test]
        public void Synchronous_Initialization_304_Does_Not_Throw()
        {
            // Act, Assert
            Assert.DoesNotThrow(() =>
            {
                var unleash = GetUnleash(new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.NotModified,
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"123\"")
                    }
                });
            });
        }


        [Test]
        public void Synchronous_Initialization_Ok_Does_Not_Throw()
        {
            // Act, Assert
            Assert.DoesNotThrow(() =>
            {
                var unleash = GetUnleash(new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(TestData, Encoding.UTF8, "application/json"),
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"123\"")
                    }
                });
            });
        }

        [Test]
        public void Synchronous_Initialization_302_Throws()
        {
            // Act, Assert
            Assert.Throws<UnleashException>(() =>
            {
                var unleash = GetUnleash(new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.Found,
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"123\"")
                    }
                });
            });
        }

        [Test]
        public void Synchronous_Initialization_500_Throws()
        {
            // Act, Assert
            Assert.Throws<UnleashException>(() =>
            {
                var unleash = GetUnleash(new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Headers =
                    {
                        ETag = new EntityTagHeaderValue("\"123\"")
                    }
                });
            });
        }

        [Test]
        public void Failing_Synchronous_Initialization_Can_Still_Create_New_Instance()
        {
            var settings = new UnleashSettings
            {
                AppName = "test-app-name",
                UnleashApi = new Uri("http://localhost:33/does/not/exist/api"),
                CustomHttpHeaders = new Dictionary<string, string>()
                {
                    { "Authorization", "Token" },
                }
            };

            var factory = new UnleashClientFactory();
            Assert.ThrowsAsync<UnleashException>(async () =>
            {
                var failingUnleashInit = await factory.CreateClientAsync(settings, synchronousInitialization: true);
            });

            var unleash = new DefaultUnleash(settings);
            Assert.IsNotNull(unleash);
            Assert.True(unleash.IsEnabled("some-toggle", true));
        }

        private IUnleash GetUnleash(HttpResponseMessage response)
        {
            var fakeHttpClientFactory = A.Fake<IHttpClientFactory>();
            var settings = new UnleashSettings()
            {
                AppName = "testapp",
                UnleashApi = new Uri("http://localhost:8080/"),
                ScheduledTaskManager = A.Fake<IUnleashScheduledTaskManager>(),
                HttpClientFactory = fakeHttpClientFactory,
                DisableSingletonWarning = true
            };
            var responseContent = TestData;
            var fakeHttpMessageHandler = new TestHttpMessageHandler();
            fakeHttpMessageHandler.Response = response;
            var client = new HttpClient(fakeHttpMessageHandler);
            client.BaseAddress = settings.UnleashApi;
            var factory = new UnleashClientFactory();
            A.CallTo(() => fakeHttpClientFactory.Create(A<Uri>._)).Returns(client);
            return factory.CreateClient(settings, synchronousInitialization: true);
        }

        public string TestData => @"{
            ""version"": 2,
            ""features"": [
                {
                    ""name"": ""enabled.with.variants"",
                    ""description"": ""Test"",
                    ""enabled"": true,
                    ""strategies"": [
                        {
                            ""name"": ""flexibleRollout"",
                            ""parameters"": {
                                ""rollout"": ""100"",
                                ""stickiness"": ""default"",
                                ""groupId"": ""default""
                            }
                        }
                    ],
                    ""variants"": [
                        {
                            ""name"": ""A"",
                            ""weight"": 50
                        },
                        {
                            ""name"": ""B"",
                            ""weight"": 50
                        }
                    ]
                },
                {
                    ""name"": ""enabled.no.variants"",
                    ""description"": ""Test"",
                    ""enabled"": true,
                    ""strategies"": [
                        {
                            ""name"": ""default""
                        }
                    ]
                }
            ]
        }";
    }
}

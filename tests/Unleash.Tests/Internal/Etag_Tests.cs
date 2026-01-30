using FakeItEasy;
using NUnit.Framework;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Scheduling;
using Yggdrasil;

namespace Unleash.Tests.Internal
{
    public class Etag_Tests
    {
        [Test]
        public void Etag_Gets_Used_For_FetchToggles()
        {
            var fetchState1 = @"
            {
              ""version"": 2,
              ""features"": [
                {
                  ""name"": ""toggle-1"",
                  ""type"": ""operational"",
                  ""enabled"": true,
                  ""impressionData"": false,
                  ""strategies"": []
                }
              ]
            }";
            var fetchState2 = @"
            {
              ""version"": 2,
              ""features"": [
                {
                  ""name"": ""toggle-1"",
                  ""type"": ""operational"",
                  ""enabled"": true,
                  ""impressionData"": true,
                  ""strategies"": []
                }
              ]
            }";
            // Arrange
            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(null, A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = true, State = fetchState1, Etag = "one" }));

            A.CallTo(() => fakeApiClient.FetchToggles("one", A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = true, State = fetchState2, Etag = "two" }));
            var tokenSource = new CancellationTokenSource();
            var config = new UnleashConfig
            {
                Engine = new YggdrasilEngine(),
                EventConfig = new EventCallbackConfig(),
                BackupManager = new NoOpBackupManager(),
                CancellationToken = tokenSource.Token,
                ApiClient = fakeApiClient,
            };
            var task = new FetchFeatureTogglesTask(config);
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Act
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Assert
            A.CallTo(() => fakeApiClient.FetchToggles("one", A<CancellationToken>._, false))
                .MustHaveHappenedOnceExactly();
        }
    }
}

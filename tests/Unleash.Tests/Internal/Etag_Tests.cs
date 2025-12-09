using FakeItEasy;
using NUnit.Framework;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Scheduling;
using Unleash.Tests.Mock;
using Yggdrasil;

namespace Unleash.Tests.Internal
{
    public class Etag_Tests
    {
        [Test]
        public void Etag_Gets_Used_For_FetchToggles()
        {
            // Arrange
            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(null, A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = true, State = "", Etag = "one" }));

            A.CallTo(() => fakeApiClient.FetchToggles("one", A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = true, State = "", Etag = "two" }));

            var engine = new YggdrasilEngine();

            var callbackConfig = new EventCallbackConfig();
            var filesystem = new MockFileSystem();
            var tokenSource = new CancellationTokenSource();
            var backupManager = new NoOpBackupManager();
            var task = new FetchFeatureTogglesTask(engine, fakeApiClient, filesystem, callbackConfig, backupManager, false);
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Act
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Assert
            A.CallTo(() => fakeApiClient.FetchToggles("one", A<CancellationToken>._, false))
                .MustHaveHappenedOnceExactly();
        }
    }
}

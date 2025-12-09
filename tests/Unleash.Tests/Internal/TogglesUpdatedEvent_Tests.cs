using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Scheduling;
using Unleash.Tests.Mock;
using Yggdrasil;

namespace Unleash.Tests.Internal
{
    public class TogglesUpdatedEvent_Tests
    {
        [Test]
        public void TogglesUpdated_Event_Gets_Called_For_HasChanged_True()
        {
            // Arrange
            TogglesUpdatedEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig
            {
                TogglesUpdatedEvent = evt => { callbackEvent = evt; }
            };

            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(A<string>._, A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = true, State = "", Etag = "one" }));

            var engine = new YggdrasilEngine();

            var filesystem = new MockFileSystem();
            var tokenSource = new CancellationTokenSource();
            var backupManager = new NoOpBackupManager();
            var task = new FetchFeatureTogglesTask(engine, fakeApiClient, filesystem, callbackConfig, backupManager, false);

            // Act
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Assert
            callbackEvent.Should().NotBeNull();
        }

        [Test]
        public void TogglesUpdated_Event_Does_Not_Get_Called_For_HasChanged_False()
        {
            // Arrange
            TogglesUpdatedEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig
            {
                TogglesUpdatedEvent = evt => { callbackEvent = evt; }
            };

            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(A<string>._, A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = false, State = "", Etag = "one" }));

            var engine = new YggdrasilEngine();

            var filesystem = new MockFileSystem();
            var tokenSource = new CancellationTokenSource();
            var backupManager = new NoOpBackupManager();
            var task = new FetchFeatureTogglesTask(engine, fakeApiClient, filesystem, callbackConfig, backupManager, false);

            // Act
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Assert
            callbackEvent.Should().BeNull();
        }

        [Test]
        public void TogglesUpdated_Event_Is_Raised_After_ToggleCollection_Is_Updated()
        {
            // Arrange
            var fetchState = @"
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
            var engine = new YggdrasilEngine();

            var toggleIsEnabledResultAfterEvent = false;
            var callbackConfig = new EventCallbackConfig
            {
                // when toggles updated event is raised (after the fetch), check toggle collection to see if toggle is enabled
                TogglesUpdatedEvent = evt => { toggleIsEnabledResultAfterEvent = engine.IsEnabled("toggle-1", new UnleashContext()).Enabled; }
            };

            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(A<string>._, A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult { HasChanged = true, State = fetchState, Etag = "one" }));

            var filesystem = new MockFileSystem();
            var tokenSource = new CancellationTokenSource();
            var backupManager = new NoOpBackupManager();
            var task = new FetchFeatureTogglesTask(engine, fakeApiClient, filesystem, callbackConfig, backupManager, false);

            // Act
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Assert
            engine.IsEnabled("toggle-1", new UnleashContext()).Enabled.Should().BeTrue(); // verify that toggle is enabled after fetch
            toggleIsEnabledResultAfterEvent.Should().BeTrue(); // verify that toggles updated event handler got the correct result for the updated toggle state (should now be enabled)
        }
    }
}

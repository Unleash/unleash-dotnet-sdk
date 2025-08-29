using FakeItEasy;
using NUnit.Framework;
using System.Net;
using System.Text;
using static Unleash.Tests.Specifications.TestFactory;
using Unleash.Tests.Mock;
using Unleash.Scheduling;
using Unleash.Internal;
using Unleash.Events;
using Unleash.Communication;
using FluentAssertions;
using Unleash.Metrics;
using Yggdrasil;

namespace Unleash.Tests.Internal
{
    public class ErrorEvents_Tests
    {
        [Test]
        public void Fetch_Toggles_Unauthorized_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var fakeHttpMessageHandler = new TestHttpMessageHandler()
            {
                Response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Unauthorized", Encoding.UTF8) },
            };
            var httpClient = new HttpClient(fakeHttpMessageHandler) { BaseAddress = new Uri("http://localhost") };
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };
            var unleashClient = new UnleashApiClient(httpClient, new UnleashApiClientRequestHeaders(), eventConfig: callbackConfig);
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var result = unleashClient.FetchToggles("123", cancellationTokenSource.Token).Result;

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().BeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.Client);
        }

        [Test]
        public void RegisterClient_Unauthorized_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var fakeHttpMessageHandler = new TestHttpMessageHandler()
            {
                Response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Unauthorized", Encoding.UTF8) },
            };
            var httpClient = new HttpClient(fakeHttpMessageHandler) { BaseAddress = new Uri("http://localhost") };
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var unleashClient = new UnleashApiClient(httpClient, new UnleashApiClientRequestHeaders(), eventConfig: callbackConfig);
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var result = unleashClient.RegisterClient(new ClientRegistration(), cancellationTokenSource.Token).Result;

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().BeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.Client);
        }

        [Test]
        public void FetchFeatureToggleTask_HttpRequestException_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            Exception thrownException = null;
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(A<string>._, A<CancellationToken>._, false))
                .ThrowsAsync(() => new HttpRequestException("The remote server refused the connection"));

            var engine = A.Fake<YggdrasilEngine>();
            var filesystem = new MockFileSystem();
            var tokenSource = new CancellationTokenSource();
            var backupManager = new NoOpBackupManager();
            var task = new FetchFeatureTogglesTask(engine, fakeApiClient, filesystem, callbackConfig, backupManager, false);

            // Act
            try
            {
                Task.WaitAll(task.ExecuteAsync(tokenSource.Token));
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.Client);
            thrownException.Should().NotBeNull();
        }

        [Test]
        public void FetchFeatureToggleTask_Etag_Writing_Throws_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var exceptionMessage = "Writing failed";
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var fakeApiClient = A.Fake<IUnleashApiClient>();
            A.CallTo(() => fakeApiClient.FetchToggles(A<string>._, A<CancellationToken>._, false))
                .Returns(Task.FromResult(new FetchTogglesResult() { HasChanged = true, State = "", Etag = "one" }));

            var engine = A.Fake<YggdrasilEngine>();

            var filesystem = A.Fake<IFileSystem>();
            A.CallTo(() => filesystem.WriteAllText(A<string>._, A<string>._))
                .Throws(() => new IOException(exceptionMessage));

            var tokenSource = new CancellationTokenSource();
            var backupManager = new NoOpBackupManager();
            var task = new FetchFeatureTogglesTask(engine, fakeApiClient, filesystem, callbackConfig, backupManager, false);

            // Act
            Task.WaitAll(task.ExecuteAsync(tokenSource.Token));

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.Error.Message.Should().Be(exceptionMessage);
            callbackEvent.ErrorType.Should().Be(ErrorType.TogglesBackup);
        }

        [Test]
        public void CachedFilesLoader_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var exceptionMessage = "Writing failed";
            var filesystem = A.Fake<IFileSystem>();
            A.CallTo(() => filesystem.WriteAllText(A<string>._, A<string>._))
                .Throws(() => new IOException(exceptionMessage));

            var toggleBootstrapProvider = A.Fake<IToggleBootstrapProvider>();
            var settings = new UnleashSettings
            {
                FileSystem = filesystem,
                ToggleBootstrapProvider = toggleBootstrapProvider
            };

            var filecache = new CachedFilesLoader(settings, callbackConfig);

            // Act
            filecache.Load();

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.FileCache);
        }

        [Test]
        public void Engine_TakeState_InvalidJson_Throws_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var bootstrapProviderFake = A.Fake<IToggleBootstrapProvider>();
            A.CallTo(() => bootstrapProviderFake.Read())
                .Returns("Something that is definitely not valid JSON");

            // Act
            new UnleashServices(new UnleashSettings()
            {
                ToggleBootstrapProvider = bootstrapProviderFake,
                BootstrapOverride = true
            }, callbackConfig);

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.FileCache);
        }
    }
}

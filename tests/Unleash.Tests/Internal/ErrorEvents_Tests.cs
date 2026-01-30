using FakeItEasy;
using NUnit.Framework;
using System.Net;
using System.Text;
using static Unleash.Tests.Specifications.TestFactory;
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

            var tokenSource = new CancellationTokenSource();
            var config = new UnleashConfig
            {
                Engine = new YggdrasilEngine(),
                EventConfig = callbackConfig,
                BackupManager = new NoOpBackupManager(),
                CancellationToken = tokenSource.Token,
                ApiClient = fakeApiClient,
            };
            var task = new FetchFeatureTogglesTask(config);

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
        public void CachedFileLoader_Saving_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var exceptionMessage = "Writing failed";
            var filesystem = A.Fake<IFileSystem>();
            A.CallTo(() => filesystem.FileOpenCreate(A<string>._))
                .Throws(() => new IOException(exceptionMessage));

            var settings = new UnleashSettings
            {
                FileSystem = filesystem
            };

            var filecache = new CachedFilesLoader(settings, callbackConfig, filesystem);

            // Act
            filecache.Save(new Backup("{}", "etag"));

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.Error.Message.Should().Be(exceptionMessage);
            callbackEvent.ErrorType.Should().Be(ErrorType.TogglesBackup);
        }

        [Test]
        public void CachedFilesLoader_Loading_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var exceptionMessage = "Reading failed";
            var filesystem = A.Fake<IFileSystem>();
            A.CallTo(() => filesystem.ReadAllText(A<string>._))
                .Throws(() => new IOException(exceptionMessage));

            var settings = new UnleashSettings
            {
                FileSystem = filesystem,
            };

            var filecache = new CachedFilesLoader(settings, callbackConfig, filesystem);

            // Act
            filecache.Load();

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.FileCache);
        }

        [Test]
        public void CachedFilesLoader_Bootstrapping_Raises_ErrorEvent()
        {
            // Arrange
            ErrorEvent callbackEvent = null;
            var callbackConfig = new EventCallbackConfig()
            {
                ErrorEvent = evt => { callbackEvent = evt; }
            };

            var exceptionMessage = "Bootstrapping failed";
            var filesystem = A.Fake<IFileSystem>();

            var toggleBootstrapProvider = A.Fake<IToggleBootstrapProvider>();
            A.CallTo(() => toggleBootstrapProvider.Read())
                .Throws(() => new IOException(exceptionMessage));
            var settings = new UnleashSettings
            {
                FileSystem = filesystem,
                ToggleBootstrapProvider = toggleBootstrapProvider
            };

            var filecache = new CachedFilesLoader(settings, callbackConfig, filesystem);

            // Act
            filecache.Load();

            // Assert
            callbackEvent.Should().NotBeNull();
            callbackEvent.Error.Should().NotBeNull();
            callbackEvent.ErrorType.Should().Be(ErrorType.FileCache);
        }
    }
}

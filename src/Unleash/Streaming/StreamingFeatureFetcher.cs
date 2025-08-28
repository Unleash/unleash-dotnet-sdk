using Unleash.Communication;
using Yggdrasil;
using LaunchDarkly.EventSource;
using System;
using System.Threading.Tasks;
using Unleash.Internal;
using Unleash.Events;
using Unleash.Logging;
using System.IO;

namespace Unleash.Streaming
{
    /// <summary>
    /// </summary>
    internal class StreamingFeatureFetcher
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(StreamingFeatureFetcher));

        public StreamingFeatureFetcher(UnleashSettings settings, IUnleashApiClient apiClient, YggdrasilEngine engine, EventCallbackConfig eventConfig, string toggleFile, IFileSystem fileSystem)
        {
            Settings = settings;
            ApiClient = apiClient;
            Engine = engine;
            EventConfig = eventConfig;
            ToggleFile = toggleFile;
            FileSystem = fileSystem;
        }

        private YggdrasilEngine Engine { get; set; }
        private EventCallbackConfig EventConfig { get; set; }
        private string ToggleFile { get; }
        private IFileSystem FileSystem { get; }
        private UnleashSettings Settings { get; set; }
        private IUnleashApiClient ApiClient { get; set; }

        public async Task StartAsync()
        {
            try
            {
                var uri = Settings.UnleashApi;
                if (!uri.AbsolutePath.EndsWith("/"))
                {
                    uri = new Uri($"{uri.AbsoluteUri}/");
                }
                await ApiClient.StartStreamingAsync(uri, this).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = ex });
                throw new UnleashException("Exception while starting streaming", ex);
            }
        }

        public void HandleMessage(object target, MessageReceivedEventArgs data)
        {
            switch (data.EventName)
            {
                case "unleash-connected":
                case "unleash-updated":
                    Logger.Debug(() => $"UNLEASH: Handling event '{data.EventName}'");
                    HandleStreamingUpdate(data.Message.Data);
                    break;
                default:
                    Logger.Debug(() => $"UNLEASH: Ignoring unknown event type: {data.EventName}");
                    break;
            }
        }

        public void HandleStreamingUpdate(string data)
        {
            try
            {
                Engine.TakeState(data);

                // now that the toggle collection has been updated, raise the toggles updated event if configured
                EventConfig?.RaiseTogglesUpdated(new TogglesUpdatedEvent { UpdatedOn = DateTime.UtcNow });

                try
                {
                    var state = Engine.GetState();
                    FileSystem.WriteAllText(ToggleFile, state);
                }
                catch (IOException ex)
                {
                    Logger.Warn(() => $"UNLEASH: Exception when writing to toggle file '{ToggleFile}'.", ex);
                    EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.TogglesBackup, Error = ex });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(() => $"UNLEASH: Error processing streaming event, feature flags will likely not evaluate correctly until application restart or stream re-connect", ex);
                EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = ex });
            }
        }

        public void HandleError(object target, ExceptionEventArgs data)
        {
            // Handle any errors that occur during streaming
            EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = data.Exception });
        }

        public void Dispose()
        {
            try
            {
                ApiClient.StopStreaming();
            }
            catch (Exception ex)
            {
                EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = ex });
                throw new UnleashException("Exception while stopping streaming", ex);
            }
        }
    }
}
using Unleash.Communication;
using Yggdrasil;
using LaunchDarkly.EventSource;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unleash.Internal;
using Unleash.Events;
using Unleash.Logging;
using System.Threading;

namespace Unleash.Streaming
{
    /// <summary>
    /// Connects to and consumes messages from streaming endpoint
    /// </summary>
    internal class StreamingFeatureFetcher
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(StreamingFeatureFetcher));
        private int ready = 0;

        internal event EventHandler OnReady;

        public StreamingFeatureFetcher(UnleashConfig config, Action<string> modeChange)
        {
            this.UnleashApi = config.UnleashApi;
            this.Engine = config.Engine;
            this.EventConfig = config.EventConfig;
            this.BackupManager = config.BackupManager;
            this.ApiClient = config.ApiClient;
            ModeChange = modeChange;
        }

        private Uri UnleashApi { get; set; }
        private YggdrasilEngine Engine { get; set; }
        private EventCallbackConfig EventConfig { get; set; }
        private IBackupManager BackupManager { get; set; }
        public Action<string> ModeChange { get; }
        private IUnleashApiClient ApiClient { get; set; }

        private async Task Reconnect()
        {
            ApiClient.StopStreaming();
            await StartAsync();
        }

        public async Task StartAsync()
        {
            try
            {
                var uri = UnleashApi;
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

        public async Task StopAsync()
        {
            ApiClient.StopStreaming();
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

                var raiseReady = Interlocked.Exchange(ref ready, 1) == 0;
                if (raiseReady)
                {
                    OnReady?.Invoke(this, new EventArgs());
                }

                BackupManager.Save(new Backup(Engine.GetState(), null));

                // now that the toggle collection has been updated, raise the toggles updated event if configured
                EventConfig?.RaiseTogglesUpdated(new TogglesUpdatedEvent { UpdatedOn = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                Logger.Warn(() => $"UNLEASH: Error processing streaming event, re-connecting", ex);
                EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = ex });
                Task.Run(() => this.Reconnect().ConfigureAwait(false));
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
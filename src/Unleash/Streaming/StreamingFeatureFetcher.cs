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
        private TaskFactory TaskFactory;

        internal event EventHandler OnReady;

        public StreamingFeatureFetcher(UnleashConfig config, Action<string> modeChange)
        {
            this.UnleashApi = config.UnleashApi;
            this.Engine = config.Engine;
            this.EventConfig = config.EventConfig;
            this.BackupManager = config.BackupManager;
            this.ApiClient = config.ApiClient;
            this.TaskFactory = config.TaskFactory;
            ModeChange = modeChange;
            failoverStrategy = new StreamingFailoverStrategy(config.MaxFailuresUntilFailover, config.FailureWindowMs);
        }

        private Uri UnleashApi { get; set; }
        private YggdrasilEngine Engine { get; set; }
        private EventCallbackConfig EventConfig { get; set; }
        private IBackupManager BackupManager { get; set; }
        public Action<string> ModeChange { get; }
        private IUnleashApiClient ApiClient { get; set; }
        private StreamingFailoverStrategy failoverStrategy { get; }

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
            catch (YggdrasilEngineException ex)
            {
                Logger.Warn(() => $"UNLEASH: Yggdrasil engine exception while processing streaming event, re-connecting", ex);
                Task.Run(() => this.Reconnect().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Logger.Warn(() => $"UNLEASH: Error processing streaming event", ex);
            }
        }

        public void HandleError(object target, ExceptionEventArgs data)
        {
            FailEventArgs failEvent = null;

            if (data.Exception is EventSourceServiceUnsuccessfulResponseException connectionException)
            {
                if (connectionException.Headers.Any(h => h.Key == "fetch-mode" && h.Value.Contains("polling")))
                {
                    failEvent = new ServerHintFailEventArgs { Hint = "polling" };
                }
                else
                {
                    failEvent = new HttpStatusFailEventArgs { StatusCode = connectionException.StatusCode };
                }
            }
            else
            {
                failEvent = new NetworkEventErrorArgs();
            }

            // Handle any errors that occur during streaming
            EventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = data.Exception });
            HandleFailoverDecision(failEvent);
        }

        public void HandleClosed(object target, StateChangedEventArgs data)
        {
            Logger.Debug(() => "Connection closed");
        }

        private void HandleFailoverDecision(FailEventArgs failEvent)
        {
            if (failoverStrategy.ShouldFailOver(failEvent, DateTimeOffset.UtcNow))
            {
                ModeChange("polling");
            }
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
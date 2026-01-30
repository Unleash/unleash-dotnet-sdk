using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Logging;
using Unleash.Events;
using System.Net.Http;
using Yggdrasil;

namespace Unleash.Scheduling
{
    internal class FetchFeatureTogglesTask : IUnleashScheduledTask
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(FetchFeatureTogglesTask));
        private readonly IBackupManager backupManager;
        private readonly EventCallbackConfig eventConfig;
        private readonly IUnleashApiClient apiClient;
        private readonly YggdrasilEngine engine;
        private readonly bool throwOnInitialLoadFail;
        private bool ready = false;

        // In-memory reference of toggles/etags
        internal string Etag { get; set; }

        internal event EventHandler OnReady;

        public FetchFeatureTogglesTask(
            UnleashConfig config)
        {
            this.engine = config.Engine;
            this.apiClient = config.ApiClient;
            this.eventConfig = config.EventConfig;
            this.backupManager = config.BackupManager;
            this.throwOnInitialLoadFail = config.ThrowOnInitialFetchFail;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var raiseReady = !ready;
            FetchTogglesResult result;
            try
            {
                result = await apiClient.FetchToggles(Etag, cancellationToken, !ready && this.throwOnInitialLoadFail).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                Logger.Warn(() => $"UNLEASH: Unhandled exception when fetching toggles.", ex);
                eventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.Client, Error = ex });
                throw new UnleashException("Exception while fetching from API", ex);
            }
            ready = true;
            var updated = TryApplyFetchedState(result);

            if (raiseReady)
            {
                OnReady?.Invoke(this, new EventArgs());
            }

            if (updated)
            {
                // now that the toggle collection has been updated, raise the toggles updated event if configured
                eventConfig?.RaiseTogglesUpdated(new TogglesUpdatedEvent { UpdatedOn = DateTime.UtcNow });
            }
        }

        private bool TryApplyFetchedState(FetchTogglesResult result)
        {
            if (!result.HasChanged)
            {
                return false;
            }

            if (string.IsNullOrEmpty(result.Etag))
                return false;

            if (result.Etag == Etag)
                return false;

            if (!string.IsNullOrEmpty(result.State))
            {
                try
                {
                    engine.TakeState(result.State);
                    backupManager.Save(new Backup(result.State, result.Etag));
                    Etag = result.Etag;
                }
                catch (Exception ex)
                {
                    Logger.Warn(() => $"UNLEASH: Exception when updating toggle collection.", ex);
                    eventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.TogglesUpdate, Error = ex });
                    throw new UnleashException("Exception while updating toggle collection", ex);
                }
                return true;
            }

            return false;
        }

        public string Name => "fetch-feature-toggles-task";
        public TimeSpan Interval { get; set; }
        public bool ExecuteDuringStartup { get; set; }
    }
}
using System;
using System.IO;
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
        private readonly IFileSystem fileSystem;
        private readonly EventCallbackConfig eventConfig;
        private readonly IUnleashApiClient apiClient;
        private readonly YggdrasilEngine engine;
        private readonly bool throwOnInitialLoadFail;
        private bool ready = false;

        // In-memory reference of toggles/etags
        internal string Etag { get; set; }

        public FetchFeatureTogglesTask(
            YggdrasilEngine engine,
            IUnleashApiClient apiClient,
            IFileSystem fileSystem,
            EventCallbackConfig eventConfig,
            IBackupManager backupManager,
            bool throwOnInitialLoadFail)
        {
            this.engine = engine;
            this.apiClient = apiClient;
            this.fileSystem = fileSystem;
            this.eventConfig = eventConfig;
            this.backupManager = backupManager;
            this.throwOnInitialLoadFail = throwOnInitialLoadFail;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
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

            if (!result.HasChanged)
            {
                return;
            }

            if (string.IsNullOrEmpty(result.Etag))
                return;

            if (result.Etag == Etag)
                return;

            if (!string.IsNullOrEmpty(result.State))
            {
                try
                {
                    engine.TakeState(result.State);
                }
                catch (Exception ex)
                {
                    Logger.Warn(() => $"UNLEASH: Exception when updating toggle collection.", ex);
                    eventConfig?.RaiseError(new ErrorEvent() { ErrorType = ErrorType.TogglesUpdate, Error = ex });
                    throw new UnleashException("Exception while updating toggle collection", ex);
                }
            }

            // now that the toggle collection has been updated, raise the toggles updated event if configured
            eventConfig?.RaiseTogglesUpdated(new TogglesUpdatedEvent { UpdatedOn = DateTime.UtcNow });

            backupManager.Save(new Backup(result.State, result.Etag));
            Etag = result.Etag;
        }

        public string Name => "fetch-feature-toggles-task";
        public TimeSpan Interval { get; set; }
        public bool ExecuteDuringStartup { get; set; }
    }
}
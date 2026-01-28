using System;
using System.Threading;
using System.Threading.Tasks;
using Unleash.Communication;
using Unleash.Scheduling;
using Yggdrasil;

namespace Unleash.Internal
{
    internal class UnleashConfig
    {
        internal string AppName { get; set; }
        internal string InstanceTag { get; set; }
        internal string SdkVersion { get; set; }
        internal Uri UnleashApi { get; set; }
        internal IUnleashScheduledTaskManager ScheduledTaskManager { get; set; }
        internal IBackupManager BackupManager { get; set; }
        internal IUnleashContextProvider ContextProvider { get; set; }
        internal YggdrasilEngine Engine { get; set; }
        internal IFileSystem FileSystem { get; set; }
        internal IUnleashApiClient ApiClient { get; set; }
        internal TaskFactory TaskFactory { get; set; }
        internal EventCallbackConfig EventConfig { get; set; }
        internal bool SynchronousInitialization { get; set; }
        internal TimeSpan? SendMetricsInterval { get; set; }
        internal TimeSpan FetchTogglesInterval { get; set; }
        internal bool ExperimentalUseStreaming { get; set; }
        internal bool ScheduleFeatureToggleFetchImmediatly { get; set; }
        internal bool ThrowOnInitialFetchFail { get; set; }
        internal CancellationToken CancellationToken { get; set; }
        internal int MaxFailuresUntilFailover { get; set; } = 5;
        internal int FailureWindowMs { get; set; } = 60_000;
    }
}
using System;
using System.IO;
using System.Linq;
using Unleash.Logging;

namespace Unleash.Internal
{
    public interface IBackupManager
    {
        void Save(Backup backup);
        Backup Load();

    }

    public class Backup
    {
        public string InitialETag { get; }
        public string InitialState { get; }

        public Backup(string featureState, string eTag)
        {
            InitialState = featureState;
            InitialETag = eTag;
        }

        internal static readonly Backup Empty = new Backup(string.Empty, string.Empty);
    }

    internal class CachedFilesLoader : IBackupManager
    {
        static internal readonly string FeatureToggleFilename = "unleash.toggles.json";
        static internal readonly string EtagFilename = "unleash.etag.txt";

        private static readonly ILog Logger = LogProvider.GetLogger(typeof(CachedFilesLoader));
        private readonly UnleashSettings settings;
        private readonly EventCallbackConfig eventCallbackConfig;

        internal CachedFilesLoader(UnleashSettings settings, EventCallbackConfig eventCallbackConfig)
        {
            this.settings = settings;
            this.eventCallbackConfig = eventCallbackConfig;
        }

        public Backup Load()
        {
            try
            {
                var backup = LoadMainBackup() ?? LoadLegacyBackup();

                if ((backup == null || settings.BootstrapOverride) && settings.ToggleBootstrapProvider != null)
                {
                    string bootstrapState = settings.ToggleBootstrapProvider.Read();

                    return new Backup(bootstrapState ?? backup.InitialState ?? string.Empty, backup?.InitialETag ?? string.Empty);
                }
                return backup ?? Backup.Empty;
            }
            catch (Exception ex)
            {
                Logger.Warn(() => $"UNLEASH: Unexpected exception when loading backup files.", ex);
                eventCallbackConfig?.RaiseError(new Events.ErrorEvent() { Error = ex, ErrorType = Events.ErrorType.FileCache });
                return Backup.Empty;
            }
        }

        public void Save(Backup backup)
        {
            try
            {
                // very intentionally write the feature file first. If we fail to write the feature file
                // then then having a more up to date ETag is dangerous since when the SDK boots next time
                // it won't correctly pull the new feature state unless it's been updated while the SDK was down
                WriteBackup(GetFeatureToggleFilePath(), backup.InitialState);
                WriteBackup(GetFeatureToggleETagFilePath(), backup.InitialETag);
            }
            catch (Exception ex)
            {
                Logger.Warn(() => $"UNLEASH: Unexpected exception when writing backup files.", ex);
                eventCallbackConfig?.RaiseError(new Events.ErrorEvent() { Error = ex, ErrorType = Events.ErrorType.TogglesBackup });
            }
        }

        private Backup LoadMainBackup()
        {
            try
            {
                string toggleFileContent = settings.FileSystem.ReadAllText(GetFeatureToggleFilePath());
                string etagFileContent = settings.FileSystem.ReadAllText(GetFeatureToggleETagFilePath());

                return new Backup(toggleFileContent, etagFileContent);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is UnauthorizedAccessException)
            {
                Logger.Info(() => $"UNLEASH: Failed to load main backup: {ex.Message}, this is expected if the SDK has been recently upgraded");
                return null;
            }
        }

        private Backup LoadLegacyBackup()
        {
            try
            {
                string toggleFileContent = settings.FileSystem.ReadAllText(GetLegacyFeatureToggleFilePath());
                string etagFileContent = settings.FileSystem.ReadAllText(GetLegacyFeatureToggleETagFilePath());

                return new Backup(toggleFileContent, etagFileContent);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is UnauthorizedAccessException)
            {
                Logger.Info(() => $"UNLEASH: Failed to load legacy backup: {ex.Message}");
                return null;
            }
        }

        private void WriteBackup(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            var tempPath = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var stream = settings.FileSystem.FileOpenCreate(tempPath))
                using (var writer = new StreamWriter(stream, settings.FileSystem.Encoding))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush();
                }

                if (settings.FileSystem.FileExists(path))
                {
                    settings.FileSystem.Replace(tempPath, path, null);
                }
                else
                {
                    settings.FileSystem.Move(tempPath, path);
                }
            }
            catch (Exception)
            {
                try { if (settings.FileSystem.FileExists(path)) settings.FileSystem.Delete(path); } catch { /* swallow */ }
                throw;
            }
        }

        private string GetFeatureToggleFilePath()
        {
            return GetFeatureToggleFilePath(settings);
        }

        private string GetFeatureToggleETagFilePath()
        {
            return GetFeatureToggleETagFilePath(settings);
        }

        private string GetLegacyFeatureToggleFilePath()
        {
            return GetLegacyFeatureToggleFilePath(settings);
        }

        private string GetLegacyFeatureToggleETagFilePath()
        {
            return GetLegacyFeatureToggleETagFilePath(settings);
        }

        private static string LegacyPrependFileName(string filename, UnleashSettings settings)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();

            var extension = Path.GetExtension(filename);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

            return new string($"{fileNameWithoutExtension}-{settings.AppName}-{settings.InstanceTag}-{settings.SdkVersion}{extension}"
                .Where(c => !invalidFileNameChars.Contains(c))
                .ToArray());
        }

        private static string PrependFileName(string filename, UnleashSettings settings)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();

            var extension = Path.GetExtension(filename);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

            return new string($"{fileNameWithoutExtension}-{settings.AppName}-{settings.SdkVersion}{extension}"
                .Where(c => !invalidFileNameChars.Contains(c))
                .ToArray());
        }

        internal static string GetFeatureToggleFilePath(UnleashSettings settings)
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, PrependFileName(FeatureToggleFilename, settings));
        }

        internal static string GetFeatureToggleETagFilePath(UnleashSettings settings)
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, PrependFileName(EtagFilename, settings));
        }

        internal static string GetLegacyFeatureToggleFilePath(UnleashSettings settings)
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, settings.AppName, LegacyPrependFileName(FeatureToggleFilename, settings));
        }

        internal static string GetLegacyFeatureToggleETagFilePath(UnleashSettings settings)
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, settings.AppName, LegacyPrependFileName(EtagFilename, settings));
        }
    }

    public class NoOpBackupManager : IBackupManager
    {
        public Backup Load() => Backup.Empty;
        public void Save(Backup backup) { }
    }
}

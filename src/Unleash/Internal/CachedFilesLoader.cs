using System;
using System.IO;
using System.Linq;
using Unleash.Logging;

namespace Unleash.Internal
{
    public interface BackupManager
    {
        void Save(Backup backup);
        Backup Load();

    }

    public class Backup
    {
        public string ETag { get; }
        public string FeatureState { get; }

        public Backup(string eTag, string featureState)
        {
            ETag = eTag;
            FeatureState = featureState;
        }

        internal static readonly Backup Empty = new Backup(string.Empty, string.Empty);
    }

    internal class CachedFilesLoader : BackupManager
    {
        static internal readonly string FeatureToggleFilename = "unleash.toggles.json";
        static internal readonly string EtagFilename = "unleash.etag.txt";

        private static readonly ILog Logger = LogProvider.GetLogger(typeof(BackupManager));
        private readonly UnleashSettings settings;
        private readonly EventCallbackConfig eventCallbackConfig;

        internal CachedFilesLoader(UnleashSettings settings, EventCallbackConfig eventCallbackConfig)
        {
            this.settings = settings;
            this.eventCallbackConfig = eventCallbackConfig;
        }

        public Backup Load()
        {
            var backup = LoadBackups();

            if ((backup == null || settings.BootstrapOverride) && settings.ToggleBootstrapProvider != null)
            {
                string bootstrapState = settings.ToggleBootstrapProvider.Read();
                return new Backup(backup?.ETag ?? string.Empty, bootstrapState);
            }
            return backup ?? Backup.Empty;
        }

        public void Save(Backup backup)
        {
            try
            {
                // very intentionally write the feature file first. If we fail to write the feature file
                // then then having a more up to date ETag is dangerous since when the SDK boots next time
                // it won't correctly pull the new feature state unless it's been updated while the SDK was down
                WriteBackup(GetFeatureToggleFilePath(), backup.FeatureState);
                WriteBackup(GetFeatureToggleETagFilePath(), backup.ETag);
            }
            catch (Exception ex)
            {
                Logger.Warn(() => $"UNLEASH: Unexpected exception when writing backup files.", ex);
                eventCallbackConfig?.RaiseError(new Events.ErrorEvent() { Error = ex, ErrorType = Events.ErrorType.FileCache });
            }
        }

        private Backup LoadBackups()
        {
            try
            {
                return LoadMainBackup() ?? LoadLegacyBackup();
            }
            catch (IOException ex)
            {
                Logger.Warn(() => $"UNLEASH: Unexpected exception when loading backup files.", ex);
                eventCallbackConfig?.RaiseError(new Events.ErrorEvent() { Error = ex, ErrorType = Events.ErrorType.FileCache });
                return null;
            }
        }

        private Backup LoadMainBackup()
        {
            try
            {
                string toggleFileContent = settings.FileSystem.ReadAllText(GetFeatureToggleFilePath());
                string etagFileContent = settings.FileSystem.ReadAllText(GetFeatureToggleETagFilePath());

                return new Backup(etagFileContent, toggleFileContent);
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

                return new Backup(etagFileContent, toggleFileContent);
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
            catch (Exception ex)
            {
                Logger.Error(() => $"UNLEASH: Failed to write backup file {path}", ex);
                try { if (settings.FileSystem.FileExists(path)) settings.FileSystem.Delete(path); } catch { /* swallow */ }
                throw;
            }
        }

        private string GetFeatureToggleFilePath()
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, PrependFileName(FeatureToggleFilename));
        }

        private string GetFeatureToggleETagFilePath()
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, PrependFileName(EtagFilename));
        }

        private string GetLegacyFeatureToggleFilePath()
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, LegacyPrependFileName(FeatureToggleFilename));
        }

        private string GetLegacyFeatureToggleETagFilePath()
        {
            var tempFolder = settings.LocalStorageFolder();
            return Path.Combine(tempFolder, LegacyPrependFileName(EtagFilename));
        }

        private string LegacyPrependFileName(string filename)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();

            var extension = Path.GetExtension(filename);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

            return new string($"{fileNameWithoutExtension}-{settings.AppName}-{settings.InstanceTag}-{settings.SdkVersion}{extension}"
                .Where(c => !invalidFileNameChars.Contains(c))
                .ToArray());
        }

        private string PrependFileName(string filename)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();

            var extension = Path.GetExtension(filename);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

            return new string($"{fileNameWithoutExtension}-{settings.AppName}-{settings.SdkVersion}{extension}"
                .Where(c => !invalidFileNameChars.Contains(c))
                .ToArray());
        }

    }

    public class NoOpBackupManager : BackupManager
    {
        public Backup Load() => Backup.Empty;
        public void Save(Backup backup) { }
    }
}

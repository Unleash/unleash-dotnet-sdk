using FluentAssertions;
using NUnit.Framework;
using System.Text;
using Unleash.Internal;

namespace Unleash.Tests.Internal
{
    public class CachedFilesLoader_Backup_And_Etag_Tests : CachedFilesLoaderTestBase
    {
        [Test]
        public void Sets_Etag_From_Etag_File_And_Toggles_From_Backup_When_Backup_Is_Not_Empty()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-v1.json");
            string etagFileName = AppDataFile("etag-12345.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.ETag.Should().Be("12345");
            ensureResult.FeatureState.Should().Be(fileSystem.ReadAllText(toggleFileName));
        }

        [Test]
        public void Writes_Empty_Etag_File_And_Sets_Etag_To_Empty_String_And_Loads_Toggles_When_Etag_File_Is_Missing_But_Backup_Exists()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-v1.json");
            string etagFileName = AppDataFile("etag-missing.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.ETag.Should().Be(string.Empty);
            fileSystem.FileExists(etagFileName).Should().BeTrue();
            fileSystem.ReadAllText(etagFileName).Should().Be(string.Empty);
            ensureResult.FeatureState.Should().Be(fileSystem.ReadAllText(toggleFileName));
        }

        [Test]
        public void Sets_Etag_To_Empty_String_And_Toggles_To_Null_When_Etag_File_Exists_But_Backup_File_Does_Not()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-missing.json");
            string etagFileName = AppDataFile("etag-12345.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };

            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.ETag.Should().Be(string.Empty);
            ensureResult.FeatureState.Should().BeEmpty();
            fileSystem.FileExists(toggleFileName).Should().BeTrue();
            fileSystem.ReadAllText(toggleFileName).Should().Be(string.Empty);
        }

        [Test]
        public void Sets_Etag_To_Empty_String_And_Toggles_To_Null_When_Neither_File_Exists()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-missing.json");
            string etagFileName = AppDataFile("etag-missing.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.ETag.Should().Be(string.Empty);
            fileSystem.FileExists(etagFileName).Should().BeTrue();
            fileSystem.ReadAllText(etagFileName).Should().Be(string.Empty);

            ensureResult.FeatureState.Should().BeEmpty();
            fileSystem.FileExists(toggleFileName).Should().BeTrue();
            fileSystem.ReadAllText(toggleFileName).Should().Be(string.Empty);
        }
    }
}

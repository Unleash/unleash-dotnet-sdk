using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using System.Text;
using Unleash.Internal;
using Unleash.Tests.Mock;

namespace Unleash.Tests.Internal
{
    public class CachedFilesLoader_Backup_And_Etag_Tests : CachedFilesLoaderTestBase
    {
        [Test]
        public void Sets_Etag_From_Etag_File_And_Toggles_From_Backup_When_Backup_Is_Not_Empty()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,

            };
            var fileLoader = new CachedFilesLoader(settings, null);
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleETagFilePath(settings), "12345");
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleFilePath(settings), "features");

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.InitialETag.Should().Be("12345");
            ensureResult.InitialState.Should().Be("features");
        }

        [Test]
        public void Sets_Etag_To_Empty_String_And_Toggles_To_Null_When_Neither_File_Exists()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.InitialETag.Should().Be(string.Empty);
            ensureResult.InitialState.Should().BeEmpty();
        }

        [Test]
        public void Falls_Back_To_Legacy_Backup_File_If_No_File_Exists()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);
            fileSystem.WriteAllText(CachedFilesLoader.GetLegacyFeatureToggleETagFilePath(settings), "12345");
            fileSystem.WriteAllText(CachedFilesLoader.GetLegacyFeatureToggleFilePath(settings), "features");

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.InitialETag.Should().Be("12345");
            ensureResult.InitialState.Should().Be("features");
        }

        [Test]
        public void Saving_Features_Fails_Without_Saving_Etag_If_Main_Feature_Write_Fails()
        {
            var fileSystem = A.Fake<IFileSystem>();

            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            A.CallTo(() => fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleFilePath(settings), A<string>._))
                .Throws<IOException>();
            A.CallTo(() => fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleETagFilePath(settings), A<string>._))
                .MustNotHaveHappened();

            // Act
            fileLoader.Save(new Backup("features", string.Empty));
        }

        [Test]
        public void Saving_Features_And_Flags_Writes_To_Underlying_FileSystem()
        {
            var fileSystem = new MockFileSystem();
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            fileLoader.Save(new Backup("features", "12345"));

            // Assert
            fileSystem.ReadAllText(CachedFilesLoader.GetFeatureToggleETagFilePath(settings)).Should().Be("12345");
            fileSystem.ReadAllText(CachedFilesLoader.GetFeatureToggleFilePath(settings)).Should().Be("features");
        }
    }
}

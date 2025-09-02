using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;
using System.Text;
using Unleash.Internal;
using Unleash.Tests.Mock;

namespace Unleash.Tests.Internal
{
    public class CachedFilesLoader_Bootstrap_Tests : CachedFilesLoaderTestBase
    {
        private static string State = @"
        {
            ""version"": 2,
            ""features"": [
              {
                ""name"": ""one-enabled"",
                ""type"": ""release"",
                ""enabled"": true,
                ""impressionData"": false,
                ""strategies"": [
                  {
                    ""name"": ""userWithId"",
                    ""parameters"": {
                      ""userIds"": ""userA""
                    }
                  }
                ],
                ""variants"": [
                  {
                    ""name"": ""Aa"",
                    ""weight"": 33
                  },
                  {
                    ""name"": ""Aa"",
                    ""weight"": 33
                  },
                  {
                    ""name"": ""Ab"",
                    ""weight"": 34,
                    ""overrides"": [
                      {
                        ""contextName"": ""context"",
                        ""values"": [""a"", ""b""]
                      }
                    ]
                  }
                ]
              },
              {
                ""name"": ""one-disabled"",
                ""type"": ""release"",
                ""enabled"": false,
                ""impression-data"": false,
                ""strategies"": [
                  {
                    ""name"": ""userWithId"",
                    ""parameters"": {
                      ""userIds"": ""userB""
                    }
                  }
                ]
              }
            ]
        }";

        [Test]
        public void Loads_From_Bootstrap_Provider_When_Backup_File_Is_Missing()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-v1-missing.json");
            string etagFileName = AppDataFile("etag-missing.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var bootstrapProviderFake = A.Fake<IToggleBootstrapProvider>();
            A.CallTo(() => bootstrapProviderFake.Read())
                .Returns(State);


            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,
                ToggleBootstrapProvider = bootstrapProviderFake
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            A.CallTo(() => bootstrapProviderFake.Read())
                .MustHaveHappenedOnceExactly();
            ensureResult.InitialETag.Should().Be(string.Empty);
            ensureResult.InitialState.Should().Be(State);
        }

        [Test]
        public void Loads_From_Bootstrap_Provider_When_Backup_File_Is_Missing_And_Returns_Null_When_Bootstrap_File_Returns_Null()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-v1-missing.json");
            string etagFileName = AppDataFile("etag-missing.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var bootstrapProviderFake = A.Fake<IToggleBootstrapProvider>();
            A.CallTo(() => bootstrapProviderFake.Read())
                .Returns(null);

            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,
                ToggleBootstrapProvider = bootstrapProviderFake
            };
            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            A.CallTo(() => bootstrapProviderFake.Read())
                .MustHaveHappenedOnceExactly();
            ensureResult.InitialETag.Should().Be(string.Empty);
            ensureResult.InitialState.Should().BeEmpty();
        }

        [Test]
        public void Default_Override_Calls_Bootstrap_Handler_When_Backup_File_Exists()
        {
            // Arrange
            string toggleFileName = AppDataFile("unleash-repo-v1.json");
            string etagFileName = AppDataFile("etag-missing.txt");
            var fileSystem = new FileSystem(Encoding.UTF8);
            var bootstrapProviderFake = A.Fake<IToggleBootstrapProvider>();
            A.CallTo(() => bootstrapProviderFake.Read())
                .Returns(State);
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,
                ToggleBootstrapProvider = bootstrapProviderFake
            };

            var fileLoader = new CachedFilesLoader(settings, null);

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            A.CallTo(() => bootstrapProviderFake.Read())
                .MustHaveHappened();
            ensureResult.InitialETag.Should().Be(string.Empty);
            ensureResult.InitialState.Should().Be(State);
        }

        [Test]
        public void Does_Not_Call_Bootstrap_Handler_When_Backup_File_Exists_And_Override_Is_False()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var bootstrapProviderFake = A.Fake<IToggleBootstrapProvider>();
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,
                ToggleBootstrapProvider = bootstrapProviderFake,
                BootstrapOverride = false
            };
            var fileLoader = new CachedFilesLoader(settings, null);
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleFilePath(settings), "{}");
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleETagFilePath(settings), "12345");


            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            A.CallTo(() => bootstrapProviderFake.Read())
                .MustNotHaveHappened();
            ensureResult.InitialETag.Should().Be("12345");
            ensureResult.InitialState.Should().Be("{}");
        }

        [Test]
        public void Default_Override_Null_Should_Not_Null_Out_Backup_Toggles()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,
                ToggleBootstrapProvider = null
            };
            var fileLoader = new CachedFilesLoader(settings, null);
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleFilePath(settings), "{}");
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleETagFilePath(settings), "12345");

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.InitialETag.Should().Be("12345");
            ensureResult.InitialState.Should().Be("{}");
        }

        [Test]
        public void Default_Override_Should_Not_Null_Out_Backup_Toggles_When_Bootstrap_Result_Is_Null()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var bootstrapProviderFake = A.Fake<IToggleBootstrapProvider>();
            A.CallTo(() => bootstrapProviderFake.Read())
                .Returns(null);
            var settings = new UnleashSettings
            {
                FileSystem = fileSystem,
                ToggleBootstrapProvider = bootstrapProviderFake
            };
            var fileLoader = new CachedFilesLoader(settings, null);
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleFilePath(settings), "{}");
            fileSystem.WriteAllText(CachedFilesLoader.GetFeatureToggleETagFilePath(settings), "12345");

            // Act
            var ensureResult = fileLoader.Load();

            // Assert
            ensureResult.InitialETag.Should().Be("12345");
            ensureResult.InitialState.Should().Be("{}");
        }
    }
}

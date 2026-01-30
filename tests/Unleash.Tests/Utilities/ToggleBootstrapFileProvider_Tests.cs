using FluentAssertions;
using NUnit.Framework;
using Unleash.Tests.Mock;
using Unleash.Utilities;

namespace Unleash.Tests.Utilities
{
    public class ToggleBootstrapFileProvider_Tests
    {
        protected string AppDataFile(string filename)
        {
            var file = Path.Combine(TestContext.CurrentContext.TestDirectory, "App_Data", filename);
            return file;
        }

        [Test]
        public void Returns_String_Empty_When_File_Does_Not_Exist()
        {
            // Arrange
            string toggleFileName = "unleash-repo-v1-missing.json";
            var toggleFileProvider = new ToggleBootstrapFileProvider(toggleFileName, new UnleashSettings() { FileSystem = new MockFileSystem() });

            // Act
            var emptyResult = toggleFileProvider.Read();

            // Assert
            emptyResult.Should().BeEmpty();
        }

        [Test]
        public void Returns_File_Content_When_File_Exists()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            string toggleFileName = "unleash-repo-v1.json";
            string fileContent = "{ \"toggles\": [] }";

            fileSystem.WriteAllText(toggleFileName, fileContent);
            var toggleFileProvider = new ToggleBootstrapFileProvider(toggleFileName, new UnleashSettings() { FileSystem = fileSystem });

            // Act
            var result = toggleFileProvider.Read();

            // Assert
            result.Should().Be(fileContent);
        }

        [Test]
        public void Returns_File_Content_When_Configured_Through_Settings_And_File_Exists()
        {
            // Arrange
            var settings = new UnleashSettings();
            var toggleFileName = "unleash-repo-v1.json";
            var fileContent = "{ \"toggles\": [] }";
            var fileSystem = new MockFileSystem();
            fileSystem.WriteAllText(toggleFileName, fileContent);

            settings.UseBootstrapFileProvider(toggleFileName);
            settings.FileSystem = fileSystem;

            // Act
            var result = settings.ToggleBootstrapProvider.Read();

            // Assert
            result.Should().Be(fileContent);
        }
    }
}
using FluentAssertions;
using NUnit.Framework;

namespace Unleash.Tests
{
    public class UnleashSettingsTests
    {
        [Test]
        public void Should_set_sdk_name()
        {
            // Act
            var settings = new UnleashSettings();

            // Assert
            settings.SdkVersion.Should().StartWith("unleash-dotnet-sdk:");
            settings.SdkVersion.Should().MatchRegex(@":\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$");
        }
    }
}
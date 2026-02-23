using FluentAssertions;
using NUnit.Framework;

namespace Unleash.Tests
{
    internal class HeaderProvider : IUnleashCustomHttpHeaderProvider
    {
        public Dictionary<string, string> CustomHeaders { get; }= new Dictionary<string, string>() { ["Authorization"] = "*:production.asdasdads" };
    }

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

        [Test]
        public void HeaderProvider_Overrides_CustomHeaders()
        {
            var token = "*:development.asdasdads";

            var settings = new UnleashSettings
            {
                CustomHttpHeaders = new Dictionary<string, string>()
                {
                    ["Authorization"] = token
                },
                UnleashCustomHttpHeaderProvider = new HeaderProvider()
            };
            var environment = settings.GetTokenEnvironment();
            environment.Should().Be("production");
        }

        [Test]
        public void GetTokenEnvironment_Locates_Environment_In_Api_Token()
        {
            var token = "*:production.asdasdads";
            var settings = new UnleashSettings()
            {
                CustomHttpHeaders = new Dictionary<string, string>()
                {
                    { "Authorization", token }
                }
            };
            var environment = settings.GetTokenEnvironment();
            environment.Should().Be("production");
        }

        [Test]
        public void GetTokenEnvironment_Returns_Null_When_No_Token()
        {
            var settings = new UnleashSettings()
            {
                CustomHttpHeaders = new Dictionary<string, string>()
                {
                    { "Authorization", "token" }
                }
            };
            var environment = settings.GetTokenEnvironment();
            environment.Should().Be("default");
        }

        [Test]
        public void GetTokenEnvironment_Returns_Null_For_Different_Format_Token()
        {
            var settings = new UnleashSettings();
            var environment = settings.GetTokenEnvironment();
            environment.Should().Be("default");
        }
    }
}
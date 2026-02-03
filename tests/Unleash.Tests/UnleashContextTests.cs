using FluentAssertions;
using NUnit.Framework;

namespace Unleash.Tests
{
    public class UnleashContextTests
    {
        [Test]
        public void Should_build_context_with_fields_set()
        {
            // Act
            var context = new UnleashContext.Builder()
                .UserId("test@gmail.com")
                .SessionId("123")
                .RemoteAddress("127.0.0.1")
                .Environment("prod")
                .AppName("myapp")
                .AddProperty("test", "me")
                .Build();

            // Assert
            context.UserId.Should().Be("test@gmail.com");
            context.SessionId.Should().Be("123");
            context.RemoteAddress.Should().Be("127.0.0.1");
            context.Environment.Should().Be("prod");
            context.AppName.Should().Be("myapp");
            context.Properties["test"].Should().Be("me");
        }

        [Test]
        public void Should_apply_static_fields()
        {
            // Arrange
            var context = new UnleashContext.Builder()
                .UserId("test@gmail.com")
                .SessionId("123")
                .RemoteAddress("127.0.0.1")
                .AddProperty("test", "me")
                .Build();

            // Act
            var enhancedContext = context.ApplyStaticFields(new UnleashSettings
            {
                AppName = "someapp"
            });

            // Assert
            enhancedContext.UserId.Should().Be("test@gmail.com");
            enhancedContext.SessionId.Should().Be("123");
            enhancedContext.RemoteAddress.Should().Be("127.0.0.1");
            enhancedContext.AppName.Should().Be("someapp");
            enhancedContext.Properties["test"].Should().Be("me");
        }

        [Test]
        public void Should_not_override_static_fields()
        {
            // Arrange
            var context = new UnleashContext.Builder()
                .UserId("test@gmail.com")
                .SessionId("123")
                .RemoteAddress("127.0.0.1")
                .Environment("prod")
                .AppName("myapp")
                .AddProperty("test", "me")
                .Build();

            // Act
            var enhancedContext = context.ApplyStaticFields(new UnleashSettings
            {
                AppName = "someapp"
            });

            // Assert
            enhancedContext.UserId.Should().Be("test@gmail.com");
            enhancedContext.SessionId.Should().Be("123");
            enhancedContext.RemoteAddress.Should().Be("127.0.0.1");
            enhancedContext.Environment.Should().Be("prod");
            enhancedContext.AppName.Should().Be("myapp");
            enhancedContext.Properties["test"].Should().Be("me");
        }

        [Test]
        public void GetByName_Should_Return_CurrentTime_For_currentTime_Context_Name()
        {
            var date = new DateTimeOffset(2024, 10, 23, 15, 0, 0, TimeSpan.FromHours(-4));
            var context = new UnleashContext.Builder()
                .CurrentTime(date)
                .Build();

            var value = context.GetByName("currentTime");
            value.Should().Be(date.ToString("O"));
        }

        [Test]
        public void GetByName_Should_Return_UtcNow_For_currentTime_Context_Name_When_CurrentTime_Is_Not_Set()
        {
            var context = new UnleashContext();

            var value = context.GetByName("currentTime");
            var parsedValue = DateTimeOffset.Parse(value);
            parsedValue.Should().BeLessThan(TimeSpan.FromSeconds(1)).Before(DateTimeOffset.UtcNow);
        }

        [Test]
        public void GetTokenEnvironment_Locates_Environment_In_Api_Token()
        {
            var context = new UnleashContext();
            var token = "*:production.asdasdads";
            var settings = new UnleashSettings()
            {
                CustomHttpHeaders = new Dictionary<string, string>()
                {
                    { "Authorization", token }
                }
            };
            var environment = context.GetTokenEnvironment(settings);
            environment.Should().Be("production");
        }

        [Test]
        public void GetTokenEnvironment_Returns_Null_When_No_Token()
        {
            var context = new UnleashContext();
            var settings = new UnleashSettings()
            {
                CustomHttpHeaders = new Dictionary<string, string>()
                {
                    { "Authorization", "token" }
                }
            };
            var environment = context.GetTokenEnvironment(settings);
            environment.Should().Be("default");
        }

        [Test]
        public void GetTokenEnvironment_Returns_Null_For_Different_Format_Token()
        {
            var context = new UnleashContext();
            var settings = new UnleashSettings();
            var environment = context.GetTokenEnvironment(settings);
            environment.Should().Be("default");
        }
    }
}
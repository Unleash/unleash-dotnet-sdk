using FluentAssertions;
using NUnit.Framework;
using Unleash.ClientFactory;

namespace Unleash.Tests
{

    public class ExampleTests
    {
        private IUnleash unleash;

        [SetUp]
        public async Task Setup()
        {
            var factory = new UnleashClientFactory();
            unleash = await factory.CreateClientAsync(new MockedUnleashSettings(instanceTag: "test instance ExampleTests"), true);
        }

        [Test]
        public void UserAEnabled()
        {
            unleash.IsEnabled("one-enabled")
                .Should().BeTrue();
        }

        [Test]
        public void DisabledFeature()
        {
            unleash.IsEnabled("one-disabled")
                .Should().BeFalse();
        }

        [TearDown]
        public void Dispose()
        {
            unleash?.Dispose();
        }
    }
}
using FluentAssertions;
using NUnit.Framework;
using Unleash.Internal;

namespace Unleash.Tests
{
    public class FakeUnleashTests
    {
        [Test]
        public void IsEnabled_ReturnsFalse_WhenToggleNotSet_AndAllDisabled()
        {
            var unleash = new FakeUnleash();
            unleash.DisableAllToggles();
            unleash.IsEnabled("unknown").Should().BeFalse();
        }

        [Test]
        public void IsEnabled_ReturnsTrue_WhenAllEnabled()
        {
            var unleash = new FakeUnleash();
            unleash.EnableAllToggles();
            unleash.IsEnabled("any").Should().BeTrue();
        }

        [Test]
        public void IsEnabled_ReturnsSetValue()
        {
            var unleash = new FakeUnleash();
            unleash.SetToggle("featureA", true);
            unleash.IsEnabled("featureA").Should().BeTrue();
            unleash.SetToggle("featureA", false);
            unleash.IsEnabled("featureA").Should().BeFalse();
        }

        [Test]
        public void IsEnabled_WithDefaultSetting_ReturnsDefaultIfNotSet()
        {
            var unleash = new FakeUnleash();
            unleash.IsEnabled("featureB", true).Should().BeTrue();
            unleash.IsEnabled("featureB", false).Should().BeFalse();
        }

        [Test]
        public void IsEnabled_WithContext_DelegatesToIsEnabled()
        {
            var unleash = new FakeUnleash();
            unleash.SetToggle("featureC", true);
            var context = new UnleashContext();
            unleash.IsEnabled("featureC", context).Should().BeTrue();
        }

        [Test]
        public void IsEnabled_WithContextAndDefault_DelegatesToIsEnabledWithDefault()
        {
            var unleash = new FakeUnleash();
            var context = new UnleashContext();
            unleash.IsEnabled("featureD", context, true).Should().BeTrue();
            unleash.IsEnabled("featureD", context, false).Should().BeFalse();
        }

        [Test]
        public void GetVariant_ReturnsDisabledVariant_IfNotSet()
        {
            var unleash = new FakeUnleash();
            unleash.GetVariant("unknown").Should().Be(Variant.DISABLED_VARIANT);
        }

        [Test]
        public void GetVariant_ReturnsSetVariant()
        {
            var unleash = new FakeUnleash();
            var variant = new Variant("A", null, true, true);
            unleash.SetVariant("featureE", variant);
            unleash.GetVariant("featureE").Should().Be(variant);
        }

        [Test]
        public void GetVariant_WithDefault_ReturnsDefaultIfNotSet()
        {
            var unleash = new FakeUnleash();
            var defaultVariant = new Variant("default", null, false, false);
            unleash.GetVariant("featureF", defaultVariant).Should().Be(defaultVariant);
        }

        [Test]
        public void GetVariant_WithContext_DelegatesToGetVariant()
        {
            var unleash = new FakeUnleash();
            var variant = new Variant("B", null, true, true);
            unleash.SetVariant("featureG", variant);
            var context = new UnleashContext();
            unleash.GetVariant("featureG", context).Should().Be(variant);
        }

        [Test]
        public void GetVariant_WithContextAndDefault_DelegatesToGetVariantWithDefault()
        {
            var unleash = new FakeUnleash();
            var defaultVariant = new Variant("default2", null, false, false);
            var context = new UnleashContext();
            unleash.GetVariant("featureH", context, defaultVariant).Should().Be(defaultVariant);
        }

        [Test]
        public void ListKnownToggles_ReturnsAllSetToggles()
        {
            var unleash = new FakeUnleash();
            unleash.SetToggle("featureI", true);
            unleash.SetToggle("featureJ", false);
            var toggles = unleash.ListKnownToggles();
            toggles.Should().ContainSingle(t => t.Name == "featureI");
            toggles.Should().ContainSingle(t => t.Name == "featureJ");
        }

        [Test]
        public void ConfigureEvents_DoesNotThrow()
        {
            var unleash = new FakeUnleash();
            var act = () => unleash.ConfigureEvents(_ => { });
            act.Should().NotThrow();
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            var unleash = new FakeUnleash();
            var act = () => unleash.Dispose();
            act.Should().NotThrow();
        }
    }
}

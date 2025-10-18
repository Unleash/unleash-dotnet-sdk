using System;
using System.Collections.Generic;
using System.Linq;
using Unleash.Internal;

namespace Unleash
{
    /// <summary>
    /// A fake implementation of IUnleash for testing purposes.
    /// Allows setting toggle states and variants manually.
    /// </summary>
    /// <remarks>
    /// Does not currently implement event handling or disposal logic.
    /// </remarks>
    public class FakeUnleash : IUnleash
    {
        private bool allEnabled = false;
        private readonly Dictionary<string, bool> toggles = new Dictionary<string, bool>();
        private readonly Dictionary<string, Variant> variants = new Dictionary<string, Variant>();

        /// <summary>
        /// Enables all toggles, regardless of individual settings.
        /// </summary>
        public void EnableAllToggles() => allEnabled = true;

        /// <summary>
        /// Disables all toggles, regardless of individual settings.
        /// </summary>
        public void DisableAllToggles() => allEnabled = false;

        /// <summary>
        /// Sets the state of a specific toggle.
        /// </summary>
        /// <param name="toggleName">
        /// The name of the toggle to set.
        /// </param>
        /// <param name="isEnabled">
        /// True to enable the toggle, false to disable it.
        /// </param>
        public void SetToggle(string toggleName, bool isEnabled) => toggles[toggleName] = isEnabled;

        /// <summary>
        /// Sets the variant for a specific toggle.
        /// </summary>
        /// <param name="toggleName">
        /// The name of the toggle to set the variant for.
        /// </param>
        /// <param name="variant">
        /// The variant to associate with the toggle.
        /// </param>
        public void SetVariant(string toggleName, Variant variant) => variants[toggleName] = variant;

        // IUnleash implementation

        public bool IsEnabled(string toggleName) =>
          allEnabled || (toggles.ContainsKey(toggleName) && toggles[toggleName]);

        public bool IsEnabled(string toggleName, bool defaultSetting) =>
          toggles.ContainsKey(toggleName) ? toggles[toggleName] : defaultSetting;

        public bool IsEnabled(string toggleName, UnleashContext context) => IsEnabled(toggleName);

        public bool IsEnabled(string toggleName, UnleashContext context, bool defaultSetting) =>
          IsEnabled(toggleName, defaultSetting);

        public Variant GetVariant(string toggleName) => GetVariant(toggleName, Variant.DISABLED_VARIANT);

        public Variant GetVariant(string toggleName, Variant defaultValue) =>
          variants.ContainsKey(toggleName) ? variants[toggleName] : defaultValue;

        public Variant GetVariant(string toggleName, UnleashContext context) => GetVariant(toggleName);

        public Variant GetVariant(string toggleName, UnleashContext context, Variant defaultValue) =>
          GetVariant(toggleName, defaultValue);

        public ICollection<ToggleDefinition> ListKnownToggles() =>
          toggles.Keys.Select(name => new ToggleDefinition(name, "Default", "SomeType")).ToList();

        public void ConfigureEvents(Action<EventCallbackConfig> config) { }

        public void Dispose() { }
    }
}

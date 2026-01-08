using System;
using System.Threading.Tasks;
using Unleash.Events;

namespace Unleash.Internal
{
    public class EventCallbackConfig
    {
        public Action<ImpressionEvent> ImpressionEvent { get; set; }
        public Action<ErrorEvent> ErrorEvent { get; set; }
        public Action<TogglesUpdatedEvent> TogglesUpdatedEvent { get; set; }
        public Action<ReadyEvent> ReadyEvent { get; set; }

        internal void RaiseReady(ReadyEvent evt)
        {
            ReadyEvent?.Invoke(evt);
        }

        internal void RaiseError(ErrorEvent evt)
        {
            if (ErrorEvent != null)
            {
                ErrorEvent(evt);
            }
        }

        internal void RaiseTogglesUpdated(TogglesUpdatedEvent evt)
        {
            if (TogglesUpdatedEvent != null)
            {
                TogglesUpdatedEvent(evt);
            }
        }

        internal void EmitImpressionEvent(string type, UnleashContext context, bool enabled, string name, string variant = null)
        {
            ImpressionEvent?.Invoke(new ImpressionEvent
            {
                Type = type,
                Context = context,
                EventId = Guid.NewGuid().ToString(),
                Enabled = enabled,
                FeatureName = name,
                Variant = variant
            });
        }
    }
}

using System;
using Unleash.Events;

namespace Unleash.Internal
{
    public class EventCallbackConfig
    {
        public Action<ImpressionEvent> ImpressionEvent { get; set; }
        public Action<ErrorEvent> ErrorEvent { get; set; }
        public Action<TogglesUpdatedEvent> TogglesUpdatedEvent { get; set; }

        [Obsolete("This API is no longer public in version 6. Details can be found in the v6_MIGRATION_GUIDE.md in the SDK repository.", false)]
        public void RaiseError(ErrorEvent evt)
        {
            if (ErrorEvent != null)
            {
                ErrorEvent(evt);
            }
        }

        [Obsolete("This API is no longer public in version 6. Details can be found in the v6_MIGRATION_GUIDE.md in the SDK repository.", false)]
        public void RaiseTogglesUpdated(TogglesUpdatedEvent evt)
        {
            if (TogglesUpdatedEvent != null)
            {
                TogglesUpdatedEvent(evt);
            }
        }

    }
}

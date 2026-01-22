using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LaunchDarkly.EventSource;

namespace Unleash.Streaming
{
    internal class StreamingFailoverStrategy
    {
        public static readonly string[] FAILOVER_SERVER_HINTS = new [] { "polling" };
        public static readonly int[] HARD_FAILOVER_STATUS_CODES = new [] { 401, 403, 404, 429, 501 };
        public static readonly int[] SOFT_FAILOVER_STATUS_CODES = new [] { 408, 500, 502, 503, 504 };
        private readonly int maxFailuresUntilFailover;
        private readonly int failureWindowMs;
        private List<FailEventArgs> failEvents = new List<FailEventArgs>();
        private object modifyFailEventsLock = new object();

        public StreamingFailoverStrategy(int maxFailuresUntilFailover = 5, int failureWindowMs = 60_000)
        {
            this.maxFailuresUntilFailover = maxFailuresUntilFailover;
            this.failureWindowMs = failureWindowMs;
        }

        public bool ShouldFailOver(FailEventArgs failEvent, DateTimeOffset? now = null)
        {
            if (now == null)
            {
                now = DateTimeOffset.UtcNow;
            }

            switch (failEvent.Type)
            {
                case FailEventType.Network:
                    return true;
                case FailEventType.HttpStatus:
                    var statusCode = (failEvent as HttpStatusFailEventArgs).StatusCode;
                    if (HARD_FAILOVER_STATUS_CODES.Contains(statusCode))
                    {
                        return true;
                    }
                    else if (SOFT_FAILOVER_STATUS_CODES.Contains(statusCode))
                    {
                        return HasTooManyFails(failEvent, now.Value);
                    }
                    break;
                case FailEventType.ServerHint:
                    return FAILOVER_SERVER_HINTS.Contains((failEvent as ServerHintFailEventArgs).Hint);
            }

            return false;
        }

        private bool HasTooManyFails(FailEventArgs failEvent, DateTimeOffset now)
        {
            var cutoff = now.AddMilliseconds(failureWindowMs * -1);

            // Copies elements from old list, then atomically replaces the list reference with the new list
            lock (modifyFailEventsLock)
            {
                var newList = failEvents
                    .Where(ev => ev.OccurredAt > cutoff)
                    .ToList();
                newList.Add(failEvent);
                failEvents = newList;
                return newList.Count > maxFailuresUntilFailover;
            }
        }
    }
}

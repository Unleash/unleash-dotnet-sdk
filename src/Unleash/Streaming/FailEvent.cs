using System;

namespace Unleash.Streaming
{
    internal enum FailEventType
    {
        Network,
        HttpStatus,
        ServerHint
    }

    internal abstract class FailEventArgs : EventArgs
    {
        public abstract FailEventType Type { get; }
        public string Message { get; set; }
        public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    }

    internal class NetworkEventErrorArgs : FailEventArgs
    {
        public override FailEventType Type => FailEventType.Network;
    }

    internal class HttpStatusFailEventArgs : FailEventArgs
    {
        public override FailEventType Type => FailEventType.HttpStatus;

        public int StatusCode { get; set; }
    }

    internal class ServerHintFailEventArgs : FailEventArgs
    {
        public override FailEventType Type => FailEventType.ServerHint;

        public string Hint { get; set; }
    }
}

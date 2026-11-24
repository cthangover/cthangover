namespace Cthangover.Core.Mods.Host
{
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The transport-independent unit the host works with. Transports move packets, the host interprets
    /// payloads; neither side sees the other's concerns. The connection number is assigned by the owning
    /// transport and stays stable for the lifetime of one client connection; topic packets carry no
    /// connection and are re-addressed to a subscribed transport's sink.
    /// </summary>
    public sealed class DebugHostPacket
    {
        /// <summary>Packet direction inside the bus.</summary>
        public DebugHostPacketKind Kind { get; }

        /// <summary>Owning client connection; zero means "no particular connection" (topic packets).</summary>
        public long Connection { get; }

        /// <summary>Event topic for <see cref="DebugHostPacketKind.Event"/> packets; empty otherwise.</summary>
        public string Topic { get; }

        /// <summary>Detached JSON payload (command request, command result, or event record).</summary>
        public JsonObject Payload { get; }

        // Awaited through the direct request API. Topic and addressed event packets do not need a waiter.
        internal TaskCompletionSource<JsonObject> Completion { get; }
        internal CancellationToken Cancellation { get; }
        internal DebugHostOperation Operation { get; }

        /// <summary>Associates an event or transport-owned request with its routing information.</summary>
        public DebugHostPacket(DebugHostPacketKind kind, long connection, string topic, JsonObject payload)
            : this(kind, connection, topic, payload, null, default, null)
        {
        }

        internal DebugHostPacket(DebugHostPacketKind kind, long connection, string topic, JsonObject payload,
            TaskCompletionSource<JsonObject> completion, CancellationToken cancellation, DebugHostOperation operation)
        {
            Kind = kind;
            Connection = connection;
            Topic = topic;
            Payload = payload;
            Completion = completion;
            Cancellation = cancellation;
            Operation = operation;
        }
    }
}

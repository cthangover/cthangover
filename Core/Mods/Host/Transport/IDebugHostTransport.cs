namespace Cthangover.Core.Mods.Host
{
    using System.Threading.Tasks;

    /// <summary>
    /// A physical channel strategy of the debug host. The transport owns connect/accept, framing and
    /// per-connection I/O. It uses the shared request bus to retrieve results without touching game state.
    /// Implementations live in <c>Core.Mods.Host.Transport.Impls</c>.
    /// <para>
    /// Security contract — a transport decides who can reach the host, and the host runs code: accepted
    /// envelopes reach commands that compile and execute C# in this process. The implementation must
    /// therefore assume the user authorized everyone it accepts. A non-local implementation (TCP, UDP,
    /// WebSocket) is a deliberate security decision, not a convenience: disable it by default, require
    /// authentication before the first command, bind an explicit address instead of every interface, and
    /// treat protocol version or identity mismatch as a refusal. The shipped local pipe exists so the
    /// default path never reaches beyond the OS user boundary.
    /// </para>
    /// </summary>
    public interface IDebugHostTransport
    {
        /// <summary>Stable transport name used in logs and diagnostics (e.g. "local-pipe").</summary>
        string Name { get; }

        /// <summary>True when the channel cannot leave the machine (local pipes, loopback sockets).</summary>
        bool IsLocal { get; }

        /// <summary>The endpoint descriptor clients discover and connect through.</summary>
        HostEndpointDescriptor Endpoint { get; }

        /// <summary>
        /// Starts accept/read/write loops on background threads. May only throw when the transport cannot
        /// serve at all; losing one client connection must never surface as an exception.
        /// </summary>
        void Start(DebugHostBus bus);

        /// <summary>Stops accepting, drops connections and releases the endpoint; idempotent.</summary>
        void Stop();

        /// <summary>Stops the transport and joins its listener when the implementation owns one.</summary>
        Task StopAsync()
        {
            Stop();
            return Task.CompletedTask;
        }
    }
}

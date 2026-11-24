namespace Cthangover.Core.Mods.Host
{
    /// <summary>
    /// One transport-specific way to reach the debug host. The descriptor is intentionally opaque data: the
    /// transport knows how to form it and how to connect over it, discovery only publishes and filters it.
    /// <para>
    /// <see cref="Kind"/> is an open string, not an enumeration: transports are added by moving code (and
    /// later by mods), so the set of possible channels cannot be known when this type is written. The
    /// transport chooses the value, keeps it stable and lowercase by convention (<c>pipe</c> today,
    /// <c>tcp</c> when a network transport appears), and publishes it as the discovery document's
    /// <c>kind</c> field. Clients match that string exactly, so renaming a kind is a protocol change.
    /// </para>
    /// </summary>
    public sealed class HostEndpointDescriptor
    {
        /// <summary>Short open identifier of the channel, chosen and owned by the transport (e.g. <c>pipe</c>).</summary>
        public string Kind { get; }

        /// <summary>Pipe name, or "host:port" for TCP endpoints.</summary>
        public string Address { get; }

        /// <summary>Wire protocol version the endpoint speaks.</summary>
        public int Protocol { get; }

        /// <summary>Builds one descriptor.</summary>
        public HostEndpointDescriptor(string kind, string address, int protocol)
        {
            Kind = kind;
            Address = address;
            Protocol = protocol;
        }
    }
}

namespace Cthangover.Core.Mods.Host
{
    using System.Collections.Generic;

    /// <summary>
    /// Hides per-transport discovery specifics behind one contract. Each transport knows how to FORM its
    /// endpoint descriptor (a pipe has a name, a TCP endpoint has host and port), while the discovery
    /// implementation owns where and how descriptors are published and found. A client locates by the
    /// transport's own kind string and then connects using the transport that owns that kind — the kind is
    /// deliberately a string, not an enumeration, because transports are extensible.
    /// </summary>
    public interface IDebugHostDiscovery
    {
        /// <summary>Publishes every endpoint the host currently serves; replaces the previous set.</summary>
        void Publish(IReadOnlyList<HostEndpointDescriptor> endpoints, int processId);

        /// <summary>Returns published endpoints of the requested kind string, or an empty list when none.</summary>
        IReadOnlyList<HostEndpointDescriptor> Locate(string kind);
    }
}

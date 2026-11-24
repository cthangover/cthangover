namespace Cthangover.Core.Mods.Host
{
    /// <summary>Distinguishes a queued command, its addressed reply, and a topic event.</summary>
    public enum DebugHostPacketKind
    {
        /// <summary>A command envelope arriving from a caller.</summary>
        Request,

        /// <summary>A command result destined back to one connection.</summary>
        Response,

        /// <summary>An unsolicited debugging record published on a topic.</summary>
        Event
    }
}

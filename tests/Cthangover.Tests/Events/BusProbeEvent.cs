namespace Cthangover.Tests.Events
{
    using Cthangover.Core.Events;

    /// <summary>Event type only the event-bus tests publish, so no other subscriber interferes.</summary>
    public class BusProbeEvent : ModEvent
    {
    }
}

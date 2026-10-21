namespace Cthangover.Tests.Events
{
    using Cthangover.Core.Events;

    /// <summary>Lifecycle event type only the event-bus tests publish: its subscribers are ordered by priority.</summary>
    [LifecycleEvent]
    public class BusLifecycleProbeEvent : ModEvent
    {
    }
}

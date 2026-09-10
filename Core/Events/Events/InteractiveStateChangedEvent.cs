namespace Cthangover.Core.Events
{
    /// <summary>
    /// Marker fired when an interactive scene object is removed, published by
    /// <see cref="Cthangover.Core.Interactive.InteractiveManager.Remove"/> and
    /// <see cref="Cthangover.Core.Interactive.InteractiveManager.ClearAll"/>.
    /// Deliberately NOT published on <c>Add</c> — adding is idempotent, and
    /// publishing it would make "ensure"-scenarios with <c>interactive_add</c>
    /// retrigger themselves. Consumers use it to invalidate cached condition
    /// results that depend on interactive object presence.
    /// </summary>
    [EventKey("interactive_state_changed")]
    public class InteractiveStateChangedEvent : ModEvent
    {
        /// <summary>The definition ID of the removed interactive object.</summary>
        public string Id { get; init; }
    }
}

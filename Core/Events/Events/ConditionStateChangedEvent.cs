namespace Cthangover.Core.Events
{
    /// <summary>
    /// Fired by <see cref="Cthangover.Core.Scenes.ConditionStateMonitor"/> every time its
    /// <c>Version</c> grows: something scenario conditions read (flags, quest tags and
    /// statuses, interactives, map zones, a restored save) changed, or a mod forced it with
    /// <c>MarkStateChanged</c>. Event chains subscribe to it instead of polling the version.
    /// <para/>
    /// It may arrive in the middle of the change that caused it (some publishers raise their
    /// own event before storing the new value) and from a background thread during boot, so a
    /// subscriber must not evaluate conditions synchronously — schedule the work (e.g.
    /// <c>CallDeferred</c>) and read the state there.
    /// </summary>
    [EventKey("condition_state_changed")]
    public class ConditionStateChangedEvent : ModEvent
    {
        /// <summary>The monitor version after the change.</summary>
        public long Version { get; init; }
    }
}

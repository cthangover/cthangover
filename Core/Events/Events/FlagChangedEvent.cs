namespace Cthangover.Core.Events
{
    /// <summary>
    /// Fired when a named scenario flag changes value. Published by
    /// <see cref="Cthangover.Core.Scenes.ScenarioCondition.SetFlag"/> at the point
    /// of mutation, and only when the value actually changed. Flag values feed
    /// condition evaluation, so this event invalidates cached condition results.
    /// </summary>
    [EventKey("flag_changed")]
    public class FlagChangedEvent : ModEvent
    {
        /// <summary>The flag name (condition identifier).</summary>
        public string Key { get; init; }

        /// <summary>The new flag value.</summary>
        public string Value { get; init; }
    }
}

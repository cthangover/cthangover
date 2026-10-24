namespace Cthangover.Core.Events
{
    /// <summary>
    /// Stats of a character may have changed (a bonus, an archetype, a level, equipment, a
    /// trait source). UI redraws on it; <see cref="CharacterId"/> is null when anyone's
    /// stats may have changed (data reload, a trait source added or removed).
    /// </summary>
    [EventKey("stats_changed")]
    public class StatsChangedEvent : ModEvent
    {
        /// <summary>The character, or null for everyone.</summary>
        public string CharacterId { get; init; }
    }
}

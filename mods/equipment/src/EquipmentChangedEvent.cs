namespace Cthangover.Core.Events
{
    /// <summary>A character put something on or took it off (the stats already know; this is for UI and mods).</summary>
    [EventKey("equipment_changed")]
    public class EquipmentChangedEvent : ModEvent
    {
        /// <summary>The character.</summary>
        public string CharacterId { get; init; }
    }
}

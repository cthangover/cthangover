namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by <see cref="Cthangover.Core.UI.Tool.MapZoneService"/> whenever
    /// a zone's effective open or visible state changes (or the whole override
    /// set is replaced). <see cref="Cthangover.Core.Scenes.ConditionStateMonitor"/>
    /// subscribes to it so scenario header conditions that depend on map
    /// progression re-evaluate without a scene reload; the map widget subscribes
    /// to rebuild its layers.
    /// <para/>
    /// <see cref="ZoneId"/> may be empty for bulk changes (<see cref="MapZoneChangeKind.Reset"/>,
    /// <see cref="MapZoneChangeKind.Restore"/>, <see cref="MapZoneChangeKind.Reload"/>),
    /// meaning "rebuild everything" rather than a single zone.
    /// </summary>
    [EventKey("map_zone_changed")]
    public class MapZoneChangedEvent : ModEvent
    {
        /// <summary>ID of the affected zone, or empty for bulk changes.</summary>
        public string ZoneId { get; init; }

        /// <summary>One of the <see cref="MapZoneChangeKind"/> constants.</summary>
        public string Change { get; init; }
    }
}

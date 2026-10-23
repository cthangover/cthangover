using System;
using System.Collections.Generic;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Builds the event list of every map from event files read in mod load order.
    /// A new id is appended; an id already present (ignoring case) is replaced in
    /// place, so the later file wins and keeps the original's drawing order. Mods
    /// load after their dependencies, so the engine's replacement rule holds:
    /// ship the same event id and declare <c>depends</c> on the owner to replace
    /// it, ship a new id to add to any mod's map.
    /// <para/>
    /// Pure data, so the merge rules are tested without a mod set.
    /// </summary>
    public class RpgMapEventMerge
    {
        /// <summary>Events per map id (case-insensitive).</summary>
        public Dictionary<string, List<RpgMapEvent>> Maps { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Adds the events of one file of <paramref name="modId"/> for map <paramref name="map"/>;
        /// each event gets <see cref="RpgMapEvent.ModId"/> and <see cref="RpgMapEvent.File"/>.
        /// Returns the ids that replaced an earlier event.
        /// </summary>
        public List<string> Add(string map, string modId, string file, IEnumerable<RpgMapEvent> events)
        {
            var replaced = new List<string>();
            if (!Maps.TryGetValue(map, out var list))
            {
                list = new List<RpgMapEvent>();
                Maps[map] = list;
            }

            foreach (var mapEvent in events ?? Array.Empty<RpgMapEvent>())
            {
                if (mapEvent == null || string.IsNullOrWhiteSpace(mapEvent.Id))
                    continue;
                mapEvent.ModId = modId;
                mapEvent.File = file;
                var index = list.FindIndex(e => string.Equals(e.Id, mapEvent.Id, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    list.Add(mapEvent);
                    continue;
                }
                list[index] = mapEvent;
                replaced.Add(mapEvent.Id);
            }
            return replaced;
        }
    }
}

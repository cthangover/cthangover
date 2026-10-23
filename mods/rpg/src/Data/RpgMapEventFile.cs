using System.Collections.Generic;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// One file of map events, <c>map_events/&lt;map id&gt;/&lt;any name&gt;.json</c>:
    /// <c>{"Events": [ … ]}</c>. The folder names the map, so a file holds the events
    /// of one map, and a mod may split them over as many files as it likes.
    /// </summary>
    public class RpgMapEventFile
    {
        /// <summary>The events of the file, in drawing order.</summary>
        public List<RpgMapEvent> Events { get; set; } = new();
    }
}

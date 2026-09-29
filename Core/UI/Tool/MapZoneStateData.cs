using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Save-format DTO for <see cref="MapZoneService"/> runtime overrides. Both
    /// dictionaries store only zones whose live state differs from the
    /// authoring defaults (<see cref="MapZone.Locked"/> / <see cref="MapZone.Hidden"/>),
    /// keyed by zone <see cref="MapZone.ID"/>.
    /// <para/>
    /// Storing deltas instead of the full effective state keeps saves small and,
    /// more importantly, lets a mod update its authoring defaults without the
    /// save silently reverting them: a zone the player never touched always
    /// follows the current JSON. Unknown IDs are tolerated on load — they simply
    /// never match a zone and cost nothing if a mod is temporarily disabled.
    /// </summary>
    public class MapZoneStateData
    {
        /// <summary>
        /// Explicit open/closed overrides. <c>true</c> — zone was opened by a
        /// scenario, <c>false</c> — closed; absent IDs follow the authoring default.
        /// </summary>
        [JsonPropertyName("open")]
        public Dictionary<string, bool> Open { get; set; } = new();

        /// <summary>
        /// Explicit shown/hidden overrides. <c>true</c> — zone was revealed by
        /// a scenario, <c>false</c> — hidden; absent IDs follow the authoring default.
        /// </summary>
        [JsonPropertyName("visible")]
        public Dictionary<string, bool> Visible { get; set; } = new();
    }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Something placed on a map cell that reacts to the player, in the spirit of
    /// RPG Maker events: an NPC, a sign, a chest, a door. What it looks like and
    /// what it does are independent:
    /// <list type="bullet">
    /// <item>look — a charset <see cref="Sprite"/>, or one atlas <see cref="Tile"/>, or an item <see cref="Icon"/>, or nothing (an invisible trigger);</item>
    /// <item>effect — a <see cref="Teleport"/>, handled natively, or a dialog built
    /// from a <see cref="Scenario"/> file followed by inline <see cref="Commands"/>
    /// (the full scenario DSL, so <c>action rpg.battle …</c>, <c>flag …</c>, quests).</item>
    /// </list>
    /// A <see cref="Condition"/> in the scenario condition language hides the
    /// event while false; it is re-checked whenever game state changes, so a
    /// defeated boss (<c>flag boss_dead 1</c>) disappears at once.
    /// <para/>
    /// Events are stored apart from their map, in <c>map_events/&lt;map&gt;/*.json</c>
    /// of any mod (<see cref="RpgMapEventResolver"/>).
    /// </summary>
    public class RpgMapEvent
    {
        /// <summary>Id unique within the map.</summary>
        public string Id { get; set; }

        /// <summary>Column.</summary>
        public int X { get; set; }

        /// <summary>Row.</summary>
        public int Y { get; set; }

        /// <summary>Charset image in the <c>charsets</c> group (3 frames × 4 facings).</summary>
        public string Sprite { get; set; }

        /// <summary>Atlas index of the map's tileset drawn as the event when it has no sprite; -1 for none.</summary>
        public int Tile { get; set; } = -1;

        /// <summary>
        /// Picture in the <c>items</c> group (what an item's <c>Sprite</c> names), drawn fitted into the
        /// cell when the event has neither a sprite nor a tile: an item lying on the ground.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>Initial facing of a sprite.</summary>
        public RpgDirection Dir { get; set; } = RpgDirection.Down;

        /// <summary>When the event fires.</summary>
        public RpgEventTrigger Trigger { get; set; } = RpgEventTrigger.Action;

        /// <summary>The event occupies its cell. Null means "solid when it is visible" (a sprite, a tile or an icon).</summary>
        public bool? Solid { get; set; }

        /// <summary>Scenario path inside the <c>scenarios</c> group (extension optional), played as a dialog.</summary>
        public string Scenario { get; set; }

        /// <summary>Inline DSL lines appended after the scenario.</summary>
        public List<string> Commands { get; set; }

        /// <summary>Moves the player instead of starting a dialog.</summary>
        public RpgTeleport Teleport { get; set; }

        /// <summary>Scenario condition; the event exists only while it is true.</summary>
        public string Condition { get; set; }

        /// <summary>Mod the event was read from (set by the event resolver, not stored).</summary>
        [JsonIgnore]
        public string ModId { get; set; }

        /// <summary>Mod-relative path of the file the event was read from (set by the event resolver, not stored).</summary>
        [JsonIgnore]
        public string File { get; set; }

        /// <summary>Effective solidity: explicit value, else whether the event is drawn.</summary>
        [JsonIgnore]
        public bool IsSolid => Solid ?? (!string.IsNullOrEmpty(Sprite) || Tile >= 0 || !string.IsNullOrEmpty(Icon));
    }
}

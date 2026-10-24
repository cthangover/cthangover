using System.Collections.Generic;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// One page of a map event, as in RPG Maker: the event's look and effect while the
    /// page's <see cref="Condition"/> holds. Every field left out (null) is the event's
    /// own; an empty <see cref="Sprite"/>/<see cref="Icon"/> clears it. The last page whose
    /// condition holds is the active one; the event's own fields are page 0 and count only
    /// while the event's own condition holds (<see cref="RpgEventPages"/>).
    /// </summary>
    public class RpgEventPage
    {
        /// <summary>Scenario condition of the page; empty = always.</summary>
        public string Condition { get; set; }

        /// <summary>Charset of the page ("" = none).</summary>
        public string Sprite { get; set; }

        /// <summary>Atlas tile of the page (-1 = none).</summary>
        public int? Tile { get; set; }

        /// <summary>Item icon of the page ("" = none).</summary>
        public string Icon { get; set; }

        /// <summary>Facing the event turns to when the page becomes active.</summary>
        public RpgDirection? Dir { get; set; }

        /// <summary>How the page starts.</summary>
        public RpgEventTrigger? Trigger { get; set; }

        /// <summary>Whether the page blocks walking.</summary>
        public bool? Solid { get; set; }

        /// <summary>Scenario file of the page.</summary>
        public string Scenario { get; set; }

        /// <summary>Inline scenario lines of the page.</summary>
        public List<string> Commands { get; set; }

        /// <summary>Teleport of the page.</summary>
        public RpgTeleport Teleport { get; set; }

        /// <summary>Autonomous movement of the page.</summary>
        public RpgMoveRoute Route { get; set; }

        /// <summary>Light of the page (a lamp lit by a page).</summary>
        public RpgLight Light { get; set; }
    }
}

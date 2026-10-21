using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// One tile map of the RPG world. The map id is also the name of the logical
    /// scene that shows it: entering scene <c>X</c> (any <c>switch_scene X</c>, a
    /// teleport, the return from a battle, loading a save) mounts map <c>X</c>
    /// over the shell. Because the scene stays an ordinary scene, everything the
    /// engine attaches to scenes works on a map unchanged — entry
    /// scenarios, ambience and music from <c>scenes/X.json</c>, the default
    /// background (which also becomes the backdrop of battles started here),
    /// saving rules.
    /// <para/>
    /// Files declare <c>"Kind": "rpg_map"</c>. Layers are parsed by
    /// <see cref="RpgMapGrid"/>; a dependent mod replaces a whole map by id, the
    /// usual entity override.
    /// </summary>
    [EntityKind("rpg_map")]
    public class RpgMap : IIdentifiable
    {
        /// <summary>Map id = logical scene name.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key of the map's display name.</summary>
        public string Name { get; set; }

        /// <summary>Id of the <see cref="RpgTileset"/> the indices refer to.</summary>
        public string Tileset { get; set; }

        /// <summary>Columns.</summary>
        public int Width { get; set; }

        /// <summary>Rows.</summary>
        public int Height { get; set; }

        /// <summary>Screen pixels per tile pixel; pixel art is scaled by a whole number.</summary>
        public int Zoom { get; set; } = 2;

        /// <summary>The three drawn layers.</summary>
        public RpgMapLayers Layers { get; set; } = new();

        /// <summary>Rows of <see cref="RpgCollision"/> codes, one character per cell; may be empty.</summary>
        public List<string> Collision { get; set; } = new();

        /// <summary>Where the player appears when entering the map without a teleport target.</summary>
        public RpgTeleport Spawn { get; set; } = new();

        /// <summary>Events placed on the map.</summary>
        public List<RpgMapEvent> Events { get; set; } = new();

        /// <summary>Random battles; null for a safe map.</summary>
        public RpgEncounterTable Encounters { get; set; }
    }
}

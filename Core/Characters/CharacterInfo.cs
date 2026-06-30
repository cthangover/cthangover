using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// JSON-serializable character descriptor loaded from data files. Flat
    /// structure — stats are individual int properties rather than nested
    /// objects — because JSON files authored by designers are easier to read
    /// and edit as flat key-value pairs. The Actions field is a string (not a
    /// list) because it carries IDs joined by a separator, parsed by the factory
    /// at load time. CharacterInfo is the serialization DTO; Character is the
    /// runtime model constructed from it by CharacterFactory.
    /// </summary>
    [Serializable]
    public class CharacterInfo : IIdentifiable
    {
        /// <summary>
        /// Unique character ID, serialized as "Id" in JSON. Serves as the
        /// factory lookup key and save-data identifier.
        /// </summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        /// <summary>
        /// AI behaviour class name for this character in battle. Resolved
        /// externally — null means no AI override.
        /// </summary>
        public string Behaviour { get; set; }
        /// <summary>
        /// Starting level for this character when recruited.
        /// </summary>
        public int    Level { get; set; }
        /// <summary>
        /// Resource path string for the full-body portrait. Resolved to a
        /// <c>Texture2D</c> by <see cref="CharacterFactory"/> at load time.
        /// </summary>
        public string Image { get; set; }
        /// <summary>
        /// Display name, localized via TranslationServer when shown to the
        /// player.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Starting health points (used as <see cref="Attribute"/>.BaseValue).
        /// </summary>
        public int    Health { get; set; }
        /// <summary>
        /// Starting defence value.
        /// </summary>
        public int    Defence { get; set; }
        /// <summary>
        /// Starting action points (resource pool for abilities).
        /// </summary>
        public int    Point { get; set; }
        /// <summary>
        /// Starting attack power.
        /// </summary>
        public int    Attack { get; set; }
        /// <summary>
        /// Starting strength stat (physical damage modifier).
        /// </summary>
        public int    Strength { get; set; }
        /// <summary>
        /// Starting magic stat (magical damage/heal modifier).
        /// </summary>
        public int    Magic { get; set; }
        /// <summary>
        /// Starting fullness (satiety/hunger system, depletes over time).
        /// </summary>
        public int    Fullness { get; set; }
        /// <summary>
        /// Starting depravity trait value (bipolar scale centered at 0,
        /// negative values represent more depraved).
        /// </summary>
        public int    Depravity { get; set; }
        /// <summary>
        /// Starting discipline trait value (bipolar scale centered at 0,
        /// positive values represent more disciplined).
        /// </summary>
        public int    Discipline { get; set; }
        /// <summary>
        /// Starting experience points.
        /// </summary>
        public int    Exp { get; set; }
        /// <summary>
        /// Recruitment chance percentage (0–100) after defeating this
        /// character in battle.
        /// </summary>
        public int    RecruitmentChance { get; set; }
        /// <summary>
        /// Action IDs joined by a separator (e.g. comma or semicolon).
        /// Parsed by <see cref="CharacterFactory"/> at load time — each
        /// ID references an <see cref="ActionCharacterInfo"/> entry in
        /// the action data file. Not a list because flat strings are
        /// easier for designers to edit in JSON.
        /// </summary>
        public string Actions { get; set; }
        /// <summary>
        /// Loot drop table for this character when defeated as an enemy.
        /// </summary>
        public List<LootEntry> Loot { get; set; }
    }

}

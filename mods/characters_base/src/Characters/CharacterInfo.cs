using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// JSON-serializable character descriptor loaded from data files.
    /// Only the four core battle stats (Health, Defence, Attack, Point)
    /// are hardcoded — they map directly to <see cref="Attribute"/> fields
    /// in <see cref="CharacterAttributes"/>. All other properties
    /// (any mod-defined extras) are captured by <see cref="ExtraProperties"/>
    /// and flowed into <see cref="CharacterAttributes.Properties"/> at
    /// factory time, so mods can add arbitrary stat keys without
    /// changing the DTO schema.
    /// </summary>
    [Serializable]
    [EntityKind("character")]
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
        /// Starting experience points.
        /// </summary>
        public int    Exp { get; set; }
        /// <summary>
        /// Recruitment chance percentage (0–100) after defeating this
        /// character in battle.
        /// </summary>
        public int    RecruitmentChance { get; set; }
        /// <summary>
        /// Action ID strings, each referencing an
        /// <see cref="ActionCharacterInfo"/> entry in the action data file.
        /// Stored as a JSON array for clean mod-patching with
        /// <c>$add</c>/<c>$remove</c>/<c>$set</c> suffix operations.
        /// </summary>
        public List<string> Actions { get; set; }
        /// <summary>
        /// Loot drop table for this character when defeated as an enemy.
        /// </summary>
        public List<LootEntry> Loot { get; set; }
        /// <summary>
        /// Captures any JSON keys not matched by the hardcoded properties
        /// above — including former DTO fields like any mod-defined extras.
        /// These are imported into <see cref="CharacterAttributes.Properties"/>
        /// by <see cref="CharacterFactory.ImportExtraProperty"/>.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> ExtraProperties { get; set; }
    }

}

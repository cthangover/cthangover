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
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public string Behaviour { get; set; }
        public int    Level { get; set; }
        public string Image { get; set; }
        public string Name { get; set; }
        public int    Health { get; set; }
        public int    Defence { get; set; }
        public int    Point { get; set; }
        public int    Attack { get; set; }
        public int    Strength { get; set; }
        public int    Magic { get; set; }
        public int    Fullness { get; set; }
        public int    Depravity { get; set; }
        public int    Discipline { get; set; }
        public int    Exp { get; set; }
        public int    RecruitmentChance { get; set; }
        public string Actions { get; set; }
        public List<LootEntry> Loot { get; set; }
    }

}

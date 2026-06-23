using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Characters
{

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

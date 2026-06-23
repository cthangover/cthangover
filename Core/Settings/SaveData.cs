using System;
using System.Collections.Generic;
using Cthangover.Core.Quests;
using Cthangover.Core.Relationship;

namespace Cthangover.Core.Settings
{
    public class SaveData
    {
        public long Time { get; set; }
        public float LampRadius { get; set; }
        public float LampInfluence { get; set; }
        public List<CharacterInfoData> Characters { get; set; }
        public List<QuestBase> Quests { get; set; }
        public List<Recruit> Recruits { get; set; }
        public List<CharacterType> BattleSet { get; set; }
        public List<CItem> Inventory { get; set; }
        public List<string> Recipes { get; set; }
        public string CurrentSceneName { get; set; }
        public DateTime SaveDateTime { get; set; }
    }
}

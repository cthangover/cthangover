using System;
using System.Collections.Generic;
using Cthangover.Core.Quests;
using Cthangover.Core.Relationship;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Flat serialization DTO that captures the entire runtime state of a
    /// playthrough. Serialized to JSON by <see cref="SaveService.Save"/>
    /// and deserialized by <see cref="SaveService.Load"/>. Every field
    /// here maps directly to a subsystem inside <see cref="RuntimeData"/>.
    /// All reference-type properties are nullable so that older save files
    /// missing a field will deserialize without error.
    /// </summary>
    public class SaveData
    {
        /// <summary>In-game time in minutes (<see cref="TimeData.Tick"/>).</summary>
        public long Time { get; set; }
        /// <summary>Lamp light radius in screen pixels.</summary>
        public float LampRadius { get; set; }
        /// <summary>Lamp influence factor (0–1).</summary>
        public float LampInfluence { get; set; }
        /// <summary>All recruited character snapshots.</summary>
        public List<CharacterInfoData> Characters { get; set; }
        /// <summary>Live quest instances (concrete subclasses of <see cref="QuestBase"/>).</summary>
        public List<QuestBase> Quests { get; set; }
        /// <summary>All active recruiting entries.</summary>
        public List<Recruit> Recruits { get; set; }
        /// <summary>Set of character IDs currently in the battle party.</summary>
        public List<string> BattleSet { get; set; }
        /// <summary>Snapshot of the player inventory as flat <see cref="CItem"/> entries.</summary>
        public List<CItem> Inventory { get; set; }
        /// <summary>Set of unlocked recipe IDs.</summary>
        public List<string> Recipes { get; set; }
        /// <summary>Name of the active scene at the moment of save.</summary>
        public string CurrentSceneName { get; set; }
        /// <summary>Real-world UTC timestamp when the save was created.</summary>
        public DateTime SaveDateTime { get; set; }
        /// <summary>Duplicate of <see cref="Time"/> — in-game minutes at save moment
        /// (kept for convenience in the slot metadata UI).</summary>
        public long GameTime { get; set; }
        /// <summary>Number of recruited characters (denormalized for slot preview).</summary>
        public int CharacterCount { get; set; }
        /// <summary>IDs of action cards owned by the player in the persistent action pool.</summary>
        public List<string> ActionPool { get; set; }
        /// <summary>IDs of all one-shot scenarios that have been completed,
        /// used to suppress re-triggering them after a load.</summary>
        public HashSet<string> CompletedScenarioIds { get; set; } = new();
    }
}

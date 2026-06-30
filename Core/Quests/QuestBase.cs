using System;
using System.Collections.Generic;

namespace Cthangover.Core.Quests
{
    
    /// <summary>
    /// Primary concrete implementation of <see cref="IQuest"/> that serves as
    /// both the runtime quest instance (created by <see cref="QuestFactory"/>
    /// from <see cref="QuestDefinition"/> data) and the serialization unit
    /// written into save files. Because it is marked <c>[Serializable]</c>,
    /// Godot's JSON serialization can round-trip the entire quest graph
    /// — including the mutable <see cref="Data"/> payload — when the
    /// player saves or loads a game. The <see cref="StatusToDescription"/>
    /// dictionary maps raw <see cref="QuestStatus"/> integer values to
    /// localised description strings that the journal UI renders for each
    /// progression stage.
    /// </summary>
    [Serializable]
    public class QuestBase : IQuest
    {

        /// <summary>
        /// Unique string identifier matching the quest's definition key in
        /// the mod data. Used by <see cref="QuestFactory.Get"/> for lookup
        /// and preserved in save files so that loaded state can be reattached
        /// to the correct definition.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Localised display name shown in the quest journal and notification
        /// banners. This value originates from the mod definition file and is
        /// translated before display.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Current progression state of the quest. Defaults to
        /// <see cref="QuestStatus.NotStarted"/>. This property is updated at
        /// runtime by quest‑advancement logic and restored from save data
        /// by <see cref="QuestFactory.SetAll"/>.
        /// </summary>
        public QuestStatus Status { get; set; } = QuestStatus.NotStarted;

        /// <summary>
        /// Mutable runtime payload holding the quest's numeric status and
        /// the collection of string tags accumulated during gameplay. The
        /// factory overwrites this object when restoring a save file via
        /// <see cref="QuestFactory.SetAll"/>, so any modifications to tags
        /// or status through <see cref="AddTag"/>, <see cref="RemoveTag"/>,
        /// or <see cref="QuestData.SetStatus"/> persist until the next load.
        /// </summary>
        public QuestData Data { get; set; } = new QuestData();

        /// <summary>
        /// Lookup table that maps integer <see cref="QuestStatus"/> values to
        /// human‑readable description strings. The journal window uses this
        /// to select which narrative text to display underneath the quest
        /// title as the player advances through <c>NotStarted</c>,
        /// <c>Progress</c>, and <c>End</c> states.
        /// </summary>
        public IDictionary<int, string> StatusToDescription { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Returns <c>true</c> when <paramref name="tag"/> is present in the
        /// underlying <see cref="Data"/>.<see cref="QuestData.Tags"/> set.
        /// Tags act as boolean flags for sub‑objective completion; dialogue
        /// conditions and scene‑transition gates call this to decide whether
        /// a particular branch should be available.
        /// </summary>
        public bool ContainsTag(string tag)
        {
            return Data.Tags.Contains(tag);
        }

        /// <summary>
        /// Syntactic negation helper that returns <c>true</c> when
        /// <paramref name="tag"/> is absent from the quest's tag set.
        /// Exists so that scenario scripts can express "tag not yet earned"
        /// conditions as a direct method call without writing a negation
        /// operator in YAML condition strings.
        /// </summary>
        public bool NotContainsTag(string tag)
        {
            return !ContainsTag(tag);
        }

        /// <summary>
        /// Inserts <paramref name="tag"/> into the mutable
        /// <see cref="Data"/>.<see cref="QuestData.Tags"/> set, marking a
        /// sub‑objective or milestone as completed. Typically called from
        /// scenario action nodes or dialogue event handlers when the player
        /// reaches a logical checkpoint.
        /// </summary>
        public void AddTag(string tag)
        {
            Data.Tags.Add(tag);
        }

        /// <summary>
        /// Removes <paramref name="tag"/> from the mutable
        /// <see cref="Data"/>.<see cref="QuestData.Tags"/> set, effectively
        /// revoking a previously earned milestone. Useful for branching
        /// narratives where the player can lose access to a completed
        /// sub‑objective through a different choice path.
        /// </summary>
        public void RemoveTag(string tag)
        {
            Data.Tags.Remove(tag);
        }

    }
    
}

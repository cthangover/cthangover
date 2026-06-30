using System;
using System.Collections.Generic;

namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Serialisable payload that holds the mutable, per‑save‑file runtime
    /// state of a single quest. Separating this from the immutable
    /// <see cref="QuestDefinition"/> allows the save system to persist only
    /// the dirty data (current status and earned tags) without duplicating
    /// the static definition fields that are reloaded from mod JSON every
    /// session. An instance of this class lives inside
    /// <see cref="QuestBase.Data"/> and is replaced wholesale by
    /// <see cref="QuestFactory.SetAll"/> when the player loads a game.
    /// </summary>
    [Serializable]
    public class QuestData
    {
        /// <summary>
        /// Numeric representation of the current quest stage, castable to
        /// <see cref="QuestStatus"/>. The journal UI maps this to a
        /// description string via <see cref="QuestBase.StatusToDescription"/>.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Set of string tags earned during this playthrough. Each tag
        /// represents a branching milestone or sub‑objective; scenario
        /// scripts query membership via <see cref="IQuest.ContainsTag"/> to
        /// gate dialogue options and scene transitions.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Sets the numeric status to <paramref name="value"/>, advancing
        /// (or potentially resetting) the quest's progression stage. Called
        /// by scenario action nodes that handle quest advancement commands.
        /// </summary>
        public void SetStatus(int value)
        {
            Status = value;
        }
    }
}

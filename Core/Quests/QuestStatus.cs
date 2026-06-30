namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Discrete progression states for the quest lifecycle. Each value maps
    /// to a 1‑byte integer that doubles as an array index into
    /// <see cref="QuestBase.StatusToDescription"/> and as the key for
    /// journal‑window filtering. The three‑state model avoids the complexity
    /// of arbitrary stage counts while still supporting branch‑like
    /// progression via the tag system in <see cref="QuestData"/>.
    /// </summary>
    public enum QuestStatus : byte
    {
        /// <summary>
        /// The quest has never been accepted or activated. The journal
        /// window hides quests in this state unless the player has
        /// explicitly toggled "show all" mode.
        /// </summary>
        NotStarted = 0x00,

        /// <summary>
        /// The quest is in progress; the player has accepted the task and
        /// may have completed some sub‑objectives (tracked via tags) but
        /// has not yet reached the final stage.
        /// </summary>
        Progress   = 0x01,

        /// <summary>
        /// Terminal state indicating the quest has been fully completed.
        /// Quests in this state are usually moved to a "completed" section
        /// of the journal and are excluded from most progression‑gating
        /// condition checks.
        /// </summary>
        End        = 0x02,
    };
}

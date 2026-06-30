using System;
using Cthangover.Core.Quests;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Quest service implementation with internal TryGet for safe fallback.
    /// Public methods (SetStatus, SetDataStatus, AddTag, etc.) use TryGet which
    /// catches KeyNotFoundException from QuestFactory — this prevents scenario
    /// scripts from crashing when referencing non-existent quests. The separation
    /// of SetStatus (global quest state like Active/Completed) from SetDataStatus
    /// (numeric progress level) matches QuestBase's dual-state design. Exists()
    /// returns false on any exception, so missing or broken quest data is treated
    /// as "doesn't exist" rather than throwing.
    /// </summary>
    internal class QuestServiceImpl : IQuestService
    {
        /// <summary>
        /// Direct lookup from QuestFactory by ID. Throws
        /// KeyNotFoundException if the quest doesn't exist — use
        /// Exists() or TryGet() when the quest may legitimately be
        /// absent from the registry.
        /// </summary>
        public IQuest Get(string id) => QuestFactory.Instance.Get(id);

        /// <summary>
        /// Safe existence check. Wraps QuestFactory.Get in try/catch
        /// and returns false on any exception — corrupted save data or
        /// broken quest definitions are treated as "doesn't exist"
        /// rather than propagating errors to the dialog engine.
        /// </summary>
        public bool Exists(string id)
        {
            try { return QuestFactory.Instance.Get(id) != null; }
            catch { return false; }
        }

        /// <summary>
        /// Internal safe-get pattern that catches KeyNotFoundException
        /// from QuestFactory. Returns null for missing quests so
        /// callers can use the null-conditional operator (?.) rather
        /// than try/catch blocks. All mutating methods (SetStatus,
        /// AddTag, etc.) route through TryGet to prevent scenario
        /// scripts from crashing on broken quest references.
        /// </summary>
        public QuestBase TryGet(string id)
        {
            QuestBase result = null;
            try
            {
                result = QuestFactory.Instance.Get(id);
            }
            catch(Exception ex)
            {
                GameLogger.Log("QUEST", $"try get quest '{id}' exception - {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Sets the global quest status (Active, Completed, Failed,
        /// etc.) by parsing the status string via
        /// Enums&lt;QuestStatus&gt;.Parse. Invalid status strings are
        /// caught and logged as errors without throwing — the dialog
        /// continues with the quest unchanged. Routes through TryGet
        /// so missing quests are silently skipped.
        /// </summary>
        public void SetStatus(string id, string status)
        {
            var quest = TryGet(id);
            if(quest == null)
                return;
            try
            {
                quest.Status = Enums<QuestStatus>.Parse(status);
            }
            catch (Exception ex)
            {
                GameLogger.Log("QUEST", $"status '{status}' invalid - {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Updates the numeric progress level within the quest's data
        /// object (quest.Data.Status). Separate from SetStatus which
        /// controls the global lifecycle state. Routes through TryGet
        /// with null-conditional access — if the quest doesn't exist,
        /// the call is silently skipped. No bounds checking: the caller
        /// is responsible for meaningful level values.
        /// </summary>
        public void SetDataStatus(string id, int level)
        {
            TryGet(id)?.Data.SetStatus(level);
        }

        /// <summary>
        /// Attaches a string tag to the quest's tag collection via
        /// QuestBase.AddTag. Tags are persisted with save data and
        /// queried by scenario conditions (has_tag). Adding a duplicate
        /// tag is handled by the underlying collection. Routes through
        /// TryGet for safe fallback on missing quests.
        /// </summary>
        public void AddTag(string id, string tag)
        {
            TryGet(id)?.AddTag(tag);
        }

        /// <summary>
        /// Removes a tag from the quest. Used for clearing temporary
        /// conditions in branching scenarios. Removing a non-existent
        /// tag is a collection-level no-op. Routes through TryGet.
        /// </summary>
        public void RemoveTag(string id, string tag)
        {
            TryGet(id)?.RemoveTag(tag);
        }

        /// <summary>
        /// Dispatches a UI notification for the quest (e.g. "Quest
        /// Updated" popup) via QuestBase.SendNotification. Does not
        /// modify quest state — call SetStatus/SetDataStatus before
        /// this to ensure the notification reflects the new state.
        /// Routes through TryGet.
        /// </summary>
        public void SendNotification(string id)
        {
            TryGet(id)?.SendNotification();
        }
    }
}

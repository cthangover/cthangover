using Cthangover.Core.Quests;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Quest manipulation contract for scenario actions. Provides full CRUD over
    /// quest state: Get by ID, status change (parsed via Enums&lt;QuestStatus&gt;),
    /// data-level status tracking, tag management, and UI notification dispatch.
    /// Separates quest.Status (global state like Active/Completed) from
    /// quest.Data.Status (incremental progress within the quest).
    /// </summary>
    public interface IQuestService
    {
        /// <summary>
        /// Retrieves a quest by its string ID from QuestFactory. Throws
        /// KeyNotFoundException if the quest doesn't exist — prefer
        /// Exists() or the safe TryGet() pattern in the implementation
        /// when the quest may legitimately be absent.
        /// </summary>
        IQuest Get(string id);

        /// <summary>
        /// Checks whether a quest with the given ID is registered in
        /// QuestFactory. Returns false on any exception (missing data,
        /// corrupted save), treating "can't read" as "doesn't exist"
        /// to prevent scenario scripts from crashing.
        /// </summary>
        bool Exists(string id);

        /// <summary>
        /// Sets the global lifecycle state of a quest (Active, Completed,
        /// Failed, etc.). The status string is parsed via
        /// Enums&lt;QuestStatus&gt;.Parse — case-sensitive. Invalid status
        /// strings are caught and logged as errors without throwing.
        /// </summary>
        void SetStatus(string id, string status);

        /// <summary>
        /// Sets the numeric progress level within a quest's Data.Status
        /// field. This is incremental progress (e.g. "2/5 wolves killed"),
        /// separate from the global quest state managed by SetStatus().
        /// The quest's data object tracks the level for display and
        /// completion checks.
        /// </summary>
        void SetDataStatus(string id, int level);

        /// <summary>
        /// Attaches a string tag to a quest for use in conditional logic.
        /// Tags are stored in the quest's save data and queried by scenario
        /// scripts via the "has_tag" condition to branch dialog or gate
        /// quest progression. Adding a tag that already exists is a no-op.
        /// </summary>
        void AddTag(string id, string tag);

        /// <summary>
        /// Removes a string tag from a quest. Used to clear temporary
        /// conditions — for example, removing a "witnessed_event" tag
        /// after the event has been addressed. Removing a non-existent
        /// tag is a no-op.
        /// </summary>
        void RemoveTag(string id, string tag);

        /// <summary>
        /// Triggers a UI notification for the quest (e.g. "Quest Updated"
        /// or "New Quest" popup). This is the visual feedback companion
        /// to state changes — call after SetStatus/SetDataStatus for the
        /// player to see the update. The notification itself does not
        /// change quest state.
        /// </summary>
        void SendNotification(string id);
    }
}
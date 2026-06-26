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
        IQuest Get(string id);
        bool Exists(string id);
        void SetStatus(string id, string status);
        void SetDataStatus(string id, int level);
        void AddTag(string id, string tag);
        void RemoveTag(string id, string tag);
        void SendNotification(string id);
    }
}
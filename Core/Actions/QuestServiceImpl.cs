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
        public IQuest Get(string id) => QuestFactory.Instance.Get(id);
        public bool Exists(string id)
        {
            try { return QuestFactory.Instance.Get(id) != null; }
            catch { return false; }
        }

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

        public void SetDataStatus(string id, int level)
        {
            TryGet(id)?.Data.SetStatus(level);
        }

        public void AddTag(string id, string tag)
        {
            TryGet(id)?.AddTag(tag);
        }

        public void RemoveTag(string id, string tag)
        {
            TryGet(id)?.RemoveTag(tag);
        }

        public void SendNotification(string id)
        {
            TryGet(id)?.SendNotification();
        }
    }
}

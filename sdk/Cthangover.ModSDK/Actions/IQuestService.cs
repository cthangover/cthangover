using Cthangover.Core.Quests;

namespace Cthangover.Core.Actions
{
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
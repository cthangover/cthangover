using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

internal class MockQuestService : IQuestService
{
    public string LastSetStatusId { get; private set; }
    public string LastSetStatusStatus { get; private set; }
    public string LastAddedTagId { get; private set; }
    public string LastAddedTag { get; private set; }
    public string LastRemovedTagId { get; private set; }
    public string LastRemovedTag { get; private set; }
    public string LastNotificationId { get; private set; }
    public int LastDataStatusLevel { get; private set; }
    public string LastDataStatusId { get; private set; }
    public bool ExistsResult { get; set; } = true;

    public Cthangover.Core.Quests.IQuest Get(string id) => null;

    public bool Exists(string id) => ExistsResult;

    public void SetStatus(string id, string status)
    {
        LastSetStatusId = id;
        LastSetStatusStatus = status;
    }

    public void SetDataStatus(string id, int level)
    {
        LastDataStatusId = id;
        LastDataStatusLevel = level;
    }

    public void AddTag(string id, string tag)
    {
        LastAddedTagId = id;
        LastAddedTag = tag;
    }

    public void RemoveTag(string id, string tag)
    {
        LastRemovedTagId = id;
        LastRemovedTag = tag;
    }

    public void SendNotification(string id) => LastNotificationId = id;
}

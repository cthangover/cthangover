namespace Cthangover.Core.Quests
{
    public interface IQuest
    {
        string ID { get; }

        string Name { get; }

        QuestStatus Status { get; }

        bool ContainsTag(string tag);
    }
}

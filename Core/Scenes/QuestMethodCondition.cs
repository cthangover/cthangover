namespace Cthangover.Core.Scenes
{
    public class QuestMethodCondition : ConditionNode
    {
        public string QuestId { get; }
        public string Method { get; }
        public string Tag { get; }

        public QuestMethodCondition(string questId, string method, string tag)
        {
            QuestId = questId;
            Method = method;
            Tag = tag;
        }
    }
}

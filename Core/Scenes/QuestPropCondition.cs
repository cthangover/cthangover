namespace Cthangover.Core.Scenes
{
    public class QuestPropCondition : ConditionNode
    {
        public string QuestId { get; }
        public string Property { get; }
        public string Op { get; }
        public int Value { get; }

        public QuestPropCondition(string questId, string property, string op, int value)
        {
            QuestId = questId;
            Property = property;
            Op = op;
            Value = value;
        }
    }
}

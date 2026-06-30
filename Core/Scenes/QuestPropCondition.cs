namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node comparing a quest's integer property (<c>Status</c> or
    /// <c>DataStatus</c>) against a numeric value. Evaluated by
    /// <see cref="ScenarioCondition.EvaluateQuestProp"/> which fetches the quest
    /// instance via <see cref="QuestFactory"/> and reads the corresponding property.
    /// </summary>
    public class QuestPropCondition : ConditionNode
    {
        /// <summary>The quest identifier to query.</summary>
        public string QuestId { get; }

        /// <summary>The property name: <c>"Status"</c> or <c>"DataStatus"</c>.</summary>
        public string Property { get; }

        /// <summary>The comparison operator (<c>==</c>, <c>!=</c>, <c>&gt;=</c>, etc.).</summary>
        public string Op { get; }

        /// <summary>The integer value to compare the quest property against.</summary>
        public int Value { get; }

        /// <summary>
        /// Creates a quest property comparison condition node.
        /// </summary>
        /// <param name="questId">The quest identifier.</param>
        /// <param name="property">The property name (<c>"Status"</c> or <c>"DataStatus"</c>).</param>
        /// <param name="op">The comparison operator.</param>
        /// <param name="value">The integer value to compare against.</param>
        public QuestPropCondition(string questId, string property, string op, int value)
        {
            QuestId = questId;
            Property = property;
            Op = op;
            Value = value;
        }
    }
}

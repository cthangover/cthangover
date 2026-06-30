namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node invoking a boolean method on a quest instance: either
    /// <c>hasTag</c> or <c>notHasTag</c>. Evaluated by
    /// <see cref="ScenarioCondition.EvaluateQuestMethod"/> which fetches the quest
    /// via <see cref="QuestFactory"/> and calls the corresponding method with the
    /// given tag string.
    /// </summary>
    public class QuestMethodCondition : ConditionNode
    {
        /// <summary>The quest identifier to query.</summary>
        public string QuestId { get; }

        /// <summary>The method name: <c>"hasTag"</c> or <c>"notHasTag"</c>.</summary>
        public string Method { get; }

        /// <summary>The tag string passed as the method argument.</summary>
        public string Tag { get; }

        /// <summary>
        /// Creates a quest method invocation condition node.
        /// </summary>
        /// <param name="questId">The quest identifier.</param>
        /// <param name="method">The method name (<c>"hasTag"</c> or <c>"notHasTag"</c>).</param>
        /// <param name="tag">The tag argument for the method call.</param>
        public QuestMethodCondition(string questId, string method, string tag)
        {
            QuestId = questId;
            Method = method;
            Tag = tag;
        }
    }
}

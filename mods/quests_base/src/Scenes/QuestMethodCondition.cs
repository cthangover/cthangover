using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node invoking a boolean method on a quest instance: either
    /// <c>hasTag</c> or <c>notHasTag</c>. Fetches the quest via
    /// <see cref="EvaluationContext.QuestFactory"/> at evaluation time and calls
    /// the corresponding method with the given tag string. Returns <c>false</c>
    /// (with a log warning) when the quest ID is not found in the factory.
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

        /// <summary>Resolves the quest and invokes the tag check; unknown quest IDs evaluate to <c>false</c>.</summary>
        public override bool Evaluate(EvaluationContext ctx)
        {
            try
            {
                var quest = ctx.QuestFactory.Get(QuestId);
                return Method == "hasTag"
                    ? quest.ContainsTag(Tag)
                    : !quest.ContainsTag(Tag);
            }
            catch (KeyNotFoundException)
            {
                GameLogger.Log("COND", $"quest '{QuestId}' not found, condition → false", LogLevel.Error);
                return false;
            }
        }
    }
}
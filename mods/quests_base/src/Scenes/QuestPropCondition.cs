using System.Collections.Generic;
using Cthangover.Core.Quests;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node comparing a quest's integer property (<c>Status</c> or
    /// <c>DataStatus</c>) against a numeric value. Fetches the quest via
    /// <see cref="EvaluationContext.QuestFactory"/> at evaluation time, reads the
    /// corresponding property, and compares using <see cref="ConditionCompare.Compare"/>.
    /// Returns <c>false</c> (with a log warning) when the quest ID is not found.
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

        /// <summary>Resolves the quest and compares the numeric property; unknown quest IDs evaluate to <c>false</c>.</summary>
        public override bool Evaluate(EvaluationContext ctx)
        {
            try
            {
                var quest = ctx.QuestFactory.Get(QuestId);
                var actual = Property == "Status"
                    ? (int)quest.Status
                    : ((QuestBase)quest).Data.Status;
                return ConditionCompare.Compare(Op, actual, Value);
            }
            catch (KeyNotFoundException)
            {
                GameLogger.Log("COND", $"quest '{QuestId}' not found, condition → false", LogLevel.Error);
                return false;
            }
        }
    }
}
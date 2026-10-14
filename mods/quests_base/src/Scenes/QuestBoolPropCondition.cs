using System.Collections.Generic;
using Cthangover.Core.Quests;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node that checks a boolean property on a quest instance.
    /// Currently the only supported property is <c>IsActive</c>, which returns
    /// <c>true</c> when the quest status equals <see cref="QuestStatus.Progress"/>.
    /// Fetches the quest via <see cref="EvaluationContext.QuestFactory"/> and
    /// returns <c>false</c> (with a log warning) when the quest ID is not found.
    /// </summary>
    public class QuestBoolPropCondition : ConditionNode
    {
        /// <summary>The quest identifier to query.</summary>
        public string QuestId { get; }

        /// <summary>The property name: currently only <c>"IsActive"</c>.</summary>
        public string Property { get; }

        /// <summary>Creates a boolean quest-property condition node.</summary>
        /// <param name="questId">The quest identifier.</param>
        /// <param name="property">The property name (currently only <c>"IsActive"</c>).</param>
        public QuestBoolPropCondition(string questId, string property)
        {
            QuestId = questId;
            Property = property;
        }

        /// <summary>Resolves the quest and reads the boolean property; unknown quest IDs evaluate to <c>false</c>.</summary>
        public override bool Evaluate(EvaluationContext ctx)
        {
            try
            {
                var quest = ctx.QuestFactory.Get(QuestId);
                if (Property == "IsActive")
                    return quest.Status == QuestStatus.Progress;
                return false;
            }
            catch (KeyNotFoundException)
            {
                GameLogger.Log("COND", $"quest '{QuestId}' not found, condition → false", LogLevel.Error);
                return false;
            }
        }
    }
}
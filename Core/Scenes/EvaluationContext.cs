using System.Collections.Generic;
using Cthangover.Core.Actions;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Carries the runtime state that condition nodes need during evaluation.
    /// A single instance is reused across the entire AST walk — created once
    /// in <see cref="ScenarioCondition.Evaluate"/> and passed through every
    /// <see cref="ConditionNode.Evaluate"/> call.
    ///
    /// <c>Flags</c> is a reference to the static flag dictionary managed by
    /// <see cref="ScenarioCondition.SetFlag"/>, allowing <see cref="FlagCondition"/>
    /// to resolve flag values without static access.
    ///
    /// <c>QuestFactory</c> is a reference to the quest service published by
    /// the quests engine mod through <see cref="Cthangover.Core.Quests.QuestServiceRegistry"/>,
    /// injected so that quest-based leaf nodes shipped with the mod can
    /// resolve quests without a hard reference to the mod assembly.
    ///
    /// The context is deliberately a plain POCO — not a Godot object — so that
    /// it can be instantiated in unit tests without the Godot runtime.
    /// </summary>
    public class EvaluationContext
    {
        /// <summary>Reference to the static flag dictionary from <see cref="ScenarioCondition"/>.</summary>
        public Dictionary<string, string> Flags;

        /// <summary>Quest service of the quests engine mod (core contract <see cref="IQuestService"/>) for quest resolution.</summary>
        public IQuestService QuestFactory;
    }
}

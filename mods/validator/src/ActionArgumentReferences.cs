using System.Collections.Generic;

namespace Cthangover.Validator
{
    /// <summary>
    /// Declarative map "scenario action → named argument → <see cref="ValidationKind"/>"
    /// for actions whose arguments reference declared content. The runtime
    /// <c>IScenarioAction</c> contract carries no parameter metadata, so the
    /// validator keeps the knowledge here instead: each entry names one action
    /// argument and the entity kind it must resolve to. Scenario validation adds
    /// a graph reference per literal value (see
    /// <see cref="ScenarioCommandValidationStrategy"/>), and
    /// <see cref="ReferenceStrategy"/> reports a missing declaration with the
    /// exact scenario file and line — the static answer to the typo class the
    /// runtime factories can only log.
    /// <para/>
    /// The table intentionally covers only actions with literal content ids
    /// today (character.add_to_party / character.send_notification /
    /// character.add_skill / battle.init). Extend it when a new action starts
    /// accepting content references; the mechanics need no code changes.
    /// Comma-separated lists (battle.init enemies=) and runtime-computed values
    /// (the ${name} marker) are supported by the caller, not here.
    /// </summary>
    public static class ActionArgumentReferences
    {
        private static readonly Dictionary<string, string> CharacterType = new Dictionary<string, string>
        {
            ["type"] = ValidationKind.Character,
        };

        private static readonly Dictionary<string, string> BattleInit = new Dictionary<string, string>
        {
            ["scene"] = ValidationKind.Scene,
            ["enemies"] = ValidationKind.Character,
            ["quest_id"] = ValidationKind.Quest,
        };

        private static readonly Dictionary<string, string> AddSkill = new Dictionary<string, string>
        {
            ["id"] = ValidationKind.Skill,
        };

        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Table =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["character.add_to_party"] = CharacterType,
            ["character.send_notification"] = CharacterType,
            ["character.add_skill"] = AddSkill,
            ["battle.init"] = BattleInit,
        };

        /// <summary>
        /// Returns the argument→kind map for an action, or false when the action
        /// carries no known content references. Lookup is case-insensitive to
        /// match the reference check for the action name itself.
        /// </summary>
        public static bool TryGetKinds(string actionName, out IReadOnlyDictionary<string, string> kinds)
        {
            return Table.TryGetValue(actionName, out kinds);
        }
    }
}

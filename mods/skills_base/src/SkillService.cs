using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Single entry point for acquiring a skill at runtime. A skill is more
    /// than a checkbox in <see cref="SkillData"/>: an active skill can hand
    /// the player an action card through <see cref="SkillInfo.GrantsAction"/>,
    /// and that card is what the player later assigns to a character in the
    /// character panel. Granting the skill and its card together keeps the two
    /// collections from drifting apart — a skill without its card (or a card
    /// without its skill) would survive save/restore inconsistently and the
    /// skills panel could not explain where the card came from.
    /// <para/>
    /// Granting is idempotent: once <see cref="SkillData"/> owns the skill ID,
    /// repeated calls are no-ops, so replayed scenario events cannot duplicate
    /// action cards in the pool.
    /// </summary>
    public static class SkillService
    {
        /// <summary>
        /// Resolves the skill definition through <see cref="SkillFactory"/> and
        /// grants it to the current runtime. This is the path used by scenario
        /// actions and mod initializers. Unknown IDs are logged and rejected
        /// instead of partially mutating the runtime state.
        /// </summary>
        /// <param name="id">Skill ID as authored in the skill's JSON file (e.g. "slave_master").</param>
        /// <returns><c>true</c> when the skill was newly granted; <c>false</c> when it was already owned or could not be resolved.</returns>
        public static bool GrantSkill(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            var skill = SkillFactory.Instance.Get(id);
            if (skill == null)
            {
                GameLogger.Log("SKILL", $"GrantSkill: unknown skill '{id}'", LogLevel.Warning);
                return false;
            }

            return GrantSkill(GameData.Instance.Runtime, skill);
        }

        /// <summary>
        /// Core grant routine operating on an explicit runtime, so the
        /// skill/card pairing can be exercised without the Godot autoload.
        /// Adds the skill to <see cref="RuntimeData.SkillData"/> and, when the
        /// definition names one, appends its granted action to
        /// <see cref="RuntimeData.ActionPool"/> where the character panel can
        /// pick it up.
        /// </summary>
        /// <param name="runtime">Session state that owns both the skill set and the action pool.</param>
        /// <param name="skill">Resolved skill definition to grant.</param>
        /// <returns><c>true</c> when the skill was newly granted; <c>false</c> when it was already owned or the arguments are invalid.</returns>
        public static bool GrantSkill(RuntimeData runtime, SkillInfo skill)
        {
            if (runtime == null || skill == null || string.IsNullOrEmpty(skill.ID))
                return false;

            if (runtime.SkillData.HasSkill(skill.ID))
                return false;

            runtime.SkillData.AddSkill(skill.ID);

            if (!string.IsNullOrEmpty(skill.GrantsAction))
                runtime.ActionPool.ActionIds.Add(skill.GrantsAction);

            GameLogger.Log("SKILL", string.IsNullOrEmpty(skill.GrantsAction)
                ? $"GrantSkill: granted '{skill.ID}'"
                : $"GrantSkill: granted '{skill.ID}', action '{skill.GrantsAction}' added to pool");

            return true;
        }
    }
}

using Cthangover.Core.Characters;
using Cthangover.Core.Relationship;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Recruitment condition that gates the "enslave" recruit option
    /// behind the <c>slave_master</c> skill. When the player attempts to
    /// recruit an NPC, the recruitment system queries all registered
    /// <see cref="IRecruitCondition"/> implementations; if this condition
    /// is present and the player does not have the skill, the enslavement
    /// option is hidden or disabled.
    /// </summary>
    public class EnslaveCondition : IRecruitCondition
    {
        /// <summary>
        /// Identifies this condition as the enslavement gate for the
        /// recruitment system (maps to <c>enslave</c> option).
        /// </summary>
        public string Id => "enslave";

        /// <summary>
        /// Determines whether enslavement is available for the given enemy.
        /// Checks the runtime skill data for the <c>slave_master</c> skill.
        /// The target <paramref name="enemy"/> is provided but not inspected
        /// because the condition depends solely on the player's skills.
        /// </summary>
        public bool CanRecruit(Character enemy, RuntimeData runtime)
        {
            return runtime.SkillData.HasSkill("slave_master");
        }
    }
}

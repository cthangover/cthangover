namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Grants a skill to the player by skill ID. Delegates to
    /// <see cref="ICharacterService.AddSkill"/>, which routes through
    /// <see cref="Skills.SkillService"/>: the skill is added to the owned set
    /// and any action card named by its GrantsAction field lands in the action
    /// pool, ready to be assigned to a character. Already owned skills are
    /// ignored, making the action safe to replay after loading a save.
    /// </summary>
    public class CharacterAddSkillAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "character.add_skill" — grants the skill named by the
        /// "skill_id" dialog variable. The variable matches the skill's Id in
        /// its JSON definition (e.g. "slave_master"). Unknown IDs are rejected
        /// downstream with a log warning.
        /// </summary>
        public string Name => "character.add_skill";

        /// <summary>
        /// Reads "skill_id" and grants the skill. Returns early with a log
        /// message when the variable is missing or empty, so a malformed
        /// scenario line cannot half-apply the acquisition.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var skillId = ctx.GetParam("skill_id");

            if (string.IsNullOrEmpty(skillId))
            {
                ctx.Log("EVENT", "CharacterAddSkillAction: missing 'skill_id' variable");
                return;
            }

            ctx.Character.AddSkill(skillId);
            ctx.Log("EVENT", $"CharacterAddSkillAction: granted skill '{skillId}'");
        }
    }
}

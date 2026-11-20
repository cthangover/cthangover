using Cthangover.Core.Actions.Atomic;

namespace Engine.Tests.Actions
{
    public class CharacterAddSkillActionTests
    {
        [Fact]
        public void Run_DelegatesSkillIdToService()
        {
            var ctx = new MockActionContext();
            var characterService = new MockCharacterService();
            ctx.Character = characterService;
            ctx.SetParam("skill_id", "slave_master");

            new CharacterAddSkillAction().Run(ctx);

            Assert.Equal("slave_master", characterService.LastSkillId);
        }

        [Fact]
        public void Run_MissingSkillId_DoesNotCallService()
        {
            var ctx = new MockActionContext();
            var characterService = new MockCharacterService();
            ctx.Character = characterService;

            new CharacterAddSkillAction().Run(ctx);

            // AddSkill(null) would leave LastSkillId null as well, so only the
            // call counter distinguishes "guard returned early" from a skipped
            // guard that granted a null skill.
            Assert.Equal(0, characterService.AddSkillCalls);
            Assert.Null(characterService.LastSkillId);
            Assert.Contains(ctx.Logs, entry => entry.Category == "EVENT");
        }
    }
}

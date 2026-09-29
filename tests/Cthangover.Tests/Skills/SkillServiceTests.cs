using Cthangover.Core.Settings;
using Cthangover.Core.Skills;

namespace Engine.Tests.Skills
{
    public class SkillServiceTests
    {
        [Fact]
        public void GrantSkill_AddsSkillAndItsGrantedAction()
        {
            var runtime = new RuntimeData();
            var skill = new SkillInfo { ID = "battle_instinct", GrantsAction = "physics/attack" };

            var granted = SkillService.GrantSkill(runtime, skill);

            Assert.True(granted);
            Assert.True(runtime.SkillData.HasSkill("battle_instinct"));
            Assert.Contains("physics/attack", runtime.ActionPool.ActionIds);
        }

        [Fact]
        public void GrantSkill_PassiveWithoutGrantedAction_LeavesPoolUntouched()
        {
            var runtime = new RuntimeData();
            var skill = new SkillInfo { ID = "slave_master" };

            var granted = SkillService.GrantSkill(runtime, skill);

            Assert.True(granted);
            Assert.True(runtime.SkillData.HasSkill("slave_master"));
            Assert.Empty(runtime.ActionPool.ActionIds);
        }

        [Fact]
        public void GrantSkill_AlreadyOwned_DoesNotDuplicateGrantedAction()
        {
            var runtime = new RuntimeData();
            var skill = new SkillInfo { ID = "battle_instinct", GrantsAction = "physics/attack" };
            SkillService.GrantSkill(runtime, skill);

            var grantedAgain = SkillService.GrantSkill(runtime, skill);

            Assert.False(grantedAgain);
            Assert.Single(runtime.ActionPool.ActionIds);
        }

        [Fact]
        public void GrantSkill_SkillWithoutId_ReturnsFalse()
        {
            var runtime = new RuntimeData();
            var skill = new SkillInfo { GrantsAction = "physics/attack" };

            var granted = SkillService.GrantSkill(runtime, skill);

            Assert.False(granted);
            Assert.Empty(runtime.ActionPool.ActionIds);
        }

        [Fact]
        public void GrantSkill_NullArguments_ReturnFalse()
        {
            var runtime = new RuntimeData();

            Assert.False(SkillService.GrantSkill(runtime, null));
            Assert.False(SkillService.GrantSkill(null, new SkillInfo { ID = "battle_instinct" }));
        }
    }
}

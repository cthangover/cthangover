using Cthangover.Core.Characters;
using Cthangover.Core.Relationship;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    public class EnslaveCondition : IRecruitCondition
    {
        public string Id => "enslave";

        public bool CanRecruit(Character enemy, RuntimeData runtime)
        {
            return runtime.SkillData.HasSkill("slave_master");
        }
    }
}

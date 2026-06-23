using System.Collections.Generic;

namespace Cthangover.Core.Skills
{
    public class SkillData
    {
        public ISet<string> Skills { get; private set; } = new HashSet<string>();

        public SkillData()
        { }

        public bool HasSkill(SkillName skill)
        {
            return HasSkill(skill.ID());
        }
        
        public bool HasSkill(string id)
        {
            return Skills.Contains(id);
        }

        public void AddSkill(SkillName skill)
        {
            AddSkill(skill.ID());
        }
        
        public void AddSkill(string id)
        {
            Skills.Add(id);
        }

    }
}

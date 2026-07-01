using System;
using Cthangover.Core.Skills;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>FileFactory</c> for skill definitions. The nested group path
    /// <c>"characters/skills"</c> places skill data inside the characters
    /// resource tree, reflecting the design assumption that skills are
    /// owned by characters — a skill cannot exist independently of a
    /// character who can learn it.
    /// </summary>
    public class SkillFactory : FileFactory<SkillInfo>
    {
        private static readonly Lazy<SkillFactory> instance = new(() => new SkillFactory());
        public static SkillFactory Instance => instance.Value;

        public override string GroupName => "characters";
    }
}

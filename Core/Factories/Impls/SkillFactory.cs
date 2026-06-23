using System;
using Cthangover.Core.Skills;

namespace Cthangover.Core.Factories.Impls
{
    public class SkillFactory : FileFactory<SkillInfo>
    {
        private static readonly Lazy<SkillFactory> instance = new(() => new SkillFactory());
        public static SkillFactory Instance => instance.Value;

        public override string GroupName => "characters/skills";
    }
}

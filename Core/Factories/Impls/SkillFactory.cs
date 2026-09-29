using System;
using Cthangover.Core.Skills;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>FileFactory</c> for skill definitions. Skills are collected by
    /// the shared <c>EntityResolver</c> through the DTO's
    /// <c>[EntityKind("skill")]</c> attribute, so this factory carries no
    /// directory knowledge at all — a skill file is found wherever a mod
    /// decides to put it, as long as its envelope declares the skill kind.
    /// </summary>
    public class SkillFactory : FileFactory<SkillInfo>
    {
        private static readonly Lazy<SkillFactory> instance = new(() => new SkillFactory());
        public static SkillFactory Instance => instance.Value;
    }
}

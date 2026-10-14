using System;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Thin <c>FileFactory</c> for skill definitions. Skills are collected by
    /// the shared <c>EntityResolver</c> through the DTO's
    /// <c>[EntityKind("skill")]</c> attribute, so this factory carries no
    /// directory knowledge at all — a skill file is found wherever a mod
    /// decides to put it, as long as its envelope declares the skill kind.
    /// <para/>
    /// The single instance is a self-contained lazy singleton owned by this
    /// mod assembly; consumers resolve it through <see cref="Instance"/>.
    /// </summary>
    public class SkillFactory : FileFactory<SkillInfo>
    {
        private static readonly Lazy<SkillFactory> instance = new(() => new SkillFactory());
        public static SkillFactory Instance => instance.Value;
    }
}

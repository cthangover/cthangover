using Cthangover.Core.Actions;
using Cthangover.Core.Mods;

namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Binds the skills subsystem into the kernel bridge. Kernel entry points
    /// (scenario actions, the character service) route grants through
    /// <see cref="SkillsBridge"/>; the actual grant logic — resolving the
    /// definition and pairing the skill with its granted action card — is
    /// owned by this mod's <see cref="SkillService"/>. The binding happens on
    /// <c>OnModResourcesReady</c>, when skill JSON data is guaranteed to be
    /// collectable through the factory.
    /// </summary>
    public class SkillsModInitializer : IModInitializer
    {
        /// <summary>No per-mod work: registration is idempotent and resources-ready only.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Installs the grant sink (first registration wins).</summary>
        public void OnModResourcesReady()
        {
            SkillsBridge.Register(SkillService.GrantSkill);
        }
    }
}

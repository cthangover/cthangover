using Cthangover.Core.Mods;

namespace Cthangover.Mods.Effects
{
    /// <summary>
    /// Engine-mod initializer for the effects subsystem. The factory is a
    /// self-contained lazy singleton resolved by consumers through
    /// <c>EffectFactory.Instance</c>, so there is no core binding to
    /// re-establish on hot reload — the initializer exists to register the
    /// assembly with the mod pipeline and keep the migration shape uniform
    /// with the other engine mods.
    /// </summary>
    public class EffectsModInitializer : IModInitializer
    {
        /// <summary>No per-mod setup is required.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>Nothing to bind: the factory lazy self-initializes on first use.</summary>
        public void OnModResourcesReady() { }
    }
}

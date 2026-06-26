namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Lifecycle hook for mod code that needs to run logic when a mod
    /// finishes loading. Implementations are auto-discovered via
    /// reflection in <c>ModInitializerRegistry</c>.
    ///
    /// <c>OnModLoaded</c> is called for <b>every</b> mod, not just the
    /// mod that owns the initializer — this cross-mod notification
    /// pattern allows a single initializer to react to any mod being
    /// added, e.g. a compatibility patch that detects a specific
    /// companion mod and registers bridging behaviour.
    /// </summary>
    public interface IModInitializer
    {
        /// <summary>
        /// Called for <b>every</b> mod that loads — the <c>modId</c>
        /// parameter is the mod that just finished loading, which may
        /// or may not be the mod that owns this initializer.
        /// </summary>
        void OnModLoaded(string modId);
    }
}

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Lifecycle hook for mod code that needs to run logic during mod
    /// loading. Implementations are auto-discovered via reflection in
    /// <c>ModInitializerRegistry</c>.
    ///
    /// <c>OnModLoaded</c> is called for <b>every</b> mod, not just the
    /// mod that owns the initializer — this cross-mod notification
    /// pattern allows a single initializer to react to any mod being
    /// added, e.g. a compatibility patch that detects a specific
    /// companion mod and registers bridging behaviour.
    ///
    /// <c>OnModResourcesReady</c> fires once, after all mods have been
    /// fully loaded and their JSON resources are accessible through
    /// factories (CharacterFactory, ItemFactory, etc.). Use this for
    /// initialisation that depends on mod data, such as adding default
    /// starter characters to the party.
    /// </summary>
    public interface IModInitializer
    {
        /// <summary>
        /// Called for <b>every</b> mod that loads — the <c>modId</c>
        /// parameter is the mod that just finished loading, which may
        /// or may not be the mod that owns this initializer.
        /// </summary>
        void OnModLoaded(string modId);

        /// <summary>
        /// Called once after all mods have been loaded and their
        /// resources (JSON data) are accessible through factories.
        /// Use this for initialisation that requires game resources,
        /// e.g. adding default characters to the party roster.
        /// </summary>
        void OnModResourcesReady();
    }
}

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Kernel access point for battle action template resolution, decoupling
    /// the core from the engine mod that owns the factory
    /// (<c>mods/characters_base</c>). The mod's initializer assigns
    /// <see cref="Provider"/> during <c>IModInitializer.OnModLoaded</c>; the
    /// skills panel resolves granted action cards through <see cref="Get"/>.
    /// Returns <c>null</c> when the mod has not registered yet.
    /// </summary>
    public static class ActionTemplateRegistry
    {
        /// <summary>Provider published by the characters engine mod; <c>null</c> until the mod loads.</summary>
        public static IActionTemplateProvider Provider { get; set; }

        /// <summary>Returns an independent copy of the action card template, or <c>null</c> when no provider is registered or the ID is unknown.</summary>
        public static ActionCharacter Get(string id) => Provider?.Get(id);
    }
}

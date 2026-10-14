namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Kernel access point for character template resolution, decoupling the
    /// core from the engine mod that owns the factory
    /// (<c>mods/characters_base</c>). The mod's initializer assigns
    /// <see cref="Provider"/> during <c>IModInitializer.OnModLoaded</c>; all
    /// core-side consumers (<see cref="CharacterData"/>, inventory and skills
    /// UI) resolve templates through <see cref="Get"/>. Before the mod
    /// registers — tests, tools, very early boot — <see cref="Get"/> returns
    /// <c>null</c>, which <see cref="CharacterData.Create"/> already treats as
    /// the unknown-ID fallback path.
    /// </summary>
    public static class CharacterTemplateRegistry
    {
        /// <summary>Provider published by the characters engine mod; <c>null</c> until the mod loads.</summary>
        public static ICharacterTemplateProvider Provider { get; set; }

        /// <summary>Returns an independent copy of the character template, or <c>null</c> when no provider is registered or the ID is unknown.</summary>
        public static Character Get(string id) => Provider?.Get(id);
    }
}

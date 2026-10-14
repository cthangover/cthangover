namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Kernel contract for resolving battle action card templates by ID. The
    /// concrete factory (<c>ActionCharacterFactory</c>) lives in the engine
    /// mod <c>mods/characters_base</c> and publishes itself here from its
    /// <c>IModInitializer.OnModLoaded</c>. Every lookup returns an independent
    /// copy because action cards carry mutable battle state (cooldowns,
    /// modifiers) and must never be shared across character instances.
    /// </summary>
    public interface IActionTemplateProvider
    {
        /// <summary>Returns an independent copy of the action card template, or <c>null</c> when the ID is unknown.</summary>
        ActionCharacter Get(string id);
    }
}

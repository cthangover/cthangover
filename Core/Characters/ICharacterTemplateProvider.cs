namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Kernel contract for resolving character templates by ID. The concrete
    /// factory (<c>CharacterFactory</c>) lives in the engine mod
    /// <c>mods/characters_base</c> and publishes itself here from its
    /// <c>IModInitializer.OnModLoaded</c>, so the core assembly never
    /// references a runtime-compiled mod assembly. Copy semantics are part of
    /// the contract: every successful lookup returns an independent template
    /// copy, because <see cref="Character.Attributes"/> mutate during gameplay.
    /// </summary>
    public interface ICharacterTemplateProvider
    {
        /// <summary>Returns an independent copy of the character template, or <c>null</c> when the ID is unknown.</summary>
        Character Get(string id);
    }
}

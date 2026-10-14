using Godot;

namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Kernel contract for resolving character avatar textures by ID. The
    /// concrete factory (<c>AvatarFactory</c>) lives in the engine mod
    /// <c>mods/characters_base</c> and publishes itself through
    /// <see cref="AvatarProviderRegistry"/> from its
    /// <c>IModInitializer.OnModLoaded</c>, so dialog and UI code in the core
    /// stays independent of the mod assembly.
    /// </summary>
    public interface IAvatarProvider
    {
        /// <summary>Returns the avatar texture for the given ID, or <c>null</c> when the ID is unknown.</summary>
        Texture2D Get(string id);
    }
}

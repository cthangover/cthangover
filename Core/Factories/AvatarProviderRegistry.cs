namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Kernel access point for avatar texture resolution, decoupling the core
    /// from the engine mod that owns the avatar factory
    /// (<c>mods/characters_base</c>). The mod's initializer assigns
    /// <see cref="Provider"/> during <c>IModInitializer.OnModLoaded</c>;
    /// dialog actions resolve portraits through <see cref="Get"/>. Returns
    /// <c>null</c> when the mod has not registered yet.
    /// </summary>
    public static class AvatarProviderRegistry
    {
        /// <summary>Provider published by the characters engine mod; <c>null</c> until the mod loads.</summary>
        public static IAvatarProvider Provider { get; set; }

        /// <summary>Returns the avatar texture, or <c>null</c> when no provider is registered or the ID is unknown.</summary>
        public static Godot.Texture2D Get(string id) => Provider?.Get(id);
    }
}

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Optional unload-aware extension of <see cref="IModInitializer"/>. Kept
    /// as a separate interface (rather than default methods on the main one)
    /// because Godot's source generators and the existing mod initializers
    /// must not be forced through an interface change: existing mods compile
    /// unchanged, unload-aware mods opt in by implementing this too.
    ///
    /// The unload pair completes the lifecycle for collectible assembly
    /// unload: <c>OnModUnloading</c> runs on the initializer of the mod being
    /// unloaded while its types are still alive (drop event bus subscriptions,
    /// close file handles, detach gameplay listeners);
    /// <c>OnModUnloaded</c> runs on everyone else so cross-mod references to
    /// the dying mod's types can be dropped.
    /// </summary>
    public interface IModUnloadAware
    {
        /// <summary>
        /// Called on the initializer of the mod being unloaded, while its own
        /// types are still alive and callable. Release everything that could
        /// pin the assembly: event bus subscriptions, saved delegates,
        /// listeners in other subsystems.
        /// </summary>
        void OnModUnloading();

        /// <summary>
        /// Called on every <b>other</b> initializer when a mod is being
        /// unloaded — the reverse of the cross-mod <c>OnModLoaded</c>
        /// notification. Drop references to the dying mod's types now.
        /// </summary>
        void OnModUnloaded(string modId);
    }
}

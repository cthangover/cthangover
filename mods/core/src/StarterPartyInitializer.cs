using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Populates the initial player party with the default starter
    /// character <c>"Marao"</c> when the core mod finishes loading.
    ///
    /// The party can only be populated during <c>OnModResourcesReady</c>,
    /// not earlier. <c>OnModLoaded</c> and the constructor fire inside
    /// <c>ModRegistry.Initialize</c> → <c>ModCompiler.LoadModCode</c>,
    /// where mod JSON resources are not yet guaranteed to be accessible.
    /// <c>AddCharacterToParty</c> chains into <c>CharacterFactory.Get</c>,
    /// which calls <c>ModManager.CollectJsonGroup</c> — this requires
    /// file providers to be fully set up, which only happens after the
    /// entire <c>ModRegistry.Initialize</c> pipeline completes.
    ///
    /// <c>OnModResourcesReady</c> is invoked by <c>ModInitializerRegistry
    /// .NotifyResourcesReady</c> at the end of <c>SceneManager.Initialize</c>,
    /// after <c>ModRegistry</c> finishes discovery, <c>SceneEventRegistry</c>
    /// is primed, and scene definitions are collected. At that point all
    /// factories can safely read mod JSON data.
    ///
    /// Mods that introduce additional starter characters should implement
    /// their own <c>IModInitializer</c> and call <c>AddCharacterToParty</c>
    /// from <c>OnModResourcesReady</c> for their character IDs.
    /// </summary>
    public class StarterPartyInitializer : IModInitializer
    {
        /// <summary>
        /// No-op: cross-mod lifecycle notifications are not needed for
        /// starter party setup. Each mod's <c>OnModResourcesReady</c>
        /// handles its own default characters independently.
        /// </summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Adds the core mod's default starter character <c>"Marao"</c>
        /// to both <c>Characters</c> and <c>BattleSet</c> via
        /// <c>CharacterData.AddCharacterToParty</c>. Called once, after
        /// all mod file providers and JSON resources are guaranteed to
        /// be accessible.
        /// </summary>
        public void OnModResourcesReady()
        {
            GameData.Instance.Runtime.CharacterData.AddCharacterToParty("Marao");
        }
    }
}

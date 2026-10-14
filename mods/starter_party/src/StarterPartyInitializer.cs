using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Mods.Skills;

namespace Cthangover.Mods.StarterParty
{
    /// <summary>
    /// Populates the initial player party with the default starter
    /// character <c>"Marao"</c> when the starter_party mod finishes loading.
    ///
    /// The party can only be populated during <c>OnModResourcesReady</c>,
    /// not earlier. <c>OnModLoaded</c> and the constructor fire inside
    /// <c>ModRegistry.Initialize</c> → <c>ModCompiler.LoadModCode</c>,
    /// where mod JSON resources are not yet guaranteed to be accessible.
    /// <c>AddCharacterToParty</c> chains into <c>CharacterFactory.Get</c>,
    /// which triggers the shared entity resolver's first scan — this requires
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
        private static bool newGameHookInstalled;

        /// <summary>
        /// No-op: cross-mod lifecycle notifications are not needed for
        /// starter party setup. Each mod's <c>OnModResourcesReady</c>
        /// handles its own default characters independently.
        /// </summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Adds the starter_party mod's default starter character <c>"Marao"</c>
        /// to both <c>Characters</c> and <c>BattleSet</c> via
        /// <c>CharacterData.AddCharacterToParty</c>. Called once, after
        /// all mod file providers and JSON resources are guaranteed to
        /// be accessible. Also installs the one-time
        /// <see cref="NewGameStartedEvent"/> hook so the starter party is
        /// restored after <c>ResetRuntime</c> wipes it at the start of a
        /// new game. Both operations are idempotent.
        /// </summary>
        public void OnModResourcesReady()
        {
            SeedStarterParty();

            if (newGameHookInstalled)
                return;

            newGameHookInstalled = true;
            ModEventBus.Subscribe<NewGameStartedEvent>(_ => SeedStarterParty());
        }

        private static void SeedStarterParty()
        {
            GameData.Instance.Runtime.CharacterData.AddCharacterToParty("Marao");
            SkillService.GrantSkill("slave_master");
        }
    }
}

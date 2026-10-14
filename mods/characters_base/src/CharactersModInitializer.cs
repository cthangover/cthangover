using Cthangover.Core.Bootstrap;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Characters
{
    /// <summary>
    /// Engine-mod initializer for the characters subsystem. Publishes the
    /// factories living in this assembly to the kernel contracts
    /// (<see cref="CharacterTemplateRegistry"/>,
    /// <see cref="ActionTemplateRegistry"/>, <see cref="AvatarProviderRegistry"/>)
    /// as soon as the assembly is registered, so core-side consumers —
    /// <c>CharacterData</c>, inventory and skills UI, dialog avatar lookups —
    /// resolve templates without referencing a runtime-compiled mod assembly.
    /// <para/>
    /// Registration happens in <see cref="OnModLoaded"/> (the earliest and
    /// most frequent initializer callback) rather than on resources-ready:
    /// <c>CharacterData.AddCharacterToParty</c> is called from other mods'
    /// <c>OnModResourcesReady</c> (starter party), and initializer callbacks
    /// are delivered per mod registration, so being first here removes any
    /// ordering dependency between mods. The factories themselves only
    /// collect entity data lazily on the first <c>Get</c>, after the mod
    /// registry is finalized. Registration is guarded and idempotent because
    /// <c>ModManager.Reload()</c> re-runs <c>OnModLoaded</c> for every mod.
    /// </summary>
    public class CharactersModInitializer : IModInitializer
    {
        private static bool registered;

        /// <summary>Publishes this mod's factories to the kernel registries; safe to call again after a mod reload.</summary>
        public void OnModLoaded(string modId)
        {
            if (registered)
                return;
            registered = true;

            CharacterTemplateRegistry.Provider = CharacterFactory.Instance;
            ActionTemplateRegistry.Provider = ActionCharacterFactory.Instance;
            AvatarProviderRegistry.Provider = AvatarFactory.Instance;

            GameLogger.Log("MODS", "CharactersModInitializer: template providers registered (character, action, avatar)");
        }

        /// <summary>
        /// Registers the new-game roster reset owner with AppServices
        /// (idempotent — duplicates by Id are ignored). Template collection
        /// stays lazy on first lookup. Persistence needs no setup here:
        /// <see cref="CharactersSaveable"/> is an <c>IModSaveable</c>
        /// discovered by ModSaveableRegistry automatically.
        /// </summary>
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new CharactersResettable());
        }
    }

    /// <summary>
    /// New-game state owner of the characters mod: clears the party roster
    /// and battle set. Today the roster dies implicitly with
    /// <c>GameData.ResetRuntime</c> (it lives inside <c>RuntimeData</c>),
    /// which runs before the reset loop, so this reset is a no-op in the
    /// normal pipeline; it stays correct if the roster ever moves to
    /// mod-owned static storage (the recruiting path) or if the reset loop
    /// runs standalone, and makes the characters mod an explicit new-game
    /// state owner like quests and recruiting. Registered from
    /// <see cref="CharactersModInitializer.OnModResourcesReady"/>;
    /// <see cref="AppServices"/> deduplicates by <see cref="IResettable.Id"/>.
    /// </summary>
    internal sealed class CharactersResettable : IResettable
    {
        /// <summary>Resettable key under which AppServices deduplicates registration.</summary>
        public string Id => "characters";

        /// <summary>Clears the party roster and battle set on "New Game".</summary>
        public void ResetForNewGame()
        {
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.Characters.Clear();
            characterData.BattleSet.Clear();
        }
    }
}

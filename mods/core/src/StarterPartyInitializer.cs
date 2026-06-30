using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Adds the default starter character "Marao" to the player party
    /// during core mod loading. Mods that introduce additional starter
    /// characters should create their own IModInitializer implementations
    /// to populate the party roster on their OnModLoaded call.
    /// </summary>
    public class StarterPartyInitializer : IModInitializer
    {
        /// <summary>
        /// Called by the mod loader when a mod finishes loading.
        /// When the core mod (<c>"core"</c>) loads, adds the default
        /// starter character <c>"Marao"</c> to the player party via
        /// <see cref="CharacterData.AddCharacterToParty"/>.
        /// Other mod IDs are ignored so that additional starter characters
        /// can be provided by separate <see cref="IModInitializer"/>
        /// implementations.
        /// </summary>
        public void OnModLoaded(string modId)
        {
            if (modId == "core")
                GameData.Instance.Runtime.CharacterData.AddCharacterToParty("Marao");
        }
    }
}

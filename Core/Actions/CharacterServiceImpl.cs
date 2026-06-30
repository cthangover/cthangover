using Cthangover.Core.Settings;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Delegates character operations to GameData.Instance.Runtime.CharacterData.
    /// The type string is a character ID — it is passed directly to CharacterData
    /// without parsing. Malformed or missing IDs are handled inside CharacterData
    /// (null-character fallback for unknown IDs).
    /// </summary>
    internal class CharacterServiceImpl : ICharacterService
    {
        /// <summary>
        /// Passes the type string directly to CharacterData.AddCharacterToParty.
        /// The character ID is looked up via CharacterFactory; if no template
        /// exists for this ID, a minimal CharacterInfoData is created with
        /// default attributes. The character is added to the runtime party and
        /// persists through the save system.
        /// </summary>
        public void AddToParty(string type)
        {
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.AddCharacterToParty(type);
        }

        /// <summary>
        /// Passes the type string directly to CharacterData.SendAddNotification.
        /// Unlike AddToParty, this only triggers the UI popup without modifying
        /// the party roster. Useful when a character was recruited earlier (or
        /// externally) but the notification needs to appear at a specific story
        /// beat.
        /// </summary>
        public void SendNotification(string type)
        {
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.SendAddNotification(type);
        }
    }
}

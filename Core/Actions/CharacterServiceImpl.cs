using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Delegates character operations to GameData.Instance.Runtime.CharacterData.
    /// Parses string type names via Enums&lt;CharacterType&gt;.Parse — malformed
    /// type strings throw at parse time rather than silently failing, which is
    /// the desired behavior for debugging scenario scripts.
    /// </summary>
    internal class CharacterServiceImpl : ICharacterService
    {
        /// <summary>
        /// Parses the type string to CharacterType enum and delegates to
        /// CharacterData.AddCharacterToParty. Parsing is strict
        /// (Enums&lt;CharacterType&gt;.Parse) — invalid enum names throw
        /// rather than silently failing, which surfaces typos in scenario
        /// scripts during testing. The character is added to the runtime
        /// party and persists through the save system.
        /// </summary>
        public void AddToParty(string type)
        {
            var characterType = Enums<CharacterType>.Parse(type);
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.AddCharacterToParty(characterType);
        }

        /// <summary>
        /// Parses the type string and delegates to
        /// CharacterData.SendAddNotification. Unlike AddToParty, this
        /// only triggers the UI popup without modifying the party roster.
        /// Useful when a character was recruited earlier (or externally)
        /// but the notification needs to appear at a specific story beat.
        /// </summary>
        public void SendNotification(string type)
        {
            var characterType = Enums<CharacterType>.Parse(type);
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.SendAddNotification(characterType);
        }
    }
}

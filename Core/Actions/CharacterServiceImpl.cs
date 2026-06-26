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
        public void AddToParty(string type)
        {
            var characterType = Enums<CharacterType>.Parse(type);
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.AddCharacterToParty(characterType);
        }

        public void SendNotification(string type)
        {
            var characterType = Enums<CharacterType>.Parse(type);
            var characterData = GameData.Instance.Runtime.CharacterData;
            characterData.SendAddNotification(characterType);
        }
    }
}

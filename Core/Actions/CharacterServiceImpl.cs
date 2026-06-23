using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
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

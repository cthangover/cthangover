using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Cards;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Messages;
using Godot;

namespace Cthangover.Core.Characters
{
	public class CharacterData
	{
		public ISet<CharacterType> BattleSet = new HashSet<CharacterType>();
		public IDictionary<CharacterType, CharacterInfoData> Characters { get; set; } = new Dictionary<CharacterType, CharacterInfoData>();

		public CharacterData()
		{
			Characters[CharacterType.Marao] = Create(CharacterType.Marao);
			BattleSet.Add(CharacterType.Marao);
		}
		
		
		public CharacterInfoData Create(CharacterType characterType)
		{
			var card = CharacterFactory.Instance.Get(characterType.ToString());
			return Create(characterType, card);
		}
		
		public CharacterInfoData Create(CharacterType characterType, Character card)
		{
			var id = characterType.ToString();
			if (card == null)
			{
				return new CharacterInfoData
				{
					Attributes    = new CharacterAttributes(),
					Level         = 0,
					Exp           = 0,
					ID            = id,
					CharacterType = characterType,
				};
			}
			return new CharacterInfoData
			{
				Attributes    = card.Attributes,
				Level         = card.Level,
				Exp           = card.Exp,
				ID            = id,
				CharacterType = characterType,
			};
		}
		
		public void AddCharacterToParty(CharacterType characterType)
		{
			Characters[characterType] = Create(characterType);
			BattleSet.Add(characterType);
		}

		public void SendAddNotification(CharacterType characterType)
		{
			var card = CharacterFactory.Instance.Get(characterType.ToString());
			var name = card?.Name ?? characterType.ToString();
			MessagesHelper.AddMessage(TranslationServer.Translate("ui/add_to_party") + " - " + TranslationServer.Translate(name));
		}
		
	}
}

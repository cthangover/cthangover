using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Messages;
using Godot;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Runtime character roster — owns the party composition and per-character
    /// progression data. BattleSet tracks which characters are in the active
    /// party for combat. Characters dictionary maps character ID strings to
    /// CharacterInfoData where CharacterInfoData carries the runtime-copied
    /// version of the template Character (via CharacterFactory). The initial
    /// party roster is populated by mods through IModInitializer implementations
    /// — each mod adds its default starter characters during mod loading.
    /// AddCharacterToParty both registers the character and adds to BattleSet,
    /// while SendAddNotification produces the UI message via MessagesHelper,
    /// using TranslationServer for localized character names — names come from
    /// CharacterFactory templates, not hardcoded strings.
    /// </summary>
	public class CharacterData
	{
        /// <summary>
        /// Set of character IDs currently in the active battle party.
        /// Populated when characters join the party via
        /// <see cref="AddCharacterToParty"/> and consulted by the battle
        /// system to determine available combatants.
        /// </summary>
		public ISet<string> BattleSet = new HashSet<string>();
        /// <summary>
        /// Full roster of all recruited characters, keyed by character ID
        /// string (e.g. "Marao", "Murakami"). Each entry is a
        /// <see cref="CharacterInfoData"/> carrying the runtime copy of the
        /// character template plus progression state. Populated by mod
        /// initializers during startup and by scenario actions during play.
        /// </summary>
		public IDictionary<string, CharacterInfoData> Characters { get; set; } = new Dictionary<string, CharacterInfoData>();
		
		/// <summary>
		/// Looks up the character template from <see cref="CharacterFactory"/>
		/// by ID and creates a runtime <see cref="CharacterInfoData"/> from it.
		/// Delegates to the two-parameter overload with the resolved template.
		/// </summary>
		/// <param name="characterType">Character ID string (e.g. "Marao").</param>
		/// <returns>A new <see cref="CharacterInfoData"/> instance with stats and level from the template.</returns>
		public CharacterInfoData Create(string characterType)
		{
			var card = CharacterFactory.Instance.Get(characterType);
			return Create(characterType, card);
		}
		
		/// <summary>
		/// Creates a runtime <see cref="CharacterInfoData"/> from a
		/// character template. If the template is <c>null</c> (unknown
		/// character ID), returns a minimal fallback with default
		/// attributes at level 0 — this prevents null-reference errors in
		/// scenario scripts that reference non-existent character IDs.
		/// </summary>
		/// <param name="characterType">Character ID string.</param>
		/// <param name="card">Character template from factory, may be null.</param>
		/// <returns>A new <see cref="CharacterInfoData"/> instance.</returns>
		public CharacterInfoData Create(string characterType, Character card)
		{
			if (card == null)
			{
				return new CharacterInfoData
				{
					Attributes    = new CharacterAttributes(),
					Level         = 0,
					Exp           = 0,
					ID            = characterType,
					CharacterType = characterType,
				};
			}
			return new CharacterInfoData
			{
				Attributes    = card.Attributes,
				Level         = card.Level,
				Exp           = card.Exp,
				ID            = characterType,
				CharacterType = characterType,
			};
		}
		
		/// <summary>
		/// Adds a character to the party roster and active battle set.
		/// Calls <see cref="Create(string)"/> to build the runtime data
		/// from the factory template, inserts it into
		/// <see cref="Characters"/>, and adds the ID to
		/// <see cref="BattleSet"/>. Unknown character IDs create a minimal
		/// fallback entry rather than throwing.
		/// </summary>
		/// <param name="characterType">Character ID string to recruit.</param>
		public void AddCharacterToParty(string characterType)
		{
			Characters[characterType] = Create(characterType);
			BattleSet.Add(characterType);
		}

		/// <summary>
		/// Displays a localized "character joined" UI notification without
		/// modifying the party roster. Looks up the character name from
		/// <see cref="CharacterFactory"/> (falls back to the raw ID if the
		/// template is missing), prepends the localized "ui/add_to_party"
		/// prefix, and dispatches via <see cref="MessagesHelper"/>.
		/// </summary>
		/// <param name="characterType">Character ID string for the notification.</param>
		public void SendAddNotification(string characterType)
		{
			var card = CharacterFactory.Instance.Get(characterType);
			var name = card?.Name ?? characterType;
			MessagesHelper.AddMessage(TranslationServer.Translate("ui/add_to_party") + " - " + TranslationServer.Translate(name));
		}
		
	}
}

using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
	/// <summary>
	/// Displays a single character's portrait, name, and three <see cref="ActionSlotBehaviour"/>
	/// instances. Reads existing action assignments from <see cref="CharacterInfoData.ActionSlots"/>
	/// when shown and writes changes back when actions are redistributed. The parent
	/// <see cref="CharacterPanelBehaviour"/> owns drag orchestration and triggers slot updates.
	/// </summary>
	public partial class CharacterCardBehaviour : Control
	{
		public string CharacterId { get; private set; }
		public ActionSlotBehaviour[] Slots { get; } = new ActionSlotBehaviour[3];

		private TextureRect _portrait;
		private Label _nameLabel;
		private VBoxContainer _slotsContainer;
		private CharacterInfoData _characterInfo;
		private PackedScene _actionCardScene;

		public override void _Ready()
		{
			_portrait = GetNodeOrNull<TextureRect>("Margin/VBox/Header/Portrait");
			_nameLabel = GetNodeOrNull<Label>("Margin/VBox/Header/NameLabel");
			_slotsContainer = GetNodeOrNull<VBoxContainer>("Margin/VBox/Slots");

			GatherSlots();
		}

		private void GatherSlots()
		{
			if (_slotsContainer == null)
				return;

			var children = _slotsContainer.GetChildren().OfType<ActionSlotBehaviour>().ToList();
			for (int i = 0; i < 3 && i < children.Count; i++)
				Slots[i] = children[i];
		}

		/// <summary>
		/// Populates the card from a <see cref="CharacterInfoData"/> entry and its factory template.
		/// Loads portrait, name, and fills action slots from <see cref="CharacterInfoData.ActionSlots"/>.
		/// </summary>
		public void SetCharacter(string characterId, CharacterInfoData info, PackedScene actionCardScene)
		{
			CharacterId = characterId;
			_characterInfo = info;
			_actionCardScene = actionCardScene;

			var template = CharacterFactory.Instance.Get(characterId);

			if (_portrait != null && template?.Image != null)
				_portrait.Texture = template.Image;

			if (_nameLabel != null)
				_nameLabel.Text = template != null
					? TranslationServer.Translate(template.Name)
					: characterId;

			GatherSlots();
			RefreshSlots();
		}

		/// <summary>
		/// Reloads all three slots from <see cref="CharacterInfoData.ActionSlots"/>,
		/// creating <see cref="ActionCardBehaviour"/> instances for assigned actions.
		/// </summary>
		public void RefreshSlots()
		{
			if (_characterInfo == null)
				return;

			var actionSlots = _characterInfo.ActionSlots;
			if (actionSlots == null)
			{
				_characterInfo.ActionSlots = new System.Collections.Generic.List<string> { null, null, null };
				actionSlots = _characterInfo.ActionSlots;
			}

			while (actionSlots.Count < 3)
				actionSlots.Add(null);

			for (int i = 0; i < 3; i++)
			{
				if (Slots[i] == null)
					continue;

				Slots[i].Init(CharacterId, i);

				var actionId = i < actionSlots.Count ? actionSlots[i] : null;
				if (!string.IsNullOrEmpty(actionId))
				{
					var action = ActionCharacterFactory.Instance.Get(actionId);
					if (action != null)
					{
						var card = _actionCardScene.Instantiate<ActionCardBehaviour>();
						Slots[i].AddChild(card);
						card.SetAction(action);
						Slots[i].PlaceCard(card);
					}
				}
			}
		}

		/// <summary>
		/// Writes current slot assignments back to <see cref="CharacterInfoData.ActionSlots"/>.
		/// </summary>
		public void CommitSlots()
		{
			if (_characterInfo == null)
				return;

			if (_characterInfo.ActionSlots == null)
				_characterInfo.ActionSlots = new System.Collections.Generic.List<string>();

			while (_characterInfo.ActionSlots.Count < 3)
				_characterInfo.ActionSlots.Add(null);

			for (int i = 0; i < 3; i++)
			{
				if (i < _characterInfo.ActionSlots.Count)
					_characterInfo.ActionSlots[i] = Slots[i]?.GetActionId();
				else
					_characterInfo.ActionSlots.Add(Slots[i]?.GetActionId());
			}
		}
	}
}

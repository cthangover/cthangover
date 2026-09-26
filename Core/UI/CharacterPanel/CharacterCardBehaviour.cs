using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
	/// <summary>
	/// Party member column of the character panel: portrait, name, derived
	/// stats and the three <see cref="ActionSlotBehaviour"/> sockets that the
	/// player fills from the action pool. The card owns the slot→save-data
	/// binding: <see cref="RefreshSlots"/> reads
	/// <see cref="CharacterInfoData.ActionSlots"/> and materializes cards,
	/// <see cref="CommitSlots"/> writes the current placement back. Cards are
	/// created directly in code (no mod scene) so mods extend the card pool
	/// through <c>ActionCharacterFactory</c> data, not through scene overrides.
	/// </summary>
	public partial class CharacterCardBehaviour : PanelContainer
	{
		/// <summary>Factory key of the character shown by this card.</summary>
		public string CharacterId { get; private set; }

		/// <summary>Fixed three sockets, index 0..2 maps to save-data slot order.</summary>
		public ActionSlotBehaviour[] Slots { get; } = new ActionSlotBehaviour[3];

		private static readonly StyleBoxFlat CardStyle = UiKit.Frame(
			UiTokens.RadiusMd, UiTokens.SurfaceRaised, UiTokens.Line);

		private TextureRect _portrait;
		private Label _nameLabel;
		private Label _subLabel;
		private HFlowContainer _statsRow;
		private CharacterInfoData _characterInfo;

		public CharacterCardBehaviour()
		{
			MouseFilter = MouseFilterEnum.Stop;
			AddThemeStyleboxOverride("panel", CardStyle);
			EnsureBuilt();
		}

		/// <summary>
		/// Populates the card from a runtime character entry and its factory
		/// template: portrait, localized name, template size caption, derived
		/// stats, then the slot cards themselves.
		/// </summary>
		public void SetCharacter(string characterId, CharacterInfoData info)
		{
			CharacterId = characterId;
			_characterInfo = info;

			var template = CharacterFactory.Instance.Get(characterId);
			if (_portrait != null)
				_portrait.Texture = template?.Image;
			if (_nameLabel != null)
				_nameLabel.Text = ActionCharacterUi.Translate(template?.Name ?? characterId);
			if (_subLabel != null)
				_subLabel.Text = $"{characterId} · " + string.Format(
					ActionCharacterUi.Translate("ui/character_panel/template"), template?.Actions?.Count ?? 0);

			BuildStats();
			RefreshSlots();
		}

		/// <summary>
		/// Rebuilds all three sockets from <see cref="CharacterInfoData.ActionSlots"/>.
		/// Missing list entries are normalized to three slots with trailing
		/// <c>null</c>s, matching the save format written by <see cref="CommitSlots"/>.
		/// </summary>
		public void RefreshSlots()
		{
			if (_characterInfo == null)
				return;

			if (_characterInfo.ActionSlots == null)
				_characterInfo.ActionSlots = new List<string> { null, null, null };
			while (_characterInfo.ActionSlots.Count < 3)
				_characterInfo.ActionSlots.Add(null);

			for (var i = 0; i < 3; i++)
			{
				var slot = Slots[i];
				if (slot == null)
					continue;

				slot.Init(CharacterId, i);
				if (slot.CurrentCard != null)
				{
					slot.CurrentCard.QueueFree();
					slot.RemoveCard();
				}

				var actionId = _characterInfo.ActionSlots[i];
				if (string.IsNullOrEmpty(actionId))
					continue;

				var action = ActionCharacterFactory.Instance.Get(actionId);
				if (action == null)
					continue;

				var card = new ActionCardBehaviour();
				slot.AddChild(card);
				card.SetAction(action);
				slot.PlaceCard(card);
			}
		}

		/// <summary>Writes the current socket assignments back to <see cref="CharacterInfoData.ActionSlots"/>.</summary>
		public void CommitSlots()
		{
			if (_characterInfo == null)
				return;

			if (_characterInfo.ActionSlots == null)
				_characterInfo.ActionSlots = new List<string>();
			while (_characterInfo.ActionSlots.Count < 3)
				_characterInfo.ActionSlots.Add(null);

			for (var i = 0; i < 3; i++)
				_characterInfo.ActionSlots[i] = Slots[i]?.GetActionId();
		}

		private void EnsureBuilt()
		{
			CustomMinimumSize = new Vector2(520, 0);

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 14);
			margin.AddThemeConstantOverride("margin_right", 14);
			margin.AddThemeConstantOverride("margin_top", 12);
			margin.AddThemeConstantOverride("margin_bottom", 14);
			margin.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(margin);

			var root = new VBoxContainer();
			root.AddThemeConstantOverride("separation", 10);
			root.MouseFilter = MouseFilterEnum.Ignore;
			margin.AddChild(root);

			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 12);
			header.MouseFilter = MouseFilterEnum.Ignore;
			root.AddChild(header);

			var portraitPanel = new PanelContainer();
			portraitPanel.CustomMinimumSize = new Vector2(58, 58);
			portraitPanel.AddThemeStyleboxOverride("panel", UiKit.Frame(
				UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line));
			portraitPanel.MouseFilter = MouseFilterEnum.Ignore;
			header.AddChild(portraitPanel);

			_portrait = new TextureRect();
			_portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			_portrait.MouseFilter = MouseFilterEnum.Ignore;
			portraitPanel.AddChild(_portrait);

			var info = new VBoxContainer();
			info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			info.AddThemeConstantOverride("separation", 4);
			info.MouseFilter = MouseFilterEnum.Ignore;
			header.AddChild(info);

			_nameLabel = ActionCharacterUi.MakeLabel(string.Empty, 19, ActionCharacterUi.InkBright);
			_nameLabel.ClipText = true;
			info.AddChild(_nameLabel);

			_subLabel = ActionCharacterUi.MakeLabel(string.Empty, 10, UiTokens.Faint);
			_subLabel.ClipText = true;
			info.AddChild(_subLabel);

			_statsRow = new HFlowContainer();
			_statsRow.AddThemeConstantOverride("h_separation", 5);
			_statsRow.AddThemeConstantOverride("v_separation", 3);
			_statsRow.MouseFilter = MouseFilterEnum.Ignore;
			info.AddChild(_statsRow);

			var slotsBox = new VBoxContainer();
			slotsBox.AddThemeConstantOverride("separation", 8);
			slotsBox.MouseFilter = MouseFilterEnum.Ignore;
			root.AddChild(slotsBox);

			for (var i = 0; i < 3; i++)
			{
				var slot = new ActionSlotBehaviour();
				slotsBox.AddChild(slot);
				Slots[i] = slot;
			}
		}

		private void BuildStats()
		{
			ClearChildren(_statsRow);
			var attributes = _characterInfo?.Attributes;
			if (attributes == null)
				return;

			var health = attributes.Health;
			var healthText = health.BaseValue > 0
				? $"{health.Value}/{health.BaseValue}"
				: health.Value.ToString();
			_statsRow.AddChild(UiKit.StatChip(
				ActionCharacterUi.Translate("ui/character_panel/stat_hp"), healthText, UiTokens.HpText));
			_statsRow.AddChild(UiKit.StatChip(
				ActionCharacterUi.Translate("ui/character_panel/stat_ap"), attributes.Point.Value.ToString(), UiTokens.AccentBright));
			_statsRow.AddChild(UiKit.StatChip(
				ActionCharacterUi.Translate("ui/character_panel/stat_atk"), attributes.Attack.Value.ToString(), UiTokens.Ink));
			_statsRow.AddChild(UiKit.StatChip(
				ActionCharacterUi.Translate("ui/character_panel/stat_def"), attributes.Defence.Value.ToString(), UiTokens.Ink));
		}

		private static void ClearChildren(Node parent)
		{
			if (parent == null)
				return;
			foreach (var child in parent.GetChildren())
				child.QueueFree();
		}
	}
}

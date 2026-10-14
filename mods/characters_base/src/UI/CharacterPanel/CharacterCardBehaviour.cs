using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Party member column of the character panel: portrait, name, derived
    /// stats and the three <see cref="ActionSlotBehaviour"/> sockets that the
    /// player fills from the action pool. The static layout (margins, header,
    /// stat row, slot stack with the three <c>action_slot.tscn</c> instances)
    /// lives in <c>scenes/ui/character_card.tscn</c>; instances are created by
    /// the panel through <see cref="ModScenePrefab"/>, never via <c>new</c>.
    /// <para/>
    /// The card owns the slot→save-data binding: <see cref="RefreshSlots"/>
    /// reads <see cref="CharacterInfoData.ActionSlots"/> and materializes
    /// action cards (through the card prefab), <see cref="CommitSlots"/> writes
    /// the current placement back. Mods extend the card pool through
    /// <c>ActionCharacterFactory</c> data, not through scene overrides.
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
        private bool _styled;

        /// <summary>Applies the card frame; node references resolve lazily so the card can be bound before entering the tree.</summary>
        public CharacterCardBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
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
            ResolveNodes();

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

                var card = CharacterPanelPrefabs.InstantiateCard();
                if (card == null)
                    continue;

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

        private void ResolveNodes()
        {
            _portrait ??= GetNodeOrNull<TextureRect>("Margin/Root/Header/PortraitPanel/Portrait");
            _nameLabel ??= GetNodeOrNull<Label>("Margin/Root/Header/Info/NameLabel");
            _subLabel ??= GetNodeOrNull<Label>("Margin/Root/Header/Info/SubLabel");
            _statsRow ??= GetNodeOrNull<HFlowContainer>("Margin/Root/Header/Info/StatsRow");

            if (!_styled)
            {
                _styled = true;
                AddThemeStyleboxOverride("panel", CardStyle);
            }

            if (Slots[0] == null)
            {
                Slots[0] = GetNodeOrNull<ActionSlotBehaviour>("Margin/Root/SlotsBox/Slot0");
                Slots[1] = GetNodeOrNull<ActionSlotBehaviour>("Margin/Root/SlotsBox/Slot1");
                Slots[2] = GetNodeOrNull<ActionSlotBehaviour>("Margin/Root/SlotsBox/Slot2");
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

using Cthangover.Core.Characters;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// A single action socket on a character card. Holds at most one
    /// <see cref="ActionCardBehaviour"/> or shows an empty drop hint. The
    /// static visuals (frame panel, "СЛОТ N" caption, empty hint) live in
    /// <c>scenes/ui/action_slot.tscn</c>; instances are created by
    /// <see cref="CharacterCardBehaviour"/> through <see cref="ModScenePrefab"/>,
    /// never via <c>new</c>. The slot owns only its own visuals; drag input,
    /// hit-testing and swap rules live in <see cref="CharacterPanelBehaviour"/>,
    /// which asks the slot to highlight while a card hovers over it. Assignment
    /// changes are written back to save data by
    /// <see cref="CharacterCardBehaviour.CommitSlots"/>.
    /// </summary>
    public partial class ActionSlotBehaviour : Control
    {
        /// <summary>Character ID this slot belongs to; part of the save-data key.</summary>
        public string CharacterId { get; private set; }

        /// <summary>Zero-based slot index within the character card.</summary>
        public int SlotIndex { get; private set; }

        /// <summary>Card currently placed in the slot, or <c>null</c> when empty.</summary>
        public ActionCardBehaviour CurrentCard { get; private set; }

        /// <summary>True when no card is placed; empty slots accept any card.</summary>
        public bool IsEmpty => CurrentCard == null;

        private static readonly StyleBoxFlat EmptyFrame = UiKit.Frame(
            UiTokens.RadiusMd, UiTokens.Surface0, UiTokens.LineFaint);
        private static readonly StyleBoxFlat FilledFrame = UiKit.Frame(
            UiTokens.RadiusMd, UiTokens.Surface1, UiTokens.LineCard);
        private static readonly StyleBoxFlat HoverFrame = UiKit.Frame(
            UiTokens.RadiusMd, UiTokens.DropBg, UiTokens.TypePassive, 2);

        private Panel _frame;
        private Label _slotLabel;
        private Label _emptyLabel;
        private bool _highlighted;

        /// <summary>
        /// Binds the save-data identity of the slot and renders the "СЛОТ N"
        /// caption. Must be called by the owning card before use.
        /// </summary>
        public void Init(string characterId, int slotIndex)
        {
            CharacterId = characterId;
            SlotIndex = slotIndex;
            ResolveNodes();
            _slotLabel.Text = string.Format(
                ActionCharacterUi.Translate("ui/character_panel/slot"), slotIndex + 1).ToUpperInvariant();
            UpdateState();
        }

        /// <summary>
        /// Places the given card into this slot, detaching it from any previous
        /// slot. Passing <c>null</c> clears the slot. The displaced card (if any)
        /// is handed back to the caller by returning it through
        /// <see cref="CurrentCard"/> before the call.
        /// </summary>
        public void PlaceCard(ActionCardBehaviour card)
        {
            if (card == null)
            {
                RemoveCard();
                return;
            }

            if (CurrentCard != null && CurrentCard != card)
            {
                if (CurrentCard.GetParent() == this)
                    RemoveChild(CurrentCard);
                CurrentCard.CurrentSlot = null;
                CurrentCard = null;
            }

            card.CurrentSlot?.RemoveCard();
            card.CurrentSlot = this;

            if (card.GetParent() != this)
            {
                card.GetParent()?.RemoveChild(card);
                AddChild(card);
            }

            card.SetCompact(false);
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            CurrentCard = card;
            UpdateState();
        }

        /// <summary>Detaches the current card without destroying it; the card object stays valid for swapping.</summary>
        public void RemoveCard()
        {
            if (CurrentCard == null)
                return;

            CurrentCard.CurrentSlot = null;
            CurrentCard = null;
            UpdateState();
        }

        /// <summary>Action ID currently in this slot, or <c>null</c> when empty.</summary>
        public string GetActionId()
        {
            return CurrentCard?.ActionData?.ID;
        }

        /// <summary>Green frame while a dragged card hovers over the slot.</summary>
        public void SetHighlight(bool highlighted)
        {
            _highlighted = highlighted;
            ApplyFrame();
        }

        private void ResolveNodes()
        {
            _frame ??= GetNodeOrNull<Panel>("Frame");
            _slotLabel ??= GetNodeOrNull<Label>("SlotLabel");
            _emptyLabel ??= GetNodeOrNull<Label>("EmptyLabel");

            if (_emptyLabel != null && string.IsNullOrEmpty(_emptyLabel.Text))
                _emptyLabel.Text = ActionCharacterUi.Translate("ui/character_panel/slot_empty");
        }

        private void UpdateState()
        {
            ResolveNodes();
            if (_emptyLabel != null)
                _emptyLabel.Visible = CurrentCard == null;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            ResolveNodes();
            if (_frame == null)
                return;
            _frame.AddThemeStyleboxOverride("panel",
                _highlighted ? HoverFrame : (CurrentCard == null ? EmptyFrame : FilledFrame));
        }
    }
}

using Cthangover.Core.Characters;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// A single action socket on a character card. Holds at most one
    /// <see cref="ActionCardBehaviour"/> or shows an empty drop hint. The slot
    /// owns only its own visuals (frame, number caption, empty hint); drag
    /// input, hit-testing and swap rules live in <see cref="CharacterPanelBehaviour"/>,
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

        public ActionSlotBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(0, 88);
            EnsureBuilt();
        }

        /// <summary>
        /// Binds the save-data identity of the slot and renders the "СЛОТ N"
        /// caption. Must be called by the owning card before use.
        /// </summary>
        public void Init(string characterId, int slotIndex)
        {
            CharacterId = characterId;
            SlotIndex = slotIndex;
            _slotLabel.Text = string.Format(
                ActionCharacterUi.Translate("ui/character_panel/slot"), slotIndex + 1).ToUpperInvariant();
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

        private void EnsureBuilt()
        {
            _frame = new Panel();
            _frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _frame.AddThemeStyleboxOverride("panel", EmptyFrame);
            _frame.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_frame);

            _slotLabel = ActionCharacterUi.MakeLabel(string.Empty, 9, UiTokens.Faint);
            _slotLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            _slotLabel.OffsetLeft = 8;
            _slotLabel.OffsetTop = 5;
            _slotLabel.OffsetRight = 120;
            _slotLabel.OffsetBottom = 21;
            AddChild(_slotLabel);

            _emptyLabel = ActionCharacterUi.MakeLabel(
                ActionCharacterUi.Translate("ui/character_panel/slot_empty"), 12, UiTokens.Faint);
            _emptyLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _emptyLabel.VerticalAlignment = VerticalAlignment.Center;
            AddChild(_emptyLabel);
        }

        private void UpdateState()
        {
            if (_emptyLabel != null)
                _emptyLabel.Visible = CurrentCard == null;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_frame == null)
                return;
            _frame.AddThemeStyleboxOverride("panel",
                _highlighted ? HoverFrame : (CurrentCard == null ? EmptyFrame : FilledFrame));
        }
    }
}

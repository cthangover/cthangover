using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// A single action slot on a character card. Can hold one <see cref="ActionCardBehaviour"/>
    /// or be empty. Serves as a drop target during drag operations. Each slot is identified
    /// by its <see cref="CharacterId"/> and <see cref="SlotIndex"/>, and reads/writes the
    /// action ID from <see cref="Settings.CharacterInfoData.ActionSlots"/>.
    /// </summary>
    public partial class ActionSlotBehaviour : Control
    {
        public string CharacterId { get; private set; }
        public int SlotIndex { get; private set; }
        public ActionCardBehaviour CurrentCard { get; private set; }
        public bool IsEmpty => CurrentCard == null;

        private ColorRect _bg;

        public ActionSlotBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _Ready()
        {
            EnsureBg();
            Resized += OnResized;
        }

        private void EnsureBg()
        {
            if (_bg == null)
            {
                _bg = GetNodeOrNull<ColorRect>("Bg");
                if (_bg == null)
                {
                    _bg = new ColorRect
                    {
                        Name = "Bg",
                        Color = new Color(0.1f, 0.1f, 0.15f, 0.5f),
                        MouseFilter = MouseFilterEnum.Ignore
                    };
                    AddChild(_bg);
                    MoveChild(_bg, 0);
                }
            }
        }

		private void OnResized()
		{
			if (_bg != null)
				_bg.SetDeferred(Control.PropertyName.Size, Size);
			if (CurrentCard != null)
				CurrentCard.SetDeferred(Control.PropertyName.Size, Size);
		}

        /// <summary>
        /// Initializes the slot with a character ID and index. Must be called before use.
        /// </summary>
        public void Init(string characterId, int slotIndex)
        {
            CharacterId = characterId;
            SlotIndex = slotIndex;
        }

        /// <summary>
        /// Places the given card into this slot, removing it from any previous slot.
        /// The card is added as a child and resized to fill the slot.
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

			card.Position = Vector2.Zero;
			card.SetDeferred(Control.PropertyName.Size, Size);
			CurrentCard = card;
        }

        /// <summary>
        /// Removes the current card from this slot without destroying it.
        /// </summary>
        public void RemoveCard()
        {
            if (CurrentCard == null)
                return;

            CurrentCard.CurrentSlot = null;
            CurrentCard = null;
        }

        /// <summary>
        /// Returns the action ID currently in this slot, or <c>null</c> if empty.
        /// </summary>
        public string GetActionId()
        {
            return CurrentCard?.ActionData?.ID;
        }
    }
}

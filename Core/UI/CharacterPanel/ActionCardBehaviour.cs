using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Draggable action card that sits inside an <see cref="ActionSlotBehaviour"/>
    /// or in the <see cref="ActionPoolBehaviour"/>. Drag input is handled entirely
    /// by <see cref="CharacterPanelBehaviour._Input"/> — this card does not process
    /// its own GuiInput because <c>GuiInput</c> stops firing when the cursor leaves
    /// the card bounds mid-drag.
    /// </summary>
    public partial class ActionCardBehaviour : Control
    {
        public ActionCharacter ActionData { get; private set; }
        public ActionSlotBehaviour CurrentSlot { get; set; }

        private TextureRect _icon;
        private Label _nameLabel;

        public ActionCardBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
        }

        /// <summary>
        /// Binds the action data and populates the visual elements.
        /// </summary>
        public void SetAction(ActionCharacter action)
        {
            ActionData = action;
            if (action == null)
                return;

            EnsureIcon();
            EnsureLabel();

            _icon.Texture = action.Image;
            _nameLabel.Text = TranslationServer.Translate(action.Name);
            TooltipText = TranslationServer.Translate(action.Description ?? "");
        }

        private void EnsureIcon()
        {
            if (_icon == null)
            {
                _icon = GetNodeOrNull<TextureRect>("Icon");
                if (_icon == null)
                {
                    _icon = new TextureRect
                    {
                        Name = "Icon",
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                    };
                    AddChild(_icon);
                }
            }
        }

        private void EnsureLabel()
        {
            if (_nameLabel == null)
            {
                _nameLabel = GetNodeOrNull<Label>("NameLabel");
                if (_nameLabel == null)
                {
                    _nameLabel = new Label
                    {
                        Name = "NameLabel",
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    AddChild(_nameLabel);
                }
            }
        }

        /// <summary>
        /// Returns the card to its owning slot, or if none (pool card), to the pool.
        /// Reparents from wherever the drag left it.
        /// </summary>
        public void ReturnToOrigin()
        {
            if (CurrentSlot != null)
            {
                if (GetParent() != CurrentSlot)
                {
                    GetParent()?.RemoveChild(this);
                    CurrentSlot.AddChild(this);
                }
                Position = Vector2.Zero;
                Size = CurrentSlot.Size;
            }
        }
    }
}

using Cthangover.Core.Cards;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    /// <summary>
    /// UI node representing a single action card that the player can drag onto a target.
    /// Implements <see cref="ICard"/> so <see cref="CardController"/> can hit-test drag targets.
    /// Displayed in the <see cref="CardController"/>'s action panel below the character cards.
    /// Supports hover scale-up animation, selection highlighting (<see cref="Select"/>, <see cref="Attack"/>, <see cref="Invalid"/>),
    /// and info updates from the underlying <see cref="ActionCharacter"/> data model.
    /// </summary>
    public partial class ActionCardNode : ModWidget, ICard
    {
        private TextureRect frame;
        private TextureRect image;
        private Label nameLabel;

        /// <inheritdoc />
        public TextureRect Frame => frame;
        /// <inheritdoc />
        public TextureRect Image => image;

        /// <summary>
        /// The <see cref="ActionCharacter"/> data model backing this card. Contains the action's
        /// name, image, type, and stat modifiers used during execution.
        /// </summary>
        public ActionCharacter Card { get; set; }

        /// <summary>
        /// Returns <c>this</c> — action cards are <see cref="Control"/> nodes themselves,
        /// unlike <see cref="CharacterCardNode"/> which wraps a separate control.
        /// </summary>
        public Control GetControlNode() => this;

        private Tween hoverTween;
        private float baseY;
        private bool isHovered;
        private Vector2 _baseScale;

        protected override void Construct()
        {
            CustomMinimumSize = new Vector2(200, 280);
            Size = new Vector2(200, 280);

            image = new TextureRect();
            image.SetAnchorsPreset(LayoutPreset.FullRect);
            image.OffsetLeft = 15;
            image.OffsetTop = 15;
            image.OffsetRight = -15;
            image.OffsetBottom = -40;
            image.MouseFilter = MouseFilterEnum.Ignore;
            image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            image.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            AddChild(image);

            frame = new TextureRect();
            frame.SetAnchorsPreset(LayoutPreset.FullRect);
            frame.MouseFilter = MouseFilterEnum.Ignore;
            frame.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            frame.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            AddChild(frame);

            nameLabel = new Label();
            nameLabel.SetAnchorsPreset(LayoutPreset.BottomWide);
            nameLabel.OffsetTop = -40;
            nameLabel.OffsetBottom = 0;
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(nameLabel);

            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        /// <summary>
        /// Placeholder for selection highlight. Currently unused — action cards use
        /// <see cref="Attack"/> and <see cref="Invalid"/> for drag-over feedback instead.
        /// </summary>
        public void Select()
        {
        }

        /// <summary>
        /// Placeholder for attack-mode highlight. Currently unused — target highlighting
        /// is applied by <see cref="ICardActionStrategy.HighlightTarget"/> instead.
        /// </summary>
        public void Attack()
        {
        }

        /// <summary>
        /// Placeholder for invalid-target highlight. Currently unused — invalidity is
        /// indicated by <see cref="ICardActionStrategy.HighlightTarget"/> calling
        /// <see cref="CharacterCardNode.Invalid"/> on the target card instead.
        /// </summary>
        public void Invalid()
        {
        }

        /// <summary>
        /// Resets the Z-index to 0 when the card is no longer being dragged.
        /// </summary>
        public void Unselect()
        {
            ZIndex = 0;
        }

        private void OnMouseEntered()
        {
            if (isHovered) return;
            isHovered = true;
            baseY = Position.Y;
            hoverTween?.Kill();
            hoverTween = CreateTween();
            hoverTween.SetParallel(true);
            hoverTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            hoverTween.TweenProperty(this, "position:y", baseY - 10f, 0.12f);
            hoverTween.TweenProperty(this, "scale", _baseScale * 1.15f, 0.12f);
        }

        private void OnMouseExited()
        {
            if (!isHovered) return;
            isHovered = false;
            hoverTween?.Kill();
            hoverTween = CreateTween();
            hoverTween.SetParallel(true);
            hoverTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            hoverTween.TweenProperty(this, "position:y", baseY, 0.12f);
            hoverTween.TweenProperty(this, "scale", _baseScale, 0.12f);
        }

        /// <summary>
        /// Initializes the card with data from <paramref name="card"/>, caches the base scale
        /// for hover animation, sets the card image texture, and refreshes displayed text.
        /// Called by <see cref="CardController.CreateAction"/>.
        /// </summary>
        public void Init(ActionCharacter card)
        {
            Card = card;
            _baseScale = Scale;

            if (image != null)
                image.Texture = card.Image;
            UpdateInfo();
        }

        /// <summary>
        /// Refreshes the displayed name label from the translated <see cref="ActionCharacter.Name"/>.
        /// </summary>
        public void UpdateInfo()
        {
            if (nameLabel != null && Card != null)
                nameLabel.Text = TranslationServer.Translate(Card.Name);
        }
    }
}

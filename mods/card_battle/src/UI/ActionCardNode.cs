using Cthangover.Core.Cards;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    public partial class ActionCardNode : ModWidget, ICard
    {
        private TextureRect frame;
        private TextureRect image;
        private Label nameLabel;

        public TextureRect Frame => frame;
        public TextureRect Image => image;

        public ActionCharacter Card { get; set; }

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

        public void Select()
        {
        }

        public void Attack()
        {
        }

        public void Invalid()
        {
        }

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

        public void Init(ActionCharacter card)
        {
            Card = card;
            _baseScale = Scale;

            if (image != null)
                image.Texture = card.Image;
            UpdateInfo();
        }

        public void UpdateInfo()
        {
            if (nameLabel != null && Card != null)
                nameLabel.Text = TranslationServer.Translate(Card.Name);
        }
    }
}

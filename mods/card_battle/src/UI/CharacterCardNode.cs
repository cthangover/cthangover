using Cthangover.Core.Cards;
using Cthangover.Core.Mods;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    public partial class CharacterCardNode : ModWidget, ICard
    {
        private ColorRect teamOutline;
        private TextureRect frame;
        private TextureRect image;
        private TextureRect selection;

        private ColorRect healthRect;
        private Label nameLabel;
        private Label defenceLabel;
        private Label attackLabel;
        private Label pointsLabel;

        private Color selectionState;
        private float defaultWidth;
        private float baseY;
        private bool isHovered;
        private Tween hoverTween;
        private Tween selectionTween;
        private static Texture2D cachedSelectTex;
        private static bool selectTexLoaded;

        public bool IsPlayer { get; set; }
        public bool IsDead { get; set; }

        private int _baseZIndex;
        private bool _selected;
        private Vector2 _baseScale;

        public TextureRect Frame => frame;
        public TextureRect Image => image;
        public TextureRect[] AllImages => _allImages ??= new[] { frame, image, selection };
        private TextureRect[] _allImages;

        public Character Card { get; set; }

        public Control GetControlNode() => this;

        protected override void Construct()
        {
            CustomMinimumSize = new Vector2(230, 420);
            Size = new Vector2(230, 420);

            teamOutline = new ColorRect();
            teamOutline.SetAnchorsPreset(LayoutPreset.FullRect);
            teamOutline.OffsetLeft = -4;
            teamOutline.OffsetTop = -4;
            teamOutline.OffsetRight = 4;
            teamOutline.OffsetBottom = 4;
            teamOutline.MouseFilter = MouseFilterEnum.Ignore;
            teamOutline.Color = new Color(0, 0, 0, 0);
            AddChild(teamOutline);

            image = new TextureRect();
            image.SetAnchorsPreset(LayoutPreset.FullRect);
            image.OffsetLeft = 20;
            image.OffsetTop = 20;
            image.OffsetRight = -20;
            image.OffsetBottom = -60;
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

            var infoPanel = new Control();
            infoPanel.SetAnchorsPreset(LayoutPreset.BottomWide);
            infoPanel.OffsetTop = -60;
            infoPanel.OffsetBottom = 0;
            AddChild(infoPanel);

            nameLabel = new Label();
            nameLabel.SetAnchorsPreset(LayoutPreset.TopWide);
            nameLabel.OffsetBottom = 18;
            nameLabel.AddThemeFontSizeOverride("font_size", 16);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            infoPanel.AddChild(nameLabel);

            var healthBar = new Control();
            healthBar.SetAnchorsPreset(LayoutPreset.BottomWide);
            healthBar.OffsetTop = -18;
            healthBar.OffsetBottom = 0;
            infoPanel.AddChild(healthBar);

            var healthBg = new ColorRect();
            healthBg.SetAnchorsPreset(LayoutPreset.FullRect);
            healthBg.Color = new Color(0.2f, 0.2f, 0.2f, 1);
            healthBar.AddChild(healthBg);

            healthRect = new ColorRect();
            healthRect.SetAnchorsPreset(LayoutPreset.FullRect);
            healthRect.Color = new Color(0, 0.8f, 0, 1);
            healthBar.AddChild(healthRect);

            var labels = new HBoxContainer();
            labels.SetAnchorsPreset(LayoutPreset.VcenterWide);
            labels.Alignment = BoxContainer.AlignmentMode.Center;
            infoPanel.AddChild(labels);

            labels.AddChild(new Label { Text = "S", HorizontalAlignment = HorizontalAlignment.Center });

            defenceLabel = new Label();
            defenceLabel.Text = "0";
            labels.AddChild(defenceLabel);

            labels.AddChild(new Label { Text = "A", HorizontalAlignment = HorizontalAlignment.Center });

            attackLabel = new Label();
            attackLabel.Text = "0";
            labels.AddChild(attackLabel);

            labels.AddChild(new Label { Text = "P", HorizontalAlignment = HorizontalAlignment.Center });

            pointsLabel = new Label();
            pointsLabel.Text = "0";
            labels.AddChild(pointsLabel);

            selection = new TextureRect();
            selection.SetAnchorsPreset(LayoutPreset.FullRect);
            selection.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(selection);

            defaultWidth = 230f;
            selectionState = new Color(0, 0, 0, 0);

            if (!selectTexLoaded)
            {
                cachedSelectTex = ModManager.Instance.ResolveTexture("select");
                selectTexLoaded = true;
            }
            if (cachedSelectTex != null)
            {
                selection.Texture = cachedSelectTex;
                selection.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                selection.StretchMode = TextureRect.StretchModeEnum.Scale;
            }

            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        public void Init(Character characterCard)
        {
            Card = characterCard;
            _baseScale = Scale;

            if (image != null)
                image.Texture = characterCard.Image;

            if (frame != null && characterCard?.Image != null)
                frame.Texture = characterCard.Image;

            if (selection != null)
                selection.Modulate = new Color(0, 0, 0, 0);

            selectionState = new Color(0, 0, 0, 0);

            UpdateInfo();
        }

        public void SetTeamColors()
        {
            if (teamOutline != null)
                teamOutline.Color = IsPlayer
                    ? new Color(0.2f, 0.4f, 1f, 0.5f)
                    : new Color(1f, 0.8f, 0.1f, 0.5f);
        }

        public void SetBaseZIndex(int z)
        {
            _baseZIndex = z;
            if (!_selected)
                ZIndex = z;
        }

        public void Select()
        {
            _selected = true;
            selectionState = new Color(0, 1, 0, 1);
            AnimateSelection(selectionState);
            if (selection != null) selection.ZIndex = 1;
            ZIndex = 4096;
        }

        public void Unselect()
        {
            _selected = false;
            selectionState = new Color(0, 0, 0, 0);
            AnimateSelection(selectionState);
            if (selection != null) selection.ZIndex = 0;
            ZIndex = _baseZIndex;
        }

        public void Attack()
        {
            selectionState = new Color(1, 0, 0, 1);
            AnimateSelection(selectionState);
            if (selection != null) selection.ZIndex = 1;
        }

        public void Invalid()
        {
            selectionState = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            AnimateSelection(selectionState);
            if (selection != null) selection.ZIndex = 1;
        }

        private void AnimateSelection(Color target)
        {
            if (selection == null) return;
            selectionTween?.Kill();
            selectionTween = CreateTween();
            selectionTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            selectionTween.TweenProperty(selection, "modulate", target, 0.15f);
        }

        public void UpdateInfo()
        {
            if (healthRect == null || Card == null) return;

            if (nameLabel != null)
                nameLabel.Text = TranslationServer.Translate(Card.Name);
            if (defenceLabel != null)
                defenceLabel.Text = Card.Attributes.Defence.Value.ToString();
            if (attackLabel != null)
                attackLabel.Text = Card.Attributes.Attack.Value.ToString();
            if (pointsLabel != null)
                pointsLabel.Text = Card.Attributes.Point.Value.ToString();

            var sizeDelta = healthRect.Size;
            sizeDelta.X = Card.Attributes.Health.Percent * defaultWidth;
            healthRect.SetDeferred(Control.PropertyName.Size, sizeDelta);
        }

        private void OnMouseEntered()
        {
            if (isHovered) return;
            isHovered = true;
            baseY = Position.Y;

            if (!_selected)
                ZIndex = _baseZIndex + 100;

            hoverTween?.Kill();
            hoverTween = CreateTween();
            hoverTween.SetParallel(true);
            hoverTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            hoverTween.TweenProperty(this, "position:y", baseY - 10f, 0.15f);
            hoverTween.TweenProperty(this, "scale", _baseScale * 1.15f, 0.15f);
        }

        private void OnMouseExited()
        {
            if (!isHovered) return;
            isHovered = false;

            if (!_selected)
                ZIndex = _baseZIndex;

            hoverTween?.Kill();
            hoverTween = CreateTween();
            hoverTween.SetParallel(true);
            hoverTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            hoverTween.TweenProperty(this, "position:y", baseY, 0.15f);
            hoverTween.TweenProperty(this, "scale", _baseScale, 0.15f);
        }
    }
}

using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Visual representation of a single character (player or enemy) in the FF battle UI.
    /// Composite widget containing a sprite, HP bar, name label, and selection overlay.
    /// Supports highlight, flash, shake, and dissolve-death animations via Godot tweens.
    /// Created per-character by <see cref="FFPlayerPanel.Init"/> and
    /// <see cref="FFEnemyPanel.Init"/>. Clickable via <c>GuiInput</c> events;
    /// embedding panels wire these to <see cref="FFBattleCore"/> for action flow.
    /// </summary>
    public partial class FFCharacterWidget : ModWidget
    {
        private TextureRect _sprite;
        private TextureRect _selection;
        private Label _nameLabel;
        private ColorRect _hpBg;
        private ColorRect _hpFill;
        private Tween _animTween;
        private static Texture2D _cachedSelectTex;
        private static bool _selectTexLoaded;
        private static Shader _dissolveShader;

        /// <summary>Whether this widget belongs to the player party (affects highlight/targeting logic).</summary>
        public bool IsPlayer { get; set; }
        /// <summary>Whether the character is dead; set by <see cref="PlayDeathAnimation"/>.</summary>
        public bool IsDead { get; set; }
        /// <summary>The character data model backing this widget: stats, actions, status effects.</summary>
        public Character Card { get; set; }
        /// <summary>Original scale captured at construction; used to restore after death animation.</summary>
        public Vector2 BaseScale { get; private set; }
        /// <summary>Exposes this widget as a <see cref="Godot.Control"/> for layout calculations.</summary>
        public Control ControlNode => this;

        protected override void Construct()
        {
            CustomMinimumSize = new Vector2(180, 260);
            Size = new Vector2(180, 260);

            _sprite = new TextureRect();
            _sprite.SetAnchorsPreset(LayoutPreset.FullRect);
            _sprite.OffsetLeft = 8;
            _sprite.OffsetTop = 8;
            _sprite.OffsetRight = -8;
            _sprite.OffsetBottom = -44;
            _sprite.MouseFilter = MouseFilterEnum.Ignore;
            _sprite.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _sprite.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
            AddChild(_sprite);

            _hpBg = new ColorRect();
            _hpBg.SetAnchorsPreset(LayoutPreset.TopLeft);
            _hpBg.Position = new Vector2(4, Size.Y - 42);
            _hpBg.Size = new Vector2(Size.X - 8, 18);
            _hpBg.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
            _hpBg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_hpBg);

            _hpFill = new ColorRect();
            _hpFill.SetAnchorsPreset(LayoutPreset.TopLeft);
            _hpFill.Position = new Vector2(4, Size.Y - 42);
            _hpFill.Size = new Vector2(Size.X - 8, 18);
            _hpFill.Color = new Color(0.1f, 0.85f, 0.1f, 1f);
            _hpFill.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_hpFill);

            _nameLabel = new Label();
            _nameLabel.SetAnchorsPreset(LayoutPreset.TopLeft);
            _nameLabel.Position = new Vector2(4, Size.Y - 22);
            _nameLabel.Size = new Vector2(Size.X - 8, 18);
            _nameLabel.AddThemeFontSizeOverride("font_size", 13);
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_nameLabel);

            _selection = new TextureRect();
            _selection.SetAnchorsPreset(LayoutPreset.FullRect);
            _selection.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_selection);

            if (!_selectTexLoaded)
            {
                _cachedSelectTex = ModManager.Instance.ResolveTexture("select");
                _selectTexLoaded = true;
            }
            if (_cachedSelectTex != null)
            {
                _selection.Texture = _cachedSelectTex;
                _selection.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                _selection.StretchMode = TextureRect.StretchModeEnum.Scale;
            }
            _selection.Modulate = new Color(0, 0, 0, 0);
        }

        /// <summary>
        /// Binds this widget to a <see cref="Character"/> model. Sets the sprite from
        /// <see cref="Character.Image"/>, localises the name via
        /// <see cref="TranslationServer"/>, and calls <see cref="UpdateInfo"/>
        /// to refresh the HP bar.
        /// </summary>
        public void Init(Character character)
        {
            Card = character;
            BaseScale = Scale;
            IsDead = false;

            if (_sprite != null && character.Image != null)
                _sprite.Texture = character.Image;

            if (_nameLabel != null)
                _nameLabel.Text = TranslationServer.Translate(character.Name);

            UpdateInfo();
        }

        /// <summary>Refreshes the HP bar fill width and colour gradient based on current health percentage.</summary>
        public void UpdateInfo()
        {
            if (_hpFill == null || Card == null)
                return;

            var hpPercent = Card.Attributes.Health.Percent;
            _hpFill.Size = new Vector2((Size.X - 8) * hpPercent, 18);

            if (hpPercent > 0.5f)
                _hpFill.Color = new Color(0.1f, 0.85f, 0.1f, 1f);
            else if (hpPercent > 0.25f)
                _hpFill.Color = new Color(0.85f, 0.85f, 0.1f, 1f);
            else
                _hpFill.Color = new Color(0.85f, 0.1f, 0.1f, 1f);
        }

        /// <summary>Shows a coloured selection overlay with a 0.15s tween fade-in. Used for target highlighting and selection indication.</summary>
        public void Highlight(Color color)
        {
            if (_selection == null)
                return;

            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _animTween.TweenProperty(_selection, "modulate", color, 0.15f);
        }

        /// <summary>Fades out the selection overlay to transparent via a 0.15s tween.</summary>
        public void ClearHighlight()
        {
            if (_selection == null)
                return;

            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _animTween.TweenProperty(_selection, "modulate", new Color(0, 0, 0, 0), 0.15f);
        }

        /// <summary>Rapidly oscillates the widget's position with random offsets (6 iterations) to simulate impact feedback.</summary>
        public void Shake(float intensity, float duration)
        {
            var originalPos = Position;
            _animTween?.Kill();
            _animTween = CreateTween();

            for (int i = 0; i < 6; i++)
            {
                var offset = new Vector2(
                    (float)GD.RandRange(-intensity, intensity),
                    (float)GD.RandRange(-intensity, intensity)
                );
                _animTween.TweenProperty(this, "position", originalPos + offset, duration / 12f);
                _animTween.TweenProperty(this, "position", originalPos, duration / 12f);
            }
        }

        /// <summary>Briefly tints the entire widget with a colour pulse (30% rise, 70% fall) for damage/heal feedback.</summary>
        public void Flash(Color color, float duration)
        {
            var originalModulate = Modulate;
            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.TweenProperty(this, "modulate", color, duration * 0.3f);
            _animTween.TweenProperty(this, "modulate", originalModulate, duration * 0.7f);
        }

        /// <summary>
        /// Plays a dissolve-and-shrink death sequence. If a <c>"scene_transition"</c>
        /// shader is available, applies a dissolve material to the sprite. Fades the
        /// widget alpha, scales it to zero, and hides the HP bar and name label.
        /// Invokes <paramref name="onComplete"/> when finished so the parent panel
        /// can remove the widget from the grid.
        /// </summary>
        public void PlayDeathAnimation(Action onComplete)
        {
            if (IsDead)
            {
                onComplete?.Invoke();
                return;
            }

            IsDead = true;
            MouseFilter = MouseFilterEnum.Ignore;
            ClearHighlight();

            _animTween?.Kill();
            var duration = 0.7f;

            if (_dissolveShader == null)
                _dissolveShader = ModManager.Instance.ResolveShader("scene_transition");

            if (_dissolveShader != null && _sprite?.Texture != null)
            {
                var mat = new ShaderMaterial { Shader = _dissolveShader };
                mat.SetShaderParameter("noise_scale", 5f);
                mat.SetShaderParameter("smoothness", 0.25f);
                mat.SetShaderParameter("distortion", 0.092f);
                mat.SetShaderParameter("glow_intensity", 0f);
                mat.SetShaderParameter("hue_shift", 0f);
                mat.SetShaderParameter("invert_direction", false);
                mat.SetShaderParameter("progress", 0f);
                _sprite.Material = mat;

                var matTween = CreateTween();
                matTween.TweenMethod(
                    Callable.From<float>(t => mat.SetShaderParameter("progress", t)),
                    0f, 1f, duration);
            }

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "modulate:a", 0f, duration * 0.5f);
            tween.TweenProperty(this, "scale", BaseScale * new Vector2(0.1f, 1.2f), duration);
            tween.TweenProperty(_hpBg, "modulate:a", 0f, duration);
            tween.TweenProperty(_hpFill, "modulate:a", 0f, duration);
            tween.TweenProperty(_nameLabel, "modulate:a", 0f, duration);

            tween.Chain().TweenProperty(this, "scale", Vector2.Zero, 0.15f);

            tween.Finished += () =>
            {
                onComplete?.Invoke();
            };
        }
    }
}

using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Floating damage-number overlay. Self-contained: creates its own Label
    /// in _Ready, floats upward with random horizontal drift, fades alpha,
    /// then QueueFrees itself. Static factory methods (SpawnDamage,
    /// SpawnDefence) instantiate and parent it in one call — damage is red,
    /// heal is green, defence is blue. MouseFilter is Ignore so clicks pass
    /// through to cards underneath.
    /// </summary>
    public partial class ShowDamageBehaviour : Control
    {
        private Label _label;

        private float duration = 1.0f;
        private float floatDistance = 80f;
        private float driftRange = 30f;
        private Vector2 startPosition;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            _label = new Label();
            _label.SetAnchorsPreset(LayoutPreset.Center);
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            _label.VerticalAlignment = VerticalAlignment.Center;
            _label.AddThemeFontSizeOverride("font_size", 28);
            _label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
            _label.AddThemeConstantOverride("outline_size", 6);
            AddChild(_label);
        }

        /// <summary>
        /// Creates and parents a floating damage (or heal) number.
        /// Damage is red, heal is green. The label floats upward with
        /// a random horizontal drift and auto-destructs after the
        /// tween completes. Skips values &lt;= 0.
        /// </summary>
        public static void SpawnDamage(int amount, Node parent, Vector2 position, bool isHeal = false)
        {
            if (amount <= 0)
                return;

            var color = isHeal
                ? new Color(0.3f, 1f, 0.3f, 1f)
                : new Color(1f, 0.3f, 0.3f, 1f);

            var hint = new ShowDamageBehaviour();
            parent.AddChild(hint);
            hint.Init(amount, position, color);
        }

        /// <summary>
        /// Creates and parents a floating defence number (blue).
        /// Same floating behaviour as <see cref="SpawnDamage"/>.
        /// Skips values &lt;= 0.
        /// </summary>
        public static void SpawnDefence(int amount, Node parent, Vector2 position)
        {
            if (amount <= 0)
                return;

            var hint = new ShowDamageBehaviour();
            parent.AddChild(hint);
            hint.Init(amount, position, new Color(0.3f, 0.3f, 1f, 1f));
        }

        /// <summary>
        /// Sets the label text, colour, and world position, then starts
        /// the floating animation tween. Called by the static factory
        /// methods after instantiation.
        /// </summary>
        public void Init(int amount, Vector2 worldPosition, Color? color = null)
        {
            _label.Text = amount.ToString();
            if (color.HasValue)
                _label.Modulate = color.Value;

            GlobalPosition = worldPosition;
            startPosition = Position;

            RunAnimation();
        }

        private async void RunAnimation()
        {
            float driftX = (float)GD.RandRange(-driftRange, driftRange);
            Vector2 targetPos = startPosition + new Vector2(driftX, -floatDistance);

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(this, "position", targetPos, duration);

            var fadeTween = CreateTween();
            fadeTween.TweenProperty(_label, "modulate:a", 0f, duration * 0.8f);

            await ToSignal(tween, "finished");
            QueueFree();
        }
    }
}

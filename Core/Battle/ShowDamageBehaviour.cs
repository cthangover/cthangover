using Godot;

namespace Cthangover.Core.Battle
{
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

        public static void SpawnDefence(int amount, Node parent, Vector2 position)
        {
            if (amount <= 0)
                return;

            var hint = new ShowDamageBehaviour();
            parent.AddChild(hint);
            hint.Init(amount, position, new Color(0.3f, 0.3f, 1f, 1f));
        }

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

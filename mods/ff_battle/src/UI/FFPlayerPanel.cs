using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    public partial class FFPlayerPanel : ModWidget
    {
        private const float CELL_SPACING = 20f;

        public List<FFCharacterWidget> Widgets { get; } = new();

        public event System.Action<FFCharacterWidget> OnWidgetClicked;

        protected override void Construct() { }

        public void Init(Character[] players, float scale)
        {
            foreach (var child in Widgets)
                child.QueueFree();
            Widgets.Clear();

            for (int i = 0; i < players.Length; i++)
            {
                var widget = new FFCharacterWidget();
                widget.EnsureConstructed();
                widget.Scale = new Vector2(scale, scale);
                widget.Init(players[i]);
                widget.IsPlayer = true;
                widget.MouseFilter = MouseFilterEnum.Stop;
                widget.GuiInput += (evt) =>
                {
                    if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        OnWidgetClicked?.Invoke(widget);
                };
                AddChild(widget);
                Widgets.Add(widget);

                players[i].Attributes.Health.OnChange += (value, baseValue) =>
                {
                    widget.UpdateInfo();
                    if (value <= 0 && !widget.IsDead)
                    {
                        widget.PlayDeathAnimation(() =>
                        {
                            RemoveWidget(widget);
                            Redraw();
                        });
                    }
                };
            }

            Redraw();
        }

        public void Redraw()
        {
            for (int i = 0; i < Widgets.Count; i++)
            {
                var widget = Widgets[i];
                if (widget == null)
                    continue;

                var effWidth = widget.Size.X * widget.Scale.X;
                var effHeight = widget.Size.Y * widget.Scale.Y;
                widget.Position = new Vector2(i * (effWidth + CELL_SPACING) + CELL_SPACING, Size.Y - effHeight - CELL_SPACING);
            }
        }

		public bool HasAlive()
		{
			return Widgets.Any(w => !w.IsDead && w.Card?.Attributes?.Health?.Value > 0);
		}

		public void ClearAll()
		{
			foreach (var widget in Widgets.ToList())
			{
				if (GodotObject.IsInstanceValid(widget))
					widget.QueueFree();
			}
			Widgets.Clear();
			Redraw();
		}

		public void RemoveWidget(FFCharacterWidget widget)
        {
            Widgets.Remove(widget);
            widget.QueueFree();
            Redraw();
        }
    }
}

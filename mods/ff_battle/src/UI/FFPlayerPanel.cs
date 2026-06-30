using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Panel that arranges player character widgets horizontally at the bottom of
    /// the battle screen. Creates one <see cref="FFCharacterWidget"/> per player
    /// character, wires click and health-change handlers, and positions them using
    /// fixed horizontal spacing via <see cref="Redraw"/>. When a player's HP drops
    /// to zero, the widget plays its death animation and is removed; the remaining
    /// widgets are repositioned. Exposes <see cref="HasAlive"/> for win/loss checks.
    /// </summary>
    public partial class FFPlayerPanel : ModWidget
    {
        private const float CELL_SPACING = 20f;

        /// <summary>All player character widgets, including dead ones until their death animation completes.</summary>
        public List<FFCharacterWidget> Widgets { get; } = new();

        /// <summary>Raised when a player widget is clicked — routed to <see cref="FFBattleCore"/> for character selection.</summary>
        public event System.Action<FFCharacterWidget> OnWidgetClicked;

        protected override void Construct() { }

        /// <summary>
        /// Creates <see cref="FFCharacterWidget"/> instances for each player, scales
        /// them uniformly, wires click and health-change handlers, and calls
        /// <see cref="Redraw"/> to position them horizontally.
        /// </summary>
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

        /// <summary>Repositions all widgets in a horizontal row with fixed <c>CELL_SPACING</c> between them.</summary>
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

        /// <summary>Returns <c>true</c> if any widget is alive and has positive health.</summary>
		public bool HasAlive()
		{
			return Widgets.Any(w => !w.IsDead && w.Card?.Attributes?.Health?.Value > 0);
		}

        /// <summary>Frees all widget instances and clears the widget list. Called on battle cleanup.</summary>
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

        /// <summary>Removes a widget from the panel, frees it, and repositions remaining widgets.</summary>
		public void RemoveWidget(FFCharacterWidget widget)
        {
            Widgets.Remove(widget);
            widget.QueueFree();
            Redraw();
        }
    }
}

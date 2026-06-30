using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Panel that arranges enemy character widgets in a grid layout and handles their
    /// lifecycle. Calculates a uniform scale for all enemy widgets based on the panel
    /// dimensions and enemy count, centering rows horizontally. Listens to each
    /// widget's health change and triggers <see cref="FFCharacterWidget.PlayDeathAnimation"/>
    /// when HP reaches zero, then removes the dead widget and re-grids the survivors.
    /// Raises <see cref="OnEnemyDead"/> so <see cref="FFBattleCore"/> can check win conditions.
    /// </summary>
    public partial class FFEnemyPanel : ModWidget
    {
        private const float BASE_W = 180f;
        private const float BASE_H = 260f;
        private const float MIN_SCALE = 0.25f;
        private const float MAX_SCALE = 1.0f;
        private const float CELL_PADDING = 10f;

        /// <summary>All enemy character widgets currently in the panel, including dead ones until removed.</summary>
        public List<FFCharacterWidget> Widgets { get; } = new();

        /// <summary>Raised when a widget's death animation completes and the widget is removed.</summary>
        public event System.Action<FFCharacterWidget> OnEnemyDead;
        /// <summary>Raised when an enemy widget is clicked (used for target selection).</summary>
        public event System.Action<FFCharacterWidget> OnEnemyClicked;

        protected override void Construct() { }

        /// <summary>Number of enemy widget instances currently managed (including dead).</summary>
        public int EnemyCount => Widgets.Count;

        /// <summary>
        /// Creates <see cref="FFCharacterWidget"/> instances for each enemy, scales
        /// them uniformly, wires click and health-change handlers, and calls
        /// <see cref="GridLayout"/> to arrange them.
        /// </summary>
        public void Init(Character[] enemies, float scale)
        {
            foreach (var child in Widgets)
                child.QueueFree();
            Widgets.Clear();

            for (int i = 0; i < enemies.Length; i++)
            {
                var widget = new FFCharacterWidget();
                widget.EnsureConstructed();
                widget.Scale = new Vector2(scale, scale);
                widget.Init(enemies[i]);
                widget.IsPlayer = false;
                widget.MouseFilter = MouseFilterEnum.Stop;
                widget.GuiInput += (evt) =>
                {
                    if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        OnEnemyClicked?.Invoke(widget);
                };
                AddChild(widget);
                Widgets.Add(widget);

                enemies[i].Attributes.Health.OnChange += (value, baseValue) =>
                {
                    if (value <= 0 && !widget.IsDead)
                        HandleDeath(widget);
                };
            }

            GridLayout();
        }

        /// <summary>
        /// Computes a uniform scale factor so all enemies fit within
        /// <paramref name="panelSize"/> with padding. Uses
        /// <see cref="GetGridDimensions"/> to determine layout, then chooses
        /// the tighter of width-based and height-based scales, clamped to
        /// [<c>MIN_SCALE</c>, <c>MAX_SCALE</c>].
        /// </summary>
        public static float CalculateScale(int enemyCount, Vector2 panelSize)
        {
            var (cols, rows) = GetGridDimensions(enemyCount);

            float availW = panelSize.X - CELL_PADDING * 2;
            float availH = panelSize.Y - CELL_PADDING * 2;

            float scaleByW = (availW - (cols - 1) * CELL_PADDING) / (cols * BASE_W);
            float scaleByH = (availH - (rows - 1) * CELL_PADDING) / (rows * BASE_H);

            float scale = Mathf.Min(scaleByW, scaleByH);
            scale = Mathf.Clamp(scale, MIN_SCALE, MAX_SCALE);

            return scale;
        }

        /// <summary>
        /// Determines grid dimensions for a given count: 1–4 → single row,
        /// 5–10 → 2 rows, 11–16 → 3 rows, 17+ → 4 rows.
        /// </summary>
        public static (int cols, int rows) GetGridDimensions(int count)
        {
            if (count <= 4) return (count, 1);
            if (count <= 10) return ((count + 1) / 2, 2);
            if (count <= 16) return ((count + 2) / 3, 3);
            return ((count + 3) / 4, 4);
        }

        /// <summary>Repositions all widgets into the calculated grid, with optional 0.3s cubic tween animation.</summary>
        public void GridLayout(bool animate = true)
        {
            var (cols, rows) = GetGridDimensions(Widgets.Count);

            for (int i = 0; i < Widgets.Count; i++)
            {
                var widget = Widgets[i];
                if (widget == null)
                    continue;

                int row = i / cols;
                int col = i % cols;

                var totalColsInRow = row < rows - 1 ? cols : (Widgets.Count - row * cols);
                if (totalColsInRow <= 0)
                    totalColsInRow = 1;

                var effW = widget.Size.X * widget.Scale.X;
                var effH = widget.Size.Y * widget.Scale.Y;

                float startX = (Size.X - (totalColsInRow * effW + (totalColsInRow - 1) * CELL_PADDING)) / 2f;
                float startY = CELL_PADDING;

                var targetPos = new Vector2(
                    startX + col * (effW + CELL_PADDING),
                    startY + row * (effH + CELL_PADDING)
                );

                if (animate)
                {
                    var tween = CreateTween();
                    tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                    tween.TweenProperty(widget, "position", targetPos, 0.3f);
                }
                else
                {
                    widget.Position = targetPos;
                }
            }
        }

        private void HandleDeath(FFCharacterWidget widget)
        {
            if (widget.IsDead)
                return;

            widget.PlayDeathAnimation(() =>
            {
                RemoveWidget(widget);
                GridLayout(true);
                OnEnemyDead?.Invoke(widget);
            });
        }

        /// <summary>Removes a widget from the panel, frees it, and re-grids the remaining widgets.</summary>
        public void RemoveWidget(FFCharacterWidget widget)
        {
            Widgets.Remove(widget);
            widget.QueueFree();
            GridLayout();
        }

        /// <summary>Returns <c>true</c> if any widget in the panel is not marked dead.</summary>
        public bool HasAlive()
        {
            foreach (var w in Widgets)
                if (!w.IsDead)
                    return true;
            return false;
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
            GridLayout(false);
        }
    }
}

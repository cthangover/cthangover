using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Characters;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    public partial class FFEnemyPanel : ModWidget
    {
        private const float BASE_W = 180f;
        private const float BASE_H = 260f;
        private const float MIN_SCALE = 0.25f;
        private const float MAX_SCALE = 1.0f;
        private const float CELL_PADDING = 10f;

        public List<FFCharacterWidget> Widgets { get; } = new();

        public event System.Action<FFCharacterWidget> OnEnemyDead;
        public event System.Action<FFCharacterWidget> OnEnemyClicked;

        protected override void Construct() { }

        public int EnemyCount => Widgets.Count;

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

        public static (int cols, int rows) GetGridDimensions(int count)
        {
            if (count <= 4) return (count, 1);
            if (count <= 10) return ((count + 1) / 2, 2);
            if (count <= 16) return ((count + 2) / 3, 3);
            return ((count + 3) / 4, 4);
        }

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

        public void RemoveWidget(FFCharacterWidget widget)
        {
            Widgets.Remove(widget);
            widget.QueueFree();
            GridLayout();
        }

        public bool HasAlive()
        {
            foreach (var w in Widgets)
                if (!w.IsDead)
                    return true;
            return false;
        }

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

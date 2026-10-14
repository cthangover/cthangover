using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
namespace Mods.ModManager
{
    /// <summary>
    /// Transparent overlay that paints the scheme's dependency lines above the
    /// group cards, the Godot counterpart of the mockup's SVG layer. Drawing is
    /// connected through the <c>"draw"</c> signal rather than a <c>_Draw</c>
    /// override, because virtual overrides are unreliable in mod assemblies.
    /// <para/>
    /// The layer stores only edge descriptors and chip references; coordinates
    /// are read from the chips' global rectangles at paint time, so the canvas
    /// never has to update line geometry — it only calls <see cref="QueueRedraw"/>
    /// after a drag or resize.
    /// </summary>
    public partial class CollectionEdgeLayer : ModWidget
    {
        private readonly List<CollectionEdge> _edges = new();
        private readonly Dictionary<string, CollectionModChip> _chips = new();
        private Control _canvas;

        /// <summary>Replaces the drawn edges. <paramref name="canvas"/> anchors the local coordinate space.</summary>
        public void SetEdges(IEnumerable<CollectionEdge> edges, Dictionary<string, CollectionModChip> chips, Control canvas)
        {
            EnsureConstructed();
            _canvas = canvas;
            _edges.Clear();
            _chips.Clear();

            if (edges != null)
            {
                foreach (var edge in edges)
                    _edges.Add(edge);
            }

            if (chips != null)
            {
                foreach (var pair in chips)
                    _chips[pair.Key] = pair.Value;
            }

            QueueRedraw();
        }

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            Connect("draw", Callable.From((Action)DrawAll));
        }

        private void DrawAll()
        {
            if (_canvas == null || !GodotObject.IsInstanceValid(_canvas))
                return;

            var origin = _canvas.GetGlobalRect().Position;

            foreach (var edge in _edges)
            {
                if (!_chips.TryGetValue(edge.FromModId, out var from) || !GodotObject.IsInstanceValid(from))
                    continue;
                if (!_chips.TryGetValue(edge.ToModId, out var to) || !GodotObject.IsInstanceValid(to))
                    continue;

                var a = from.GetGlobalRect().GetCenter() - origin;
                var b = to.GetGlobalRect().GetCenter() - origin;
                var points = BuildCurve(a, b);
                var color = edge.Cross ? (edge.Active ? UiTokens.Danger : UiTokens.Warning) : (edge.Active ? UiTokens.Accent : UiTokens.LineStrong);
                var width = edge.Cross ? (edge.Active ? 3f : 2f) : (edge.Active ? 2.5f : 1.5f);

                if (edge.Cross)
                {
                    for (var i = 0; i < points.Count - 1; i++)
                        DrawDashedLine(points[i], points[i + 1], color, width, 5f);
                    DrawCircle(points[^1], 2.5f, color);
                }
                else
                {
                    DrawPolyline(points.ToArray(), color, width, true);
                }
            }
        }

        private static List<Vector2> BuildCurve(Vector2 a, Vector2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            Vector2 c1, c2;

            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            {
                c1 = new Vector2(a.X + dx * 0.4f, a.Y);
                c2 = new Vector2(b.X - dx * 0.4f, b.Y);
            }
            else
            {
                c1 = new Vector2(a.X, a.Y + dy * 0.4f);
                c2 = new Vector2(b.X, b.Y - dy * 0.4f);
            }

            var points = new List<Vector2>();
            const int steps = 24;
            for (var i = 0; i <= steps; i++)
                points.Add(Cubic(a, c1, c2, b, i / (float)steps));
            return points;
        }

        private static Vector2 Cubic(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
        }
    }
}

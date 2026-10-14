using System.Collections.Generic;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Invisible interactive layer of the world map that draws every zone
    /// polygon and reports pointer activity through signals. Polygons arrive as
    /// normalised [0..1] point lists — the same data <see cref="MapZone"/> reads
    /// from <c>mods/&lt;mod&gt;/map/*.json</c> — and are scaled by the layer's
    /// own size, which keeps the map resolution-independent and makes the
    /// polygon a much larger click target than the tiny marker icon.
    /// <para/>
    /// The layer never selects anything by itself: <see cref="MapWidget"/> owns
    /// the selected zone and controls the outline through <see cref="SetSelected"/>.
    /// Hover is deliberately split in two: <see cref="SetHovered"/> is the
    /// cross-highlight entry point used when the pointer is over the paired
    /// marker, while pointer motion inside the layer emits
    /// <see cref="ZoneHoveredEventHandler"/>. Because the setter does not emit,
    /// marker and polygon can highlight each other without signal loops.
    /// <para/>
    /// All painting is delegated to <see cref="MapZoneStyle"/>, the single
    /// style definition shared with the map editor canvas, so the game and the
    /// editor render zones identically. Closed zones stay selectable (the
    /// location card explains the block): they use the dimmed locked hatch, and
    /// unlike before the selection keyline is still drawn for them, so a picked
    /// locked zone is visible too.
    /// </summary>
    public partial class MapZonesLayer : Control
    {
        [Signal] public delegate void ZoneClickedEventHandler(string zoneId);
        [Signal] public delegate void ZoneHoveredEventHandler(string zoneId, bool hovered);

        private sealed class ZoneShape
        {
            /// <summary>Zone id this shape highlights on hover and reports on click.</summary>
            public string ZoneId;
            /// <summary>Polygon vertices scaled to layer pixels at draw time.</summary>
            public Vector2[] Points;
            /// <summary>True when the zone's effective open state is closed; switches the hatch to the dimmed locked style.</summary>
            public bool Locked;
        }

        private readonly List<ZoneShape> _shapes = new();
        private string _selectedId;
        private string _hoverId;

        /// <summary>Prepares the canvas for hatch painting and subscribes pointer and resize handlers.</summary>
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            MapZoneStyle.Prepare(this);
            GuiInput += OnGuiInput;
            MouseExited += OnMouseExited;
            Resized += OnResized;
        }

        /// <summary>Drops every registered polygon; call before rebuilding the zone set.</summary>
        public void ClearZones()
        {
            _shapes.Clear();
            _selectedId = null;
            _hoverId = null;
            QueueRedraw();
        }

        /// <summary>
        /// Registers one normalised polygon. Shapes with fewer than three points
        /// have no interior and are skipped — such a zone can still be reached
        /// through its marker.
        /// </summary>
        public void AddZone(string zoneId, IReadOnlyList<Vector2> normalizedPoints, bool locked)
        {
            if (string.IsNullOrEmpty(zoneId) || normalizedPoints == null || normalizedPoints.Count < 3)
                return;

            var points = new Vector2[normalizedPoints.Count];
            for (var i = 0; i < points.Length; i++)
                points[i] = normalizedPoints[i];

            _shapes.Add(new ZoneShape { ZoneId = zoneId, Points = points, Locked = locked });
            QueueRedraw();
        }

        /// <summary>Updates the closed state of a registered zone, e.g. after the active scene changed.</summary>
        public void SetLocked(string zoneId, bool locked)
        {
            foreach (var shape in _shapes)
            {
                if (shape.ZoneId != zoneId)
                    continue;
                shape.Locked = locked;
                QueueRedraw();
                return;
            }
        }

        /// <summary>Marks the zone as selected; its outline is painted above every other polygon.</summary>
        public void SetSelected(string zoneId)
        {
            _selectedId = zoneId;
            QueueRedraw();
        }

        /// <summary>
        /// Applies a hover state coming from the paired marker. Does not emit
        /// <see cref="ZoneHoveredEventHandler"/> on purpose, so the marker's
        /// hover and the polygon's hover never bounce off each other.
        /// </summary>
        public void SetHovered(string zoneId, bool hovered)
        {
            if (hovered)
            {
                if (_hoverId == zoneId)
                    return;
                _hoverId = zoneId;
            }
            else
            {
                if (_hoverId != zoneId)
                    return;
                _hoverId = null;
            }
            QueueRedraw();
        }

        /// <summary>Paints every zone polygon, redrawing the selected one last so its keyline sits on top.</summary>
        public override void _Draw()
        {
            if (_shapes.Count == 0)
                return;

            ZoneShape selected = null;

            foreach (var shape in _shapes)
            {
                if (shape.ZoneId == _selectedId)
                {
                    selected = shape;
                    continue;
                }
                DrawShape(shape, Size);
            }

            if (selected != null)
                DrawShape(selected, Size);
        }

        /// <summary>
        /// Paints one zone polygon through <see cref="MapZoneStyle"/>.
        /// Polygons that cannot be triangulated — a self-intersecting outline
        /// or a degenerate sliver produced while authoring — skip the fill
        /// entirely, because Godot's textured polygon draw reports a native
        /// triangulation error for them on every frame; the outline is still
        /// drawn so the author can see and repair the shape.
        /// </summary>
        private void DrawShape(ZoneShape shape, Vector2 size)
        {
            MapZoneStyle.Draw(this, ScaledPolygon(shape, size), shape.ZoneId == _selectedId, shape.ZoneId == _hoverId, shape.Locked);
        }

        private void OnGuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseMotion:
                    ApplyPointer(HitTest(GetLocalMousePosition()));
                    break;
                case InputEventMouseButton button when button.Pressed && button.ButtonIndex == MouseButton.Left:
                    var shape = HitTest(button.Position);
                    if (shape == null)
                        return;
                    EmitSignal(SignalName.ZoneClicked, shape.ZoneId);
                    AcceptEvent();
                    break;
            }
        }

        private void OnMouseExited()
        {
            ApplyPointer(null);
        }

        private void OnResized()
        {
            QueueRedraw();
        }

        /// <summary>Resolves the topmost zone under a layer-local point; polygons later in the list win overlaps.</summary>
        private ZoneShape HitTest(Vector2 point)
        {
            for (var i = _shapes.Count - 1; i >= 0; i--)
            {
                var shape = _shapes[i];
                if (Geometry2D.IsPointInPolygon(point, ScaledPolygon(shape, Size)))
                    return shape;
            }
            return null;
        }

        private void ApplyPointer(ZoneShape shape)
        {
            var nextId = shape?.ZoneId;
            var nextCursor = shape == null ? CursorShape.Arrow : CursorShape.PointingHand;

            if (MouseDefaultCursorShape != nextCursor)
                MouseDefaultCursorShape = nextCursor;

            if (_hoverId == nextId)
                return;

            var previous = _hoverId;
            _hoverId = nextId;
            QueueRedraw();

            if (!string.IsNullOrEmpty(previous))
                EmitSignal(SignalName.ZoneHovered, previous, false);
            if (!string.IsNullOrEmpty(nextId))
                EmitSignal(SignalName.ZoneHovered, nextId, true);
        }

        private static Vector2[] ScaledPolygon(ZoneShape shape, Vector2 size)
        {
            var result = new Vector2[shape.Points.Length];
            for (var i = 0; i < result.Length; i++)
                result[i] = shape.Points[i] * size;
            return result;
        }
    }
}

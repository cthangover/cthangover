using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Single source of truth for world map zone visuals, shared by the live
    /// map (<see cref="MapZonesLayer"/>, which also backs the map editor's
    /// game-like preview) and the map editor's editing canvas. The editor and
    /// the game read the same colours, pattern and outline parameters here, so
    /// the two views cannot drift apart when the style is tuned.
    /// <para/>
    /// Zone readability must not depend on the backdrop: a zone lying on a
    /// same-hue patch of the map image still has to be visible, and colour
    /// inversion is not an option. The style therefore avoids flat washes of a
    /// single hue:
    /// <list type="bullet">
    /// <item>the fill is a diagonal hatch whose stripes alternate between a
    /// light and a dark tone, modulated by the state colour — on any backdrop
    /// at least one stripe keeps its contrast, and the semi-transparent gaps
    /// let the map texture stay readable;</item>
    /// <item>hover and selection are outlined with a two-tone dashed keyline:
    /// a solid dark polyline with light dashes stamped on top, producing the
    /// classic marching-ants alternation that stays visible on both bright and
    /// dark map areas.</item>
    /// </list>
    /// State only changes the hatch modulation and the outline weight, so the
    /// visual language is identical across normal, hover, selected and locked
    /// zones. The hatch is a tiny repeating texture stamped through polygon
    /// UVs rather than CPU line clipping, because the editor redraws on every
    /// drag frame and geometry clipping per frame would be wasteful.
    /// </summary>
    public static class MapZoneStyle
    {
        /// <summary>Side of the repeating hatch tile in pixels.</summary>
        public const int HatchTile = 12;

        /// <summary>Thickness of one hatch stripe in pixels; the dark stripe follows the light one immediately.</summary>
        public const int HatchStripe = 2;

        private static readonly Color HatchLightPixel = new(1f, 1f, 1f, 1f);
        private static readonly Color HatchDarkPixel = new(0.1f, 0.1f, 0.13f, 1f);

        private static readonly Color NormalFill = UiTokens.Alpha(UiTokens.White, 0.32f);
        private static readonly Color HoverFill = UiTokens.Alpha(UiTokens.Accent, 0.5f);
        private static readonly Color SelectedFill = UiTokens.Alpha(UiTokens.AccentBright, 0.7f);
        private static readonly Color LockedFill = UiTokens.Alpha(UiTokens.White, 0.14f);

        private static readonly Color OutlineDark = new(0.03f, 0.03f, 0.05f, 0.9f);
        private static readonly Color OutlineLight = new(0.96f, 0.94f, 0.9f, 0.95f);
        private static readonly Color OutlineDarkSoft = new(0.03f, 0.03f, 0.05f, 0.65f);
        private static readonly Color OutlineLightSoft = new(0.96f, 0.94f, 0.9f, 0.8f);

        private const float HoverOutlineWidth = 1.6f;
        private const float SelectedOutlineWidth = 2.6f;
        private const float OutlineDash = 7f;

        /// <summary>Handle colour of a polygon vertex on the editing canvas.</summary>
        public static readonly Color VertexHandle = UiTokens.AccentBright;

        /// <summary>Handle colour of a marker position on the editing canvas.</summary>
        public static readonly Color MarkerHandle = UiTokens.TypePassive;

        /// <summary>Handle colour of a label position on the editing canvas.</summary>
        public static readonly Color LabelHandle = UiTokens.AccentDim;

        private static Texture2D _hatchTexture;

        /// <summary>
        /// Enables texture repeat and filtering needed by the hatch fills. Call
        /// once from the drawing control (the map zones layer and the editing
        /// canvas both do it when they enter the tree).
        /// </summary>
        public static void Prepare(CanvasItem canvas)
        {
            canvas.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
            canvas.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        }

        /// <summary>
        /// Draws one zone polygon in pixel coordinates with the shared style.
        /// Polygons that cannot be triangulated (a self-intersecting outline
        /// produced while authoring) skip the hatch fill, so Godot does not
        /// report a native triangulation error on every frame; the keyline is
        /// still drawn for the selected and hovered states, which is enough
        /// for the author to find and repair the shape.
        /// </summary>
        public static void Draw(CanvasItem canvas, Vector2[] polygon, bool selected, bool hovered, bool locked)
        {
            if (canvas == null || polygon == null || polygon.Length < 2)
                return;

            var fill = locked ? LockedFill : selected ? SelectedFill : hovered ? HoverFill : NormalFill;
            if (polygon.Length >= 3 && Geometry2D.TriangulatePolygon(polygon).Length > 0)
                DrawHatch(canvas, polygon, fill);

            if (selected)
                DrawDashedKeyline(canvas, polygon, OutlineDark, OutlineLight, SelectedOutlineWidth);
            else if (hovered && !locked)
                DrawDashedKeyline(canvas, polygon, OutlineDarkSoft, OutlineLightSoft, HoverOutlineWidth);
        }

        /// <summary>
        /// Colour of the repeating hatch pattern at a pixel offset: a 45°
        /// diagonal where the light stripe is immediately followed by the dark
        /// one. Kept pure (no Godot resources) so the two guarantees the whole
        /// style rests on — the pattern tiles and every tile carries both a
        /// light and a dark stripe — are unit tested without a rendering
        /// context.
        /// </summary>
        public static Color HatchPixel(int x, int y)
        {
            var band = (x + y) % HatchTile;
            if (band < 0)
                band += HatchTile;

            if (band < HatchStripe)
                return HatchLightPixel;
            if (band < HatchStripe * 2)
                return HatchDarkPixel;
            return new Color(0f, 0f, 0f, 0f);
        }

        private static void DrawHatch(CanvasItem canvas, Vector2[] polygon, Color modulate)
        {
            var uvs = new Vector2[polygon.Length];
            for (var i = 0; i < polygon.Length; i++)
                uvs[i] = polygon[i] / HatchTile;

            canvas.DrawColoredPolygon(polygon, modulate, uvs, HatchTexture);
        }

        /// <summary>
        /// Two-tone dashed keyline: the dark line is drawn solid first and the
        /// light dashes — whose gaps equal their length — are stamped on top,
        /// so the perimeter alternates dark and light and one of the two
        /// always contrasts with the map underneath.
        /// </summary>
        private static void DrawDashedKeyline(CanvasItem canvas, Vector2[] polygon, Color dark, Color light, float width)
        {
            var loop = new Vector2[polygon.Length + 1];
            polygon.CopyTo(loop, 0);
            loop[polygon.Length] = polygon[0];
            canvas.DrawPolyline(loop, dark, width, true);

            for (var i = 0; i < polygon.Length; i++)
                canvas.DrawDashedLine(polygon[i], loop[i + 1], light, width, OutlineDash, true, true);
        }

        private static Texture2D HatchTexture => _hatchTexture ??= BuildHatchTexture();

        private static Texture2D BuildHatchTexture()
        {
            var image = Image.CreateEmpty(HatchTile, HatchTile, false, Image.Format.Rgba8);
            for (var y = 0; y < HatchTile; y++)
            {
                for (var x = 0; x < HatchTile; x++)
                    image.SetPixel(x, y, HatchPixel(x, y));
            }

            return ImageTexture.CreateFromImage(image);
        }
    }
}

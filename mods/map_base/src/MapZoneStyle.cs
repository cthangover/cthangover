using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Single source of truth for world map zone visuals, shared by the live
    /// map (<see cref="MapZonesLayer"/>, which also backs the map editor's
    /// game-like preview) and the map editor's editing canvas. The editor and
    /// the game read the same colours, pattern and outline parameters here, so
    /// the two views cannot drift apart when the style is tuned.
    /// <para/>
    /// Zone visuals are built from a two-tone accent pair — gold with orange
    /// as its partner — instead of flat washes of a single hue, so a zone reads
    /// as an area while staying in the gold UI family:
    /// <list type="bullet">
    /// <item>the fill is a diagonal hatch whose stripes alternate gold and
    /// orange, stamped through polygon UVs at a low state-dependent alpha so
    /// the map texture stays readable through the gaps;</item>
    /// <item>hover and selection use a two-tone dashed keyline: a solid orange
    /// polyline with gold dashes stamped on top, the classic marching-ants
    /// alternation repeated in the same accent pair.</item>
    /// </list>
    /// State only changes the hatch opacity and the outline weight, so the
    /// visual language is identical across normal, hover, selected and locked
    /// zones; the editor additionally uses the muted hidden state for zones the
    /// game would omit. The hatch is a tiny repeating texture stamped through
    /// polygon UVs rather than CPU line clipping, because the editor redraws on
    /// every drag frame and geometry clipping per frame would be wasteful.
    /// </summary>
    public static class MapZoneStyle
    {
        /// <summary>Side of the repeating hatch tile in pixels.</summary>
        public const int HatchTile = 12;

        /// <summary>Thickness of one hatch stripe in pixels; the orange stripe follows the gold one immediately.</summary>
        public const int HatchStripe = 2;

        /// <summary>Gold half of the zone accent pair, used for hatch stripes and keyline dashes.</summary>
        public static readonly Color Gold = UiTokens.AccentBright;

        /// <summary>
        /// Orange half of the zone accent pair, the deeper tone of the hatch
        /// and the keyline underlay. Deliberately close to <see cref="Gold"/>
        /// (the <c>TypeUsed</c> token) so the hatch reads as one warm texture
        /// instead of two fighting colours.
        /// </summary>
        public static readonly Color Orange = UiTokens.TypeUsed;

        private static readonly Color NormalFill = UiTokens.Alpha(UiTokens.White, 0.4f);
        private static readonly Color HoverFill = UiTokens.Alpha(UiTokens.White, 0.6f);
        private static readonly Color SelectedFill = UiTokens.Alpha(UiTokens.White, 0.8f);
        private static readonly Color LockedFill = UiTokens.Alpha(UiTokens.White, 0.18f);
        private static readonly Color HiddenFill = UiTokens.Alpha(UiTokens.Faint, 0.8f);

        private static readonly Color OutlineSolid = UiTokens.Alpha(Orange, 0.95f);
        private static readonly Color OutlineDash = UiTokens.Alpha(Gold, 0.95f);
        private static readonly Color OutlineSolidSoft = UiTokens.Alpha(Orange, 0.6f);
        private static readonly Color OutlineDashSoft = UiTokens.Alpha(Gold, 0.7f);

        private const float HoverOutlineWidth = 1.6f;
        private const float SelectedOutlineWidth = 2.6f;
        private const float OutlineDashLength = 7f;

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
        /// <para/>
        /// <paramref name="hidden"/> is only passed by the editing canvas: the
        /// game and the preview omit hidden zones entirely, but the author
        /// still has to see and edit them, so they are painted with the muted
        /// hidden hatch and a soft keyline instead of disappearing.
        /// </summary>
        public static void Draw(CanvasItem canvas, Vector2[] polygon, bool selected, bool hovered, bool locked, bool hidden = false)
        {
            if (canvas == null || polygon == null || polygon.Length < 2)
                return;

            var fill = hidden ? HiddenFill : locked ? LockedFill : selected ? SelectedFill : hovered ? HoverFill : NormalFill;
            if (polygon.Length >= 3 && Geometry2D.TriangulatePolygon(polygon).Length > 0)
                DrawHatch(canvas, polygon, fill);

            if (selected)
                DrawDashedKeyline(canvas, polygon, OutlineSolid, OutlineDash, SelectedOutlineWidth);
            else if (hidden || (hovered && !locked))
                DrawDashedKeyline(canvas, polygon, OutlineSolidSoft, OutlineDashSoft, HoverOutlineWidth);
        }

        /// <summary>
        /// Colour of the repeating hatch pattern at a pixel offset: a 45°
        /// diagonal where the gold stripe is immediately followed by the orange
        /// one. Kept pure (no Godot resources) so the two guarantees the whole
        /// style rests on — the pattern tiles and every tile carries both
        /// accent colours — are unit tested without a rendering context.
        /// </summary>
        public static Color HatchPixel(int x, int y)
        {
            var band = (x + y) % HatchTile;
            if (band < 0)
                band += HatchTile;

            if (band < HatchStripe)
                return Gold;
            if (band < HatchStripe * 2)
                return Orange;
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
        /// Two-tone dashed keyline: the orange line is drawn solid first and
        /// the gold dashes — whose gaps equal their length — are stamped on
        /// top, so the perimeter alternates the two accent colours like
        /// marching ants.
        /// </summary>
        private static void DrawDashedKeyline(CanvasItem canvas, Vector2[] polygon, Color solid, Color dash, float width)
        {
            var loop = new Vector2[polygon.Length + 1];
            polygon.CopyTo(loop, 0);
            loop[polygon.Length] = polygon[0];
            canvas.DrawPolyline(loop, solid, width, true);

            for (var i = 0; i < polygon.Length; i++)
                canvas.DrawDashedLine(polygon[i], loop[i + 1], dash, width, OutlineDashLength, true, true);
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

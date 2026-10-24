using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Settings;
using Cthangover.Mods.Time;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Light of a mounted map. One <c>rpg_lit</c> material is shared by the tile layers and
    /// every actor (their sprites use the parent's material), so the whole map is lit by the
    /// same ambient and the same lights.
    /// <para/>
    /// The ambient follows the game clock with the phases and cross-fades of the novel
    /// backgrounds (<see cref="UiLightController.CalculateTimeWeights"/>; dark mode forces night,
    /// the light switch off keeps day), unless the map fixes its own (<see cref="RpgMap.Ambient"/>,
    /// a lit room). The lights are the player's own glow, the tiles whose tileset entry has a
    /// <see cref="RpgLight"/> (lamps and windows light every map they are drawn on, collected
    /// once per map) and the shown events with a light on their active page. The nearest to the
    /// player win, at most <see cref="MaxLights"/>. They are pushed every frame in canvas
    /// pixels, because the view moves with the player and actors walk.
    /// </summary>
    public sealed class RpgLighting
    {
        /// <summary>Lights the shader takes (its arrays' size).</summary>
        public const int MaxLights = 16;

        /// <summary>Reach of the player's glow in cells.</summary>
        public const float PlayerRadius = 3.5f;

        private static readonly Color PlayerColor = new(1f, 0.9f, 0.75f, 0.75f);
        private static readonly Vector4 Day = new(0, 1, 0, 0);
        private static readonly Vector4 Night = new(0, 0, 0, 1);

        private readonly Vector2[] positions = new Vector2[MaxLights];
        private readonly Color[] colors = new Color[MaxLights];
        private readonly float[] radii = new float[MaxLights];
        private readonly List<(Vector2I Cell, RpgLight Light)> tileLights = new();
        private readonly List<(Vector2 Centre, RpgLight Light)> candidates = new();

        /// <summary>The shared material; null when the shader is missing (the map is drawn unlit).</summary>
        public ShaderMaterial Material { get; }

        /// <summary>Creates the material for <paramref name="map"/>, applies its fixed ambient and collects the lit tiles.</summary>
        public RpgLighting(RpgMap map, RpgMapGrid grid, RpgTileset tileset)
        {
            if (tileset != null)
                tileLights.AddRange(TileLights(grid, tileset));

            var shader = Shaders.Resolve("rpg_lit");
            if (shader == null)
                return;
            Material = new ShaderMaterial { Shader = shader };
            var ambient = !string.IsNullOrEmpty(map.Ambient) && Color.HtmlIsValid(map.Ambient);
            Material.SetShaderParameter("use_ambient", ambient);
            if (ambient)
                Material.SetShaderParameter("ambient_color", Color.FromHtml(map.Ambient));
        }

        /// <summary>
        /// The ambient weights for an hour 0–24: night in dark mode, day while lighting is
        /// switched off, otherwise the phase blend of the time shaders.
        /// </summary>
        public static Vector4 Weights(float hour, bool darkMode, bool useLight)
        {
            if (darkMode)
                return Night;
            if (!useLight)
                return Day;
            return UiLightController.CalculateTimeWeights(hour);
        }

        /// <summary>Cells of <paramref name="grid"/> whose tile (on any layer) gives off light, with that light.</summary>
        public static List<(Vector2I Cell, RpgLight Light)> TileLights(RpgMapGrid grid, RpgTileset tileset)
        {
            var result = new List<(Vector2I, RpgLight)>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    foreach (var layer in new[] { RpgLayer.Back, RpgLayer.Main, RpgLayer.Front })
                    {
                        var light = tileset.LightOf(grid.GetTile(layer, x, y));
                        if (light == null)
                            continue;
                        result.Add((new Vector2I(x, y), light));
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>Pushes the ambient of the current hour and the lights of <paramref name="view"/>.</summary>
        public void Update(RpgWorldView view, IEnumerable<RpgActor> events)
        {
            if (Material == null || view.Player == null)
                return;

            var controller = UiLightController.Instance;
            var hour = GameData.Instance?.Runtime?.Time?.Normalized ?? 12f;
            Material.SetShaderParameter("time_weights", Weights(hour, controller?.IsDarkMode == true, controller?.IsUseLight != false));

            var half = new Vector2(view.TileSize, view.TileSize) / 2f;
            candidates.Clear();
            foreach (var (cell, light) in tileLights)
                candidates.Add((new Vector2(cell.X, cell.Y) * view.TileSize + half, light));
            foreach (var actor in events)
            {
                if (actor.Visible && actor.Current?.Light != null)
                    candidates.Add((actor.Position + half, actor.Current.Light));
            }

            var cellPixels = view.TileSize * view.GlobalScale.X;
            var player = view.Player.Position + half;
            positions[0] = view.ToGlobal(player);
            colors[0] = PlayerColor;
            radii[0] = PlayerRadius * cellPixels;
            var count = 1;
            foreach (var (centre, light) in candidates.OrderBy(c => c.Centre.DistanceSquaredTo(player)))
            {
                if (count >= MaxLights)
                    break;
                positions[count] = view.ToGlobal(centre);
                colors[count] = Color.HtmlIsValid(light.Color ?? "") ? Color.FromHtml(light.Color) : Colors.White;
                radii[count] = light.Radius * cellPixels;
                count++;
            }

            Material.SetShaderParameter("light_count", count);
            Material.SetShaderParameter("light_positions", positions);
            Material.SetShaderParameter("light_colors", colors);
            Material.SetShaderParameter("light_radii", radii);
        }
    }
}

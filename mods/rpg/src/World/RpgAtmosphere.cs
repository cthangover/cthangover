namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using Godot;

    /// <summary>
    /// Opt-in woodland motion in map coordinates: drifting leaves, fireflies and small
    /// water ripples. Positions are deterministic; nothing enters the save or collision
    /// model. The small fixed draw budget is independent of map dimensions.
    /// </summary>
    public partial class RpgAtmosphere : Node2D
    {
        private readonly Vector2 extent;
        private readonly List<Vector2> water = new();
        private readonly int tileSize;
        private double elapsed;
        private double redraw;

        /// <summary>Collects water cells once; an empty Atmosphere leaves this node unmounted.</summary>
        public RpgAtmosphere(RpgMap map, RpgMapGrid grid, int tileSize, RpgTileset tileset)
        {
            Name = "Atmosphere";
            this.tileSize = tileSize;
            extent = new Vector2(grid.Width, grid.Height) * tileSize;
            var waterTiles = new HashSet<int>();
            foreach (var tile in tileset?.Tiles ?? new List<RpgTileInfo>())
                if (string.Equals(tile.Tag, "water", StringComparison.OrdinalIgnoreCase))
                    waterTiles.Add(tile.Index);
            for (var y = 0; y < grid.Height; y++)
                for (var x = 0; x < grid.Width; x++)
                    if (waterTiles.Contains(grid.GetTile(RpgLayer.Back, x, y)))
                        water.Add(new Vector2(x, y) * tileSize);
        }

        /// <summary>Updates at 20 Hz, retaining crisp pixel positions and avoiding a particle system per tile.</summary>
        public override void _Process(double delta)
        {
            elapsed += delta;
            redraw += delta;
            if (redraw < 0.05)
                return;
            redraw = 0;
            QueueRedraw();
        }

        /// <summary>Draws small translucent accents; the ground and actor sprites retain their own palette.</summary>
        public override void _Draw()
        {
            var t = (float)elapsed;
            for (var i = 0; i < 36; i++)
            {
                var x = Mathf.PosMod(i * 137.3f + t * (2 + i % 3), extent.X);
                var y = Mathf.PosMod(i * 83.7f + t * 1.4f + Mathf.Sin(t * 0.4f + i) * 7, extent.Y);
                var p = new Vector2(Mathf.Round(x), Mathf.Round(y));
                if (i % 3 == 0)
                {
                    var alpha = 0.15f + 0.45f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 1.2f + i)), 2);
                    DrawCircle(p, 3, new Color(0.72f, 0.91f, 0.5f, alpha * 0.12f));
                    DrawRect(new Rect2(p, Vector2.One), new Color(0.95f, 1f, 0.65f, alpha));
                }
                else
                    DrawLine(p, p + new Vector2(2, i % 2 == 0 ? 1 : -1), new Color(0.77f, 0.68f, 0.34f, 0.32f));
            }
            for (var i = 0; i < water.Count; i += 2)
            {
                var phase = t * 0.6f + i * 1.7f;
                var p = water[i] + new Vector2(8 + Mathf.Sin(phase) * 4, 10 + i % 13);
                var alpha = 0.08f + 0.12f * Mathf.Max(0, Mathf.Sin(phase));
                DrawLine(p.Round(), (p + new Vector2(tileSize * 0.25f, 0)).Round(), new Color(0.63f, 0.87f, 0.84f, alpha));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Parsed cells of an <see cref="RpgMap"/>: the three tile layers as atlas
    /// indices (-1 = empty) and the collision codes. The map file keeps rows as
    /// text (<see cref="RpgMapLayers"/>); this is the form the world and the
    /// editor work with, and <see cref="WriteTo"/> turns it back into rows.
    /// <para/>
    /// Parsing is forgiving, because maps are edited by hand: short rows and
    /// missing rows are empty cells, extra cells are cut off, an unreadable token
    /// is empty. The grid always has exactly the map's declared size.
    /// </summary>
    public class RpgMapGrid
    {
        private const string EmptyToken = ".";

        private int[][] layers;
        private char[] collision;

        /// <summary>Columns.</summary>
        public int Width { get; private set; }

        /// <summary>Rows.</summary>
        public int Height { get; private set; }

        /// <summary>Creates an empty grid: no tiles, collision from tiles everywhere.</summary>
        public RpgMapGrid(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            layers = new int[3][];
            for (var i = 0; i < layers.Length; i++)
                layers[i] = Enumerable.Repeat(-1, Width * Height).ToArray();
            collision = Enumerable.Repeat(RpgCollision.Auto, Width * Height).ToArray();
        }

        /// <summary>Reads the layers and the collision rows of <paramref name="map"/>.</summary>
        public static RpgMapGrid Parse(RpgMap map)
        {
            var grid = new RpgMapGrid(map?.Width ?? 1, map?.Height ?? 1);
            if (map == null)
                return grid;

            grid.ReadLayer(RpgLayer.Back, map.Layers?.Back);
            grid.ReadLayer(RpgLayer.Main, map.Layers?.Main);
            grid.ReadLayer(RpgLayer.Front, map.Layers?.Front);

            var rows = map.Collision ?? new List<string>();
            for (var y = 0; y < grid.Height && y < rows.Count; y++)
            {
                var row = rows[y] ?? string.Empty;
                for (var x = 0; x < grid.Width && x < row.Length; x++)
                    grid.SetCollision(x, y, row[x]);
            }
            return grid;
        }

        /// <summary>True when the cell lies on the map.</summary>
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Atlas index on a layer, -1 for empty or outside the map.</summary>
        public int GetTile(RpgLayer layer, int x, int y) => InBounds(x, y) ? layers[(int)layer][y * Width + x] : -1;

        /// <summary>Puts an atlas index (-1 clears); outside the map it does nothing.</summary>
        public void SetTile(RpgLayer layer, int x, int y, int index)
        {
            if (InBounds(x, y))
                layers[(int)layer][y * Width + x] = index < 0 ? -1 : index;
        }

        /// <summary>Collision code of a cell; outside the map it is <see cref="RpgCollision.Block"/>.</summary>
        public char GetCollision(int x, int y) => InBounds(x, y) ? collision[y * Width + x] : RpgCollision.Block;

        /// <summary>Sets a collision code; an unknown code becomes <see cref="RpgCollision.Auto"/>.</summary>
        public void SetCollision(int x, int y, char code)
        {
            if (!InBounds(x, y))
                return;
            collision[y * Width + x] = code == RpgCollision.Block || code == RpgCollision.Open ? code : RpgCollision.Auto;
        }

        /// <summary>
        /// Changes the size keeping the top-left content; new cells are empty.
        /// Used by the editor; the map's events are not moved.
        /// </summary>
        public void Resize(int width, int height)
        {
            var resized = new RpgMapGrid(width, height);
            for (var y = 0; y < Math.Min(Height, resized.Height); y++)
            {
                for (var x = 0; x < Math.Min(Width, resized.Width); x++)
                {
                    foreach (RpgLayer layer in Enum.GetValues(typeof(RpgLayer)))
                        resized.SetTile(layer, x, y, GetTile(layer, x, y));
                    resized.SetCollision(x, y, GetCollision(x, y));
                }
            }
            Width = resized.Width;
            Height = resized.Height;
            layers = resized.layers;
            collision = resized.collision;
        }

        /// <summary>Copy of the grid, so editor undo steps never share cells.</summary>
        public RpgMapGrid Clone()
        {
            var copy = new RpgMapGrid(Width, Height);
            for (var i = 0; i < layers.Length; i++)
                Array.Copy(layers[i], copy.layers[i], layers[i].Length);
            Array.Copy(collision, copy.collision, collision.Length);
            return copy;
        }

        /// <summary>
        /// Writes size, layer rows and collision rows into <paramref name="map"/>.
        /// Indices are right-aligned to one width per layer, so columns line up in
        /// the file. An all-auto collision layer is written as no rows.
        /// </summary>
        public void WriteTo(RpgMap map)
        {
            map.Width = Width;
            map.Height = Height;
            map.Layers ??= new RpgMapLayers();
            map.Layers.Back = WriteLayer(RpgLayer.Back);
            map.Layers.Main = WriteLayer(RpgLayer.Main);
            map.Layers.Front = WriteLayer(RpgLayer.Front);

            var rows = new List<string>();
            if (collision.Any(c => c != RpgCollision.Auto))
            {
                for (var y = 0; y < Height; y++)
                    rows.Add(new string(collision, y * Width, Width));
            }
            map.Collision = rows;
        }

        private void ReadLayer(RpgLayer layer, List<string> rows)
        {
            if (rows == null)
                return;
            for (var y = 0; y < Height && y < rows.Count; y++)
            {
                var tokens = (rows[y] ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var x = 0; x < Width && x < tokens.Length; x++)
                    SetTile(layer, x, y, int.TryParse(tokens[x], out var index) ? index : -1);
            }
        }

        private List<string> WriteLayer(RpgLayer layer)
        {
            var cells = layers[(int)layer];
            var width = cells.Select(Token).Max(t => t.Length);
            var rows = new List<string>();
            var line = new StringBuilder();
            for (var y = 0; y < Height; y++)
            {
                line.Clear();
                for (var x = 0; x < Width; x++)
                {
                    if (x > 0)
                        line.Append(' ');
                    line.Append(Token(cells[y * Width + x]).PadLeft(width));
                }
                rows.Add(line.ToString());
            }
            return rows;
        }

        private static string Token(int index) => index < 0 ? EmptyToken : index.ToString();
    }
}

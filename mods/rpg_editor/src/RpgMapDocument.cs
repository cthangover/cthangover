using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Cthangover.Mods.Rpg;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// The map being edited, free of any UI: the <see cref="RpgMap"/> with its
    /// cells parsed into an <see cref="RpgMapGrid"/>, the edit operations of the
    /// window and an undo history.
    /// <para/>
    /// Undo works on whole states: before an edit the window calls
    /// <see cref="BeginChange"/>, which stores the grid and the map's other data
    /// (events, spawn, size…) as JSON. A paint stroke is one change, however many
    /// cells it touches. The grid is the only source of the cells while editing;
    /// <see cref="ToJson"/> writes it back into the map rows.
    /// </summary>
    public class RpgMapDocument
    {
        private const int HistoryLimit = 200;

        private readonly List<RpgMapSnapshot> undo = new();
        private readonly List<RpgMapSnapshot> redo = new();

        /// <summary>Map data (events, spawn, encounters…); its layer rows are stale until <see cref="ToJson"/>.</summary>
        public RpgMap Map { get; private set; }

        /// <summary>The cells.</summary>
        public RpgMapGrid Grid { get; private set; }

        /// <summary>True when there is a step to undo.</summary>
        public bool CanUndo => undo.Count > 0;

        /// <summary>True when there is a step to redo.</summary>
        public bool CanRedo => redo.Count > 0;

        private RpgMapDocument(RpgMap map)
        {
            Map = map;
            Map.Events ??= new List<RpgMapEvent>();
            Map.Spawn ??= new RpgTeleport();
            Grid = RpgMapGrid.Parse(map);
        }

        /// <summary>
        /// A new map filled with one ground tile, its id taken from the file name.
        /// The spawn is the centre, so a fresh map is playable at once.
        /// </summary>
        public static RpgMapDocument Create(string id, string tileset, int width, int height, int groundTile)
        {
            var map = new RpgMap { ID = id, Tileset = tileset, Width = width, Height = height };
            var document = new RpgMapDocument(map);
            for (var y = 0; y < document.Grid.Height; y++)
            {
                for (var x = 0; x < document.Grid.Width; x++)
                    document.Grid.SetTile(RpgLayer.Back, x, y, groundTile);
            }
            map.Spawn = new RpgTeleport { X = document.Grid.Width / 2, Y = document.Grid.Height / 2 };
            return document;
        }

        /// <summary>
        /// Reads a map file (<c>{"Kind":"rpg_map","Items":[…]}</c>, the first item).
        /// Throws <see cref="JsonException"/> on broken JSON and
        /// <see cref="InvalidOperationException"/> on a file without maps, so the
        /// window can show the reason instead of opening an empty map.
        /// </summary>
        public static RpgMapDocument Load(string json)
        {
            var data = JsonSerializer.Deserialize<FileData<RpgMap>>(json, JsonOptionsProvider.ReadOptions);
            var map = data?.Items?.FirstOrDefault() ?? throw new InvalidOperationException("the file has no rpg_map item");
            return new RpgMapDocument(map);
        }

        /// <summary>The map file text: cells written into rows, nulls left out, indented.</summary>
        public string ToJson()
        {
            Grid.WriteTo(Map);
            var data = new FileData<RpgMap> { Kind = "rpg_map", Items = new List<RpgMap> { Map } };
            return JsonSerializer.Serialize(data, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
        }

        /// <summary>Remembers the current state as one undo step and forgets the redo branch.</summary>
        public void BeginChange()
        {
            undo.Add(Snapshot());
            if (undo.Count > HistoryLimit)
                undo.RemoveAt(0);
            redo.Clear();
        }

        /// <summary>Steps back; false when there is nothing to undo.</summary>
        public bool Undo() => Swap(undo, redo);

        /// <summary>Steps forward again; false when there is nothing to redo.</summary>
        public bool Redo() => Swap(redo, undo);

        /// <summary>Writes a tile (-1 clears) or a collision code into one cell. Returns true when the cell changed.</summary>
        public bool Paint(RpgEditLayer layer, int x, int y, int tile, char collision)
        {
            if (!Grid.InBounds(x, y))
                return false;
            if (layer == RpgEditLayer.Collision)
            {
                if (Grid.GetCollision(x, y) == collision)
                    return false;
                Grid.SetCollision(x, y, collision);
                return true;
            }
            if (layer == RpgEditLayer.Events || Grid.GetTile((RpgLayer)layer, x, y) == tile)
                return false;
            Grid.SetTile((RpgLayer)layer, x, y, tile);
            return true;
        }

        /// <summary>
        /// Flood-fills (4-neighbourhood) the area of cells equal to the start
        /// cell with a tile or a collision code. Returns the number of cells changed.
        /// </summary>
        public int Fill(RpgEditLayer layer, int x, int y, int tile, char collision)
        {
            if (!Grid.InBounds(x, y) || layer == RpgEditLayer.Events)
                return 0;

            var target = Read(layer, x, y);
            var replacement = layer == RpgEditLayer.Collision ? collision : tile;
            if (target == replacement)
                return 0;

            var changed = 0;
            var stack = new Stack<(int X, int Y)>();
            stack.Push((x, y));
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                if (!Grid.InBounds(cx, cy) || Read(layer, cx, cy) != target)
                    continue;
                Paint(layer, cx, cy, tile, collision);
                changed++;
                stack.Push((cx + 1, cy));
                stack.Push((cx - 1, cy));
                stack.Push((cx, cy + 1));
                stack.Push((cx, cy - 1));
            }
            return changed;
        }

        /// <summary>The events on a cell, top-most (last) first.</summary>
        public List<RpgMapEvent> EventsAt(int x, int y)
        {
            var found = Map.Events.Where(e => e.X == x && e.Y == y).ToList();
            found.Reverse();
            return found;
        }

        /// <summary>Adds an action event with a fresh id (<c>event_N</c>) on a cell and returns it.</summary>
        public RpgMapEvent AddEvent(int x, int y)
        {
            var number = 1;
            while (Map.Events.Any(e => string.Equals(e.Id, "event_" + number, StringComparison.OrdinalIgnoreCase)))
                number++;
            var mapEvent = new RpgMapEvent { Id = "event_" + number, X = x, Y = y };
            Map.Events.Add(mapEvent);
            return mapEvent;
        }

        /// <summary>Removes an event; false when it is not on this map.</summary>
        public bool RemoveEvent(RpgMapEvent mapEvent) => Map.Events.Remove(mapEvent);

        /// <summary>Changes the map size; events left outside are moved to the nearest edge cell.</summary>
        public void Resize(int width, int height)
        {
            Grid.Resize(width, height);
            foreach (var mapEvent in Map.Events)
            {
                mapEvent.X = Math.Clamp(mapEvent.X, 0, Grid.Width - 1);
                mapEvent.Y = Math.Clamp(mapEvent.Y, 0, Grid.Height - 1);
            }
            Map.Spawn.X = Math.Clamp(Map.Spawn.X, 0, Grid.Width - 1);
            Map.Spawn.Y = Math.Clamp(Map.Spawn.Y, 0, Grid.Height - 1);
        }

        /// <summary>
        /// Problems that would make the map misbehave in the game, as locale keys
        /// with an argument; empty when the map can be saved.
        /// </summary>
        public List<(string Key, string Argument)> Validate()
        {
            var problems = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(Map.Tileset))
                problems.Add(("tools/rpg_editor/problem_tileset", Map.ID));
            foreach (var mapEvent in Map.Events)
            {
                if (string.IsNullOrWhiteSpace(mapEvent.Id))
                    problems.Add(("tools/rpg_editor/problem_event_id", $"{mapEvent.X},{mapEvent.Y}"));
            }
            foreach (var group in Map.Events.Where(e => !string.IsNullOrWhiteSpace(e.Id)).GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() > 1)
                    problems.Add(("tools/rpg_editor/problem_event_duplicate", group.Key));
            }
            return problems;
        }

        private int Read(RpgEditLayer layer, int x, int y)
        {
            return layer == RpgEditLayer.Collision ? Grid.GetCollision(x, y) : Grid.GetTile((RpgLayer)layer, x, y);
        }

        private RpgMapSnapshot Snapshot()
        {
            return new RpgMapSnapshot(Grid.Clone(), JsonSerializer.Serialize(Map));
        }

        private bool Swap(List<RpgMapSnapshot> from, List<RpgMapSnapshot> to)
        {
            if (from.Count == 0)
                return false;
            to.Add(Snapshot());
            var state = from[^1];
            from.RemoveAt(from.Count - 1);
            Grid = state.Grid.Clone();
            Map = JsonSerializer.Deserialize<RpgMap>(state.MapJson);
            return true;
        }
    }
}

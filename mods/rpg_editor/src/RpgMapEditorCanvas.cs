using System;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Rpg;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// The map as the editor shows it: back, main and front tiles, the events
    /// (their charset frame or tile, else a marker), the spawn point, the
    /// collision layer and a grid. Tiles of layers other than the active one are
    /// dimmed, so the author sees which layer the brush is on; the collision
    /// codes are shown only while the collision layer is active.
    /// <para/>
    /// Mouse, on a tile or collision layer: LMB paints with the current tool and
    /// a drag paints a stroke, RMB erases, Alt+LMB picks the tile under the
    /// pointer. On the events layer: LMB selects the top event of a cell and drags
    /// it, a double click on an empty cell adds an event, RMB clears the selection.
    /// Ctrl+wheel zooms.
    /// <para/>
    /// The canvas owns no data. It edits the window's
    /// <see cref="RpgMapDocument"/> and reports through events; every stroke or
    /// drag starts with <see cref="EditStarting"/>, so the window records exactly
    /// one undo step for it.
    /// </summary>
    public partial class RpgMapEditorCanvas : Control
    {
        private static readonly Color BlockColor = new(0.9f, 0.2f, 0.2f, 0.45f);
        private static readonly Color OpenColor = new(0.2f, 0.85f, 0.35f, 0.4f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.12f);
        private static readonly Color MarkerColor = new(0.35f, 0.55f, 1f, 0.55f);

        private readonly RpgTextures textures = new();
        private Vector2I? hover;
        private bool stroking;
        private RpgMapEvent dragged;
        private bool dragMoved;

        /// <summary>The edited map; null shows an empty canvas.</summary>
        public RpgMapDocument Document { get; set; }

        /// <summary>Tileset of the map (null: tiles are not drawn).</summary>
        public RpgTileset Tileset { get; set; }

        /// <summary>Atlas image of <see cref="Tileset"/>.</summary>
        public Texture2D Atlas { get; set; }

        /// <summary>Layer the brush works on.</summary>
        public RpgEditLayer ActiveLayer { get; set; } = RpgEditLayer.Back;

        /// <summary>Brush.</summary>
        public RpgEditTool Tool { get; set; } = RpgEditTool.Pencil;

        /// <summary>Atlas index the pencil and the fill put down.</summary>
        public int SelectedTile { get; set; }

        /// <summary>Collision code the pencil and the fill put down.</summary>
        public char SelectedCollision { get; set; } = RpgCollision.Block;

        /// <summary>The selected event, or null.</summary>
        public RpgMapEvent SelectedEvent { get; set; }

        /// <summary>Draw the cell grid.</summary>
        public bool ShowGrid { get; set; } = true;

        /// <summary>Screen pixels per tile pixel.</summary>
        public float Zoom { get; private set; } = 1.5f;

        /// <summary>An edit is about to change the document: record an undo step.</summary>
        public event Action EditStarting;

        /// <summary>The document changed.</summary>
        public event Action Changed;

        /// <summary>The selected event changed (null for none).</summary>
        public event Action<RpgMapEvent> EventSelected;

        /// <summary>Alt+click picked a tile.</summary>
        public event Action<int> TilePicked;

        /// <summary>Creates the canvas; input comes through the GuiInput event.</summary>
        public RpgMapEditorCanvas()
        {
            ClipContents = true;
            FocusMode = FocusModeEnum.Click;
            TextureFilter = TextureFilterEnum.Nearest;
            GuiInput += OnGuiInput;
            MouseExited += OnMouseExited;
        }

        private float CellSize => (Tileset?.TileSize ?? 32) * Zoom;

        /// <summary>Changes the zoom (0.25–4) and resizes the canvas to the map.</summary>
        public void SetZoom(float zoom)
        {
            Zoom = Mathf.Clamp(zoom, 0.25f, 4f);
            Refresh();
        }

        /// <summary>Resizes to the map and redraws; call after any outside change.</summary>
        public void Refresh()
        {
            var grid = Document?.Grid;
            CustomMinimumSize = grid == null ? Vector2.Zero : new Vector2(grid.Width, grid.Height) * CellSize;
            QueueRedraw();
        }

        /// <summary>Draws the map.</summary>
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), UiTokens.Surface0);
            var grid = Document?.Grid;
            if (grid == null)
                return;

            DrawRect(new Rect2(Vector2.Zero, new Vector2(grid.Width, grid.Height) * CellSize), Colors.Black);
            DrawLayer(RpgLayer.Back);
            DrawLayer(RpgLayer.Main);
            DrawEvents();
            DrawLayer(RpgLayer.Front);
            DrawSpawn();
            if (ActiveLayer == RpgEditLayer.Collision)
                DrawCollision();
            if (ShowGrid)
                DrawGrid();
            if (hover.HasValue && grid.InBounds(hover.Value.X, hover.Value.Y))
                DrawRect(CellRect(hover.Value.X, hover.Value.Y), UiTokens.AccentBright, false, 2f);
        }

        private void DrawLayer(RpgLayer layer)
        {
            if (Atlas == null || Tileset == null)
                return;
            var dim = ActiveLayer <= RpgEditLayer.Front && (int)ActiveLayer != (int)layer;
            var modulate = new Color(1f, 1f, 1f, dim ? 0.45f : 1f);
            var grid = Document.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetTile(layer, x, y);
                    if (index >= 0)
                        DrawTextureRectRegion(Atlas, CellRect(x, y), TileRegion(index), modulate);
                }
            }
        }

        private void DrawEvents()
        {
            foreach (var mapEvent in Document.Map.Events)
            {
                var rect = CellRect(mapEvent.X, mapEvent.Y);
                var charset = string.IsNullOrEmpty(mapEvent.Sprite) ? null : textures.Charset(mapEvent.Sprite);
                if (charset != null)
                {
                    var frame = new Vector2(charset.GetWidth() / 3f, charset.GetHeight() / 4f);
                    var source = new Rect2(new Vector2(frame.X, frame.Y * (int)mapEvent.Dir), frame);
                    var size = frame * Zoom;
                    var target = new Rect2(rect.Position + new Vector2((rect.Size.X - size.X) / 2f, rect.Size.Y - size.Y), size);
                    DrawTextureRectRegion(charset, target, source);
                }
                else if (mapEvent.Tile >= 0 && Atlas != null && Tileset != null)
                {
                    DrawTextureRectRegion(Atlas, rect, TileRegion(mapEvent.Tile));
                }
                else
                {
                    DrawRect(rect.Grow(-CellSize * 0.15f), MarkerColor);
                }

                if (ActiveLayer == RpgEditLayer.Events)
                    DrawRect(rect, mapEvent == SelectedEvent ? UiTokens.AccentBright : UiTokens.Alpha(UiTokens.White, 0.5f), false, mapEvent == SelectedEvent ? 3f : 1f);
            }
        }

        private void DrawSpawn()
        {
            var spawn = Document.Map.Spawn;
            if (spawn == null)
                return;
            var rect = CellRect(spawn.X, spawn.Y);
            DrawCircle(rect.GetCenter(), CellSize * 0.18f, UiTokens.Ok);
            DrawArc(rect.GetCenter(), CellSize * 0.18f, 0f, Mathf.Tau, 24, Colors.Black, 1.5f);
        }

        private void DrawCollision()
        {
            var grid = Document.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var code = grid.GetCollision(x, y);
                    if (code == RpgCollision.Block)
                        DrawRect(CellRect(x, y), BlockColor);
                    else if (code == RpgCollision.Open)
                        DrawRect(CellRect(x, y), OpenColor);
                    else if (!RpgPassability.IsWalkable(grid, Tileset, x, y))
                        DrawRect(CellRect(x, y).Grow(-CellSize * 0.35f), BlockColor);
                }
            }
        }

        private void DrawGrid()
        {
            var grid = Document.Grid;
            var size = new Vector2(grid.Width, grid.Height) * CellSize;
            for (var x = 0; x <= grid.Width; x++)
                DrawLine(new Vector2(x * CellSize, 0f), new Vector2(x * CellSize, size.Y), GridColor);
            for (var y = 0; y <= grid.Height; y++)
                DrawLine(new Vector2(0f, y * CellSize), new Vector2(size.X, y * CellSize), GridColor);
        }

        private Rect2 CellRect(int x, int y) => new(new Vector2(x, y) * CellSize, new Vector2(CellSize, CellSize));

        private Rect2 TileRegion(int index)
        {
            var coords = RpgTileSetBuilder.AtlasCoords(Tileset, index);
            return new Rect2(coords * Tileset.TileSize, new Vector2(Tileset.TileSize, Tileset.TileSize));
        }

        private Vector2I CellAt(Vector2 position) => new((int)Mathf.Floor(position.X / CellSize), (int)Mathf.Floor(position.Y / CellSize));

        private void OnGuiInput(InputEvent @event)
        {
            if (Document == null)
                return;

            if (@event is InputEventMouseMotion motion)
            {
                hover = CellAt(motion.Position);
                if (stroking && (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right)) != 0)
                    Apply(hover.Value, (motion.ButtonMask & MouseButtonMask.Right) != 0);
                else if (dragged != null && (motion.ButtonMask & MouseButtonMask.Left) != 0)
                    Drag(hover.Value);
                QueueRedraw();
                return;
            }

            if (@event is not InputEventMouseButton button)
                return;

            if (button.Pressed && button.CtrlPressed && (button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown))
            {
                SetZoom(Zoom * (button.ButtonIndex == MouseButton.WheelUp ? 1.25f : 0.8f));
                AcceptEvent();
                return;
            }

            var cell = CellAt(button.Position);
            if (!button.Pressed)
            {
                if (stroking || dragMoved)
                    Changed?.Invoke();
                stroking = false;
                dragged = null;
                dragMoved = false;
                return;
            }

            if (ActiveLayer == RpgEditLayer.Events)
                PressOnEvents(button, cell);
            else if (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right)
                PressOnCells(button, cell);
            AcceptEvent();
        }

        private void PressOnCells(InputEventMouseButton button, Vector2I cell)
        {
            if (button.AltPressed && button.ButtonIndex == MouseButton.Left && ActiveLayer != RpgEditLayer.Collision)
            {
                var index = Document.Grid.GetTile((RpgLayer)ActiveLayer, cell.X, cell.Y);
                if (index >= 0)
                    TilePicked?.Invoke(index);
                return;
            }

            var erase = button.ButtonIndex == MouseButton.Right || Tool == RpgEditTool.Eraser;
            EditStarting?.Invoke();
            if (Tool == RpgEditTool.Fill && !erase)
            {
                Document.Fill(ActiveLayer, cell.X, cell.Y, SelectedTile, SelectedCollision);
                Changed?.Invoke();
                QueueRedraw();
                return;
            }
            stroking = true;
            Apply(cell, erase);
        }

        private void PressOnEvents(InputEventMouseButton button, Vector2I cell)
        {
            if (button.ButtonIndex == MouseButton.Right)
            {
                Select(null);
                return;
            }
            if (button.ButtonIndex != MouseButton.Left || !Document.Grid.InBounds(cell.X, cell.Y))
                return;

            var found = Document.EventsAt(cell.X, cell.Y);
            if (found.Count == 0 && button.DoubleClick)
            {
                EditStarting?.Invoke();
                Select(Document.AddEvent(cell.X, cell.Y));
                Changed?.Invoke();
                return;
            }

            // Repeated clicks on a stack of events cycle through it.
            var index = SelectedEvent != null ? found.IndexOf(SelectedEvent) : -1;
            Select(found.Count == 0 ? null : found[(index + 1) % found.Count]);
            dragged = SelectedEvent;
            dragMoved = false;
        }

        private void Apply(Vector2I cell, bool erase)
        {
            var tile = erase ? -1 : SelectedTile;
            var collision = erase ? RpgCollision.Auto : SelectedCollision;
            if (Document.Paint(ActiveLayer, cell.X, cell.Y, tile, collision))
                QueueRedraw();
        }

        private void Drag(Vector2I cell)
        {
            if (!Document.Grid.InBounds(cell.X, cell.Y) || (dragged.X == cell.X && dragged.Y == cell.Y))
                return;
            if (!dragMoved)
            {
                EditStarting?.Invoke();
                dragMoved = true;
            }
            dragged.X = cell.X;
            dragged.Y = cell.Y;
            EventSelected?.Invoke(dragged);
        }

        private void Select(RpgMapEvent mapEvent)
        {
            SelectedEvent = mapEvent;
            EventSelected?.Invoke(mapEvent);
            QueueRedraw();
        }

        private void OnMouseExited()
        {
            hover = null;
            QueueRedraw();
        }
    }
}

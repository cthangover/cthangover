using System;
using System.Linq;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Rpg;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// Brush picker of the editor's left column. For a tile layer it shows the
    /// tileset atlas at one and a half times the tile size with the selected tile framed and the
    /// solid tiles marked by a red corner; a click picks a tile, the tooltip
    /// names it (index, tag, solid). For the collision layer it shows the three
    /// collision codes instead.
    /// </summary>
    public partial class RpgTilePalette : Control
    {
        private const float Scale = 1.5f;
        private const float CodeHeight = 44f;

        private static readonly char[] Codes = { RpgCollision.Block, RpgCollision.Open, RpgCollision.Auto };

        private RpgTileset tileset;
        private Texture2D atlas;
        private bool collisionMode;

        /// <summary>Selected atlas index.</summary>
        public int SelectedTile { get; private set; }

        /// <summary>Selected collision code.</summary>
        public char SelectedCollision { get; private set; } = RpgCollision.Block;

        /// <summary>A tile was picked.</summary>
        public event Action<int> TileSelected;

        /// <summary>A collision code was picked.</summary>
        public event Action<char> CollisionSelected;

        /// <summary>Creates the palette.</summary>
        public RpgTilePalette()
        {
            TextureFilter = TextureFilterEnum.Nearest;
            MouseFilter = MouseFilterEnum.Stop;
            GuiInput += OnGuiInput;
        }

        /// <summary>Shows a tileset (null: nothing to pick).</summary>
        public void SetTileset(RpgTileset value, Texture2D image)
        {
            tileset = value;
            atlas = image;
            Relayout();
        }

        /// <summary>Switches between the tile atlas and the collision codes.</summary>
        public void SetCollisionMode(bool value)
        {
            collisionMode = value;
            Relayout();
        }

        /// <summary>Selects a tile from outside (the canvas' pipette).</summary>
        public void Select(int index)
        {
            SelectedTile = index;
            QueueRedraw();
        }

        /// <summary>Draws the atlas or the codes.</summary>
        public override void _Draw()
        {
            if (collisionMode)
            {
                for (var i = 0; i < Codes.Length; i++)
                    DrawCode(i);
                return;
            }
            if (atlas == null || tileset == null)
                return;

            var cell = tileset.TileSize * Scale;
            DrawTextureRect(atlas, new Rect2(Vector2.Zero, atlas.GetSize() * Scale), false);
            foreach (var tile in tileset.Tiles.Where(t => t.Solid))
            {
                var position = (Vector2)RpgTileSetBuilder.AtlasCoords(tileset, tile.Index) * cell;
                DrawColoredPolygon(new[] { position, position + new Vector2(cell * 0.25f, 0f), position + new Vector2(0f, cell * 0.25f) }, new Color(0.9f, 0.2f, 0.2f, 0.9f));
            }
            var selected = (Vector2)RpgTileSetBuilder.AtlasCoords(tileset, SelectedTile) * cell;
            DrawRect(new Rect2(selected, new Vector2(cell, cell)), UiTokens.AccentBright, false, 3f);
        }

        private void DrawCode(int i)
        {
            var rect = new Rect2(0f, i * (CodeHeight + 6f), Size.X, CodeHeight);
            var color = Codes[i] == RpgCollision.Block ? new Color(0.9f, 0.2f, 0.2f, 0.6f)
                : Codes[i] == RpgCollision.Open ? new Color(0.2f, 0.85f, 0.35f, 0.5f) : UiTokens.Surface2;
            DrawRect(rect, color);
            if (Codes[i] == SelectedCollision)
                DrawRect(rect, UiTokens.AccentBright, false, 3f);
            var font = GetThemeDefaultFont();
            DrawString(font, rect.Position + new Vector2(10f, CodeHeight / 2f + 6f), CodeLabel(Codes[i]), HorizontalAlignment.Left, -1, UiTokens.FontBody, UiTokens.White);
        }

        private static string CodeLabel(char code) => TranslationServer.Translate(code == RpgCollision.Block
            ? "tools/rpg_editor/collision_block"
            : code == RpgCollision.Open ? "tools/rpg_editor/collision_open" : "tools/rpg_editor/collision_auto");

        private void Relayout()
        {
            if (collisionMode)
                CustomMinimumSize = new Vector2(200f, Codes.Length * (CodeHeight + 6f));
            else
                CustomMinimumSize = atlas != null ? atlas.GetSize() * Scale : Vector2.Zero;
            TooltipText = string.Empty;
            QueueRedraw();
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion && !collisionMode && tileset != null)
            {
                TooltipText = Describe(IndexAt(motion.Position));
                return;
            }
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button)
                return;

            if (collisionMode)
            {
                var i = (int)(button.Position.Y / (CodeHeight + 6f));
                if (i >= 0 && i < Codes.Length)
                {
                    SelectedCollision = Codes[i];
                    CollisionSelected?.Invoke(SelectedCollision);
                }
            }
            else if (tileset != null && atlas != null)
            {
                var index = IndexAt(button.Position);
                if (index >= 0 && index < RpgTileSetBuilder.TileCount(tileset, atlas))
                {
                    SelectedTile = index;
                    TileSelected?.Invoke(index);
                }
            }
            QueueRedraw();
            AcceptEvent();
        }

        private int IndexAt(Vector2 position)
        {
            var cell = tileset.TileSize * Scale;
            var column = (int)(position.X / cell);
            var row = (int)(position.Y / cell);
            return column < 0 || column >= tileset.Columns || row < 0 ? -1 : row * tileset.Columns + column;
        }

        private string Describe(int index)
        {
            var info = tileset.Tiles.FirstOrDefault(t => t.Index == index);
            var solid = info?.Solid == true ? " · " + TranslationServer.Translate("tools/rpg_editor/solid_tile") : string.Empty;
            return $"#{index} {info?.Tag}{solid}";
        }
    }
}

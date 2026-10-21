using System;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Something standing on a map cell: the player, an NPC, a chest. Drawn
    /// either from a charset — 3 frames by 4 facings, rows in
    /// <see cref="RpgDirection"/> order, the middle frame is the standing pose — or
    /// from one atlas tile. The frame is bottom-aligned and centred on the cell,
    /// so taller charsets overlap the cell above like in RPG Maker.
    /// <para/>
    /// Movement is grid based: <see cref="StepTo"/> starts a step to the next
    /// cell, the node slides there at <see cref="TilesPerSecond"/> and then raises
    /// <see cref="StepFinished"/>. The actor's <see cref="Cell"/> changes at the
    /// start of a step, so two actors never walk into the same cell. The actor
    /// layer sorts children by Y, which keeps whoever is lower in front.
    /// </summary>
    public partial class RpgActor : Node2D
    {
        /// <summary>Walking speed.</summary>
        public const float TilesPerSecond = 4.5f;

        private static readonly int[] WalkCycle = { 0, 1, 2, 1 };

        private readonly int tileSize;
        private Sprite2D sprite;
        private Vector2 from;
        private Vector2 to;
        private float progress = 1f;
        private float animationTime;

        /// <summary>Raised when a step reaches its cell.</summary>
        public event Action<RpgActor> StepFinished;

        /// <summary>Cell the actor stands on (or walks into).</summary>
        public Vector2I Cell { get; private set; }

        /// <summary>Facing.</summary>
        public RpgDirection Facing { get; private set; }

        /// <summary>True while a step is in progress.</summary>
        public bool IsMoving => progress < 1f;

        /// <summary>The map event this actor shows; null for the player.</summary>
        public RpgMapEvent Event { get; }

        /// <summary>Creates an actor for a cell grid of <paramref name="tileSize"/> pixels.</summary>
        public RpgActor(int tileSize, RpgMapEvent mapEvent = null)
        {
            this.tileSize = tileSize;
            Event = mapEvent;
        }

        /// <summary>Shows a charset; a null texture leaves the actor invisible (an invisible trigger).</summary>
        public void UseCharset(Texture2D texture)
        {
            if (texture == null)
                return;
            sprite = new Sprite2D { Texture = texture, Hframes = 3, Vframes = 4, Centered = false };
            var frame = new Vector2(texture.GetWidth() / 3f, texture.GetHeight() / 4f);
            sprite.Position = new Vector2((tileSize - frame.X) / 2f, tileSize - frame.Y);
            AddChild(sprite);
            UpdateFrame();
        }

        /// <summary>Shows one atlas cell of the map's tileset.</summary>
        public void UseTile(Texture2D atlas, Vector2I atlasCoords)
        {
            if (atlas == null)
                return;
            sprite = new Sprite2D
            {
                Texture = atlas,
                Centered = false,
                RegionEnabled = true,
                RegionRect = new Rect2(atlasCoords * tileSize, new Vector2(tileSize, tileSize)),
            };
            AddChild(sprite);
        }

        /// <summary>Puts the actor on a cell at once, stopping any step.</summary>
        public void Place(Vector2I cell, RpgDirection facing)
        {
            Cell = cell;
            Facing = facing;
            progress = 1f;
            Position = CellPosition(cell);
            UpdateFrame();
        }

        /// <summary>Turns without moving.</summary>
        public void Face(RpgDirection facing)
        {
            Facing = facing;
            UpdateFrame();
        }

        /// <summary>Starts a step to the neighbour cell; the caller has checked it is free.</summary>
        public void StepTo(RpgDirection direction)
        {
            Facing = direction;
            from = CellPosition(Cell);
            Cell += RpgDirections.Delta(direction);
            to = CellPosition(Cell);
            progress = 0f;
        }

        /// <summary>Advances the step and the walk animation.</summary>
        public override void _Process(double delta)
        {
            if (!IsMoving)
                return;

            progress = Mathf.Min(1f, progress + (float)delta * TilesPerSecond);
            animationTime += (float)delta * TilesPerSecond * 2f;
            Position = from.Lerp(to, progress);
            UpdateFrame();
            if (!IsMoving)
                StepFinished?.Invoke(this);
        }

        private Vector2 CellPosition(Vector2I cell) => new(cell.X * tileSize, cell.Y * tileSize);

        private void UpdateFrame()
        {
            if (sprite == null || sprite.Hframes != 3)
                return;
            var column = IsMoving ? WalkCycle[(int)animationTime % WalkCycle.Length] : 1;
            sprite.Frame = (int)Facing * 3 + column;
        }
    }
}

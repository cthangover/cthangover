using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>Grid arithmetic of <see cref="RpgDirection"/>: the cell step it makes and the facing that looks back.</summary>
    public static class RpgDirections
    {
        /// <summary>Cell offset of one step; y grows downwards like the map rows.</summary>
        public static Vector2I Delta(RpgDirection direction) => direction switch
        {
            RpgDirection.Left => new Vector2I(-1, 0),
            RpgDirection.Right => new Vector2I(1, 0),
            RpgDirection.Up => new Vector2I(0, -1),
            _ => new Vector2I(0, 1),
        };

        /// <summary>The facing that looks at the one given: an NPC turns to the player with it.</summary>
        public static RpgDirection Opposite(RpgDirection direction) => direction switch
        {
            RpgDirection.Left => RpgDirection.Right,
            RpgDirection.Right => RpgDirection.Left,
            RpgDirection.Up => RpgDirection.Down,
            _ => RpgDirection.Up,
        };
    }
}

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// A cell on some map and the facing the player arrives with. Used for the
    /// map's spawn point and for teleport events (doors, map edges, stairs).
    /// </summary>
    public class RpgTeleport
    {
        /// <summary>Target map id; empty means the map the event is on.</summary>
        public string Map { get; set; }

        /// <summary>Target column.</summary>
        public int X { get; set; }

        /// <summary>Target row.</summary>
        public int Y { get; set; }

        /// <summary>Facing after arrival.</summary>
        public RpgDirection Dir { get; set; } = RpgDirection.Down;
    }
}

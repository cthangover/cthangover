namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Session state of the RPG world, outliving the map node: the shell (and
    /// with it the world) is rebuilt after every battle, so the position must
    /// live here.
    /// <para/>
    /// The position doubles as the arrival point: a teleport writes the target
    /// map and cell, then switches the scene; a map that is entered while
    /// <see cref="Data"/> names it starts the player there, any other entry uses
    /// the map's spawn. The same rule restores the player after a battle and
    /// after loading a save.
    /// <para/>
    /// <see cref="PendingWinFlag"/> is transient: the flag a running battle sets on
    /// victory. It is not saved, because battles cannot be saved mid-way.
    /// </summary>
    public class RpgState
    {
        /// <summary>The session's state.</summary>
        public static RpgState Instance { get; } = new();

        /// <summary>Saved position.</summary>
        public RpgStateData Data { get; set; } = new();

        /// <summary>Story flag set to "1" when the running battle is won; null when none.</summary>
        public string PendingWinFlag { get; set; }

        /// <summary>Stores a position (and the map it is on).</summary>
        public void Place(string map, int x, int y, RpgDirection dir)
        {
            Data = new RpgStateData { Map = map, X = x, Y = y, Dir = dir };
        }

        /// <summary>Back to "nowhere": the next map entry uses the spawn.</summary>
        public void Reset()
        {
            Data = new RpgStateData();
            PendingWinFlag = null;
        }
    }
}

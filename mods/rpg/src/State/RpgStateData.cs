namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Saved part of the RPG world state: where the player stands. Written into
    /// the mod's save slot as JSON, so every member is a plain public property.
    /// </summary>
    public class RpgStateData
    {
        /// <summary>Map the position belongs to; empty before the first map.</summary>
        public string Map { get; set; }

        /// <summary>Column.</summary>
        public int X { get; set; }

        /// <summary>Row.</summary>
        public int Y { get; set; }

        /// <summary>Facing.</summary>
        public RpgDirection Dir { get; set; } = RpgDirection.Down;
    }
}

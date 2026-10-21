namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Cell codes of the map's system collision layer. It is a per-cell override
    /// of the tiles' own <see cref="RpgTileInfo.Solid"/> flag: most cells stay
    /// <see cref="Auto"/>, and the author paints exceptions (an invisible wall at
    /// the cliff edge, a walkable gap in a hedge).
    /// </summary>
    public static class RpgCollision
    {
        /// <summary>Passability comes from the tiles.</summary>
        public const char Auto = '.';

        /// <summary>Always blocked.</summary>
        public const char Block = '#';

        /// <summary>Always walkable, whatever the tiles say.</summary>
        public const char Open = 'o';
    }
}

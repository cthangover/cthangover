namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Enumerates the four canonical time-of-day phases used across the game.
    /// Values are explicit hex flags (<c>0x01</c>–<c>0x04</c>) to support
    /// potential bitmask queries (e.g. "all daytime phases"). Phase is
    /// derived by <see cref="TimeData.Phase"/> from the current in-game hour.
    /// </summary>
    public enum PhaseType : int
    {
        /// <summary>06:00–09:59.</summary>
        Morning = 0x01,
        /// <summary>10:00–17:59.</summary>
        Day = 0x02,
        /// <summary>18:00–21:59.</summary>
        Evening = 0x03,
        /// <summary>22:00–05:59.</summary>
        Night = 0x04,
    };
}

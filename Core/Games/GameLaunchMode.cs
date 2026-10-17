namespace Cthangover.Core.Games
{
    /// <summary>Which mods a process boots with; see <see cref="GameLaunchArgs.Mode"/>.</summary>
    public enum GameLaunchMode
    {
        /// <summary>Only the front-end collections: main menu, settings, mod manager, game picker.</summary>
        Frontend,

        /// <summary>One game collection plus the front-end.</summary>
        Game,

        /// <summary>Every installed mod (development launches and tests).</summary>
        AllMods,
    }
}

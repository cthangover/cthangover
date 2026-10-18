namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>The ways the dock can restart what it is debugging (the restart menu, in menu order).</summary>
    public enum StudioRestartMode
    {
        /// <summary>The scenario again from its first line; game state stays as it is now.</summary>
        Scenario,
        /// <summary>Game state rolled back to the scenario's start, then the scenario again.</summary>
        Rollback,
        /// <summary>The whole scene re-entered: its composition runs again.</summary>
        Scene,
        /// <summary>The scenario again from the label above the current line.</summary>
        FromLabel,
    }
}

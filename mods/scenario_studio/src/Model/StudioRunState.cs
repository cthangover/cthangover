namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>State of the dialog runtime as the dock header shows it.</summary>
    public enum StudioRunState
    {
        /// <summary>A scenario runs freely.</summary>
        Running,
        /// <summary>Pause is on: the next action will stop before it runs.</summary>
        Paused,
        /// <summary>Stopped before an action (pause or breakpoint) until continue/step.</summary>
        Held,
        /// <summary>No scenario dialog is running.</summary>
        Idle,
    }
}

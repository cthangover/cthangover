namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>One row of the studio event log; <see cref="Time"/> is game-session time as text.</summary>
    public sealed class StudioLogEntry
    {
        public string Time { get; set; }
        public StudioLogCategory Category { get; set; }
        public StudioText Text { get; set; }
        public bool IsWarning { get; set; }
    }
}

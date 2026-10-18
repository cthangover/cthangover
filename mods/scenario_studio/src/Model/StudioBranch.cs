namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>An answer of the current question with its resolved outcome (<see cref="Target"/>: scene or action id).</summary>
    public sealed class StudioBranch
    {
        public StudioCodeLine Line { get; set; }
        public StudioOutcomeKind Kind { get; set; }
        public string Target { get; set; }
    }
}

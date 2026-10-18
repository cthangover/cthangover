namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// A scenario that targets the current scene, as the scene composition saw
    /// it: owning mod, priority, header condition (DSL, null when there is none) and its result (null when
    /// it was not evaluated), and the final
    /// status. <see cref="Reason"/> spells out a skip or a shadowing (another
    /// mod won the same scenario name) in words.
    /// </summary>
    public sealed class StudioCandidate
    {
        public string Name { get; set; }
        public StudioModScope Scope { get; set; }
        public int Priority { get; set; }
        public string Condition { get; set; }
        public bool? ConditionOk { get; set; }
        public bool IsOneRun { get; set; }
        public StudioCandidateStatus Status { get; set; }
        public StudioText Reason { get; set; }
    }
}

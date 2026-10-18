namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>Outcome of a scene candidate in the scene composition, the answer to "why did (not) it start".</summary>
    public enum StudioCandidateStatus
    {
        Running,
        Waiting,
        Skipped,
        Completed,
        Shadowed,
    }
}

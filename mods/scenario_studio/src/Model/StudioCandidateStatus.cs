namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>Outcome of a scene candidate as the dock shows it, the answer to "why did (not) it start".</summary>
    public enum StudioCandidateStatus
    {
        Running,
        Queued,
        Skipped,
        Completed,
        Shadowed,
        Failed,
    }
}

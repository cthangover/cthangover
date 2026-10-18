namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// One candidate event of a scene composition as reported by
    /// <see cref="Events.SceneComposedEvent"/>: the event id (scenario name or
    /// C# type), the owning mod or overlay and file for scenario events, the
    /// ordering inputs and what happened to it.
    /// </summary>
    public class SceneCandidateReport
    {
        public string Id { get; init; }
        public string ModId { get; init; }
        public string ScenarioPath { get; init; }
        public int Priority { get; init; }
        public string Condition { get; init; }
        public bool IsOneRun { get; init; }
        public SceneCandidateOutcome Outcome { get; set; }
    }
}

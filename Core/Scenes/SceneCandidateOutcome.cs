namespace Cthangover.Core.Scenes
{
    /// <summary>What the scene composition did with one candidate event, see <see cref="SceneCandidateReport"/>.</summary>
    public enum SceneCandidateOutcome
    {
        /// <summary>Instantiated and queued on the scene's event chain.</summary>
        Queued,
        /// <summary>Its header condition evaluated to false.</summary>
        ConditionFalse,
        /// <summary>A one-run scenario already completed in this game.</summary>
        Completed,
        /// <summary>Another event with the same id was taken first (an earlier mod or an overlay won).</summary>
        Shadowed,
        /// <summary>Instantiation failed (unreadable file, missing type…); see the SCENE log.</summary>
        Failed,
    }
}

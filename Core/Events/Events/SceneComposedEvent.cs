using System.Collections.Generic;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by <see cref="SceneManager.SwitchScene"/> after it composed the
    /// event chain of a scene: every candidate in start order with its outcome
    /// (queued, condition false, already completed, shadowed by a same-named
    /// event, failed). Debugging tools use it to answer "why did my scenario
    /// not start"; nothing in the game flow depends on it. Not published for
    /// the editor's test play, which bypasses composition.
    /// </summary>
    [EventKey("scene_composed")]
    public class SceneComposedEvent : ModEvent
    {
        /// <summary>Logical name of the composed scene.</summary>
        public string SceneName { get; init; }
        /// <summary>Candidates in composition order, shadowed ones included.</summary>
        public IReadOnlyList<SceneCandidateReport> Candidates { get; init; }
    }
}

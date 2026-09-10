namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Runtime descriptor for a scene event, holding the metadata needed to instantiate,
    /// schedule, and conditionally filter it. Can reference either a C# class (via
    /// <see cref="ClassName"/>) registered through <see cref="SceneEventRegistry"/>, or
    /// a .scenario file (via <see cref="ScenarioPath"/>) discovered from mod manifests.
    /// Used by <see cref="SceneManager.ComposeEvents"/> and <see cref="SceneManager.CreateEventInstance"/>.
    /// </summary>
    public class SceneEventInfo
    {
        /// <summary>
        /// Unique identifier for this event within its scene, used for deduplication
        /// and topological ordering.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The fully-qualified type name of the C# class implementing
        /// <see cref="ExecutableEvent"/>. Mutually exclusive with
        /// <see cref="ScenarioPath"/>.
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// The file path to a .scenario script file. Mutually exclusive with
        /// <see cref="ClassName"/>.
        /// </summary>
        public string ScenarioPath { get; set; }

        /// <summary>
        /// Execution priority; lower values execute earlier. Ties broken by
        /// <see cref="Id"/> ordering.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// The identifier of an event that must precede this one in the execution order.
        /// </summary>
        public string After { get; set; }

        /// <summary>
        /// A condition expression string evaluated by <see cref="ScenarioCondition.Evaluate"/>.
        /// The event is skipped if the condition resolves to <c>false</c>.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// When set, overrides whether the in-game light clock progresses during this event.
        /// </summary>
        public bool? LightUseTime { get; set; }

        /// <summary>
        /// When <c>true</c>, this event is marked as completed after execution and
        /// excluded from future scene entries.
        /// </summary>
        public bool IsOneRun { get; set; }
    }
}

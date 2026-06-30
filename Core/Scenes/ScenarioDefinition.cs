namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Represents a parsed .scenario file entry from a mod manifest. Maps a visual
    /// novel scenario script to a target scene with scheduling metadata, condition
    /// expressions, and save/permanence flags. Collections of these definitions are
    /// built by <see cref="ModManager.CollectScenarioDefinitions"/> and consumed in
    /// <see cref="SceneManager.ComposeEvents"/> to produce <see cref="SceneEventInfo"/>
    /// entries for the event queue.
    /// </summary>
    public class ScenarioDefinition
    {
        /// <summary>
        /// The display name of the scenario, also used as the unique event identifier
        /// within its scene.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The scene identifier that this scenario is associated with.
        /// </summary>
        public string Scene { get; set; }

        /// <summary>
        /// Execution priority; lower values execute earlier. Ties are broken by
        /// identifier ordering.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// The identifier of another event that must execute before this one, enabling
        /// topological ordering within a scene's event list.
        /// </summary>
        public string After { get; set; }

        /// <summary>
        /// A condition expression string parsed by <see cref="ConditionParser"/> and
        /// evaluated by <see cref="ScenarioCondition.Evaluate"/>. The event is skipped
        /// if the condition evaluates to <c>false</c>.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// When set, overrides whether the in-game light clock (time-of-day)
        /// progresses during this scenario. <c>null</c> means default behavior.
        /// </summary>
        public bool? LightUseTime { get; set; }

        /// <summary>
        /// When <c>true</c>, the player is allowed to save during this scenario.
        /// </summary>
        public bool SaveAllowed { get; set; }

        /// <summary>
        /// When <c>true</c>, this scenario is marked as completed after its first
        /// execution and will be filtered out on subsequent scene entries.
        /// </summary>
        public bool IsOneRun { get; set; }

        /// <summary>
        /// The identifier of the mod that provides this scenario definition.
        /// </summary>
        public string ModId { get; set; }

        /// <summary>
        /// The file system path to the .scenario file containing the scenario script text.
        /// </summary>
        public string FilePath { get; set; }
    }
}

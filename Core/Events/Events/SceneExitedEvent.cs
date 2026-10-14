namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published on the mod event bus when the player leaves a scene; one of
    /// the lifecycle events composed into the scene event chain.
    /// </summary>
    [LifecycleEvent]
    [EventKey("scene_exited")]
    public class SceneExitedEvent : ModEvent
    {
        /// <summary>Logical name of the scene being left.</summary>
        public string SceneName { get; init; }
    }
}

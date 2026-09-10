namespace Cthangover.Core.Events
{
    [LifecycleEvent]
    [EventKey("scene_exited")]
    public class SceneExitedEvent : ModEvent
    {
        public string SceneName { get; init; }
    }
}

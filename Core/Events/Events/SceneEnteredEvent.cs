using Godot;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published after a new scene loads (via CallDeferred,
    /// so Root is in the tree). Priorities:
    /// 0=ApplySceneDefaults, 5=PlaySceneAmbient, 10=UpdateMusicType,
    /// 15=ComposeEvents, 20=RunSubscriptions, 25=InteractiveManager.
    /// </summary>
    [LifecycleEvent]
    [EventKey("scene_entered")]
    public class SceneEnteredEvent : ModEvent
    {
        public string SceneName { get; init; }
        public Node Root { get; init; }
    }
}

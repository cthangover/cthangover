using Godot;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published after a new scene loads (via CallDeferred,
    /// so Root is in the tree). Bus subscriber priorities:
    /// 0=ApplySceneDefaults, 5=PlaySceneAmbient, 10=UpdateMusicType,
    /// 30=UiHost. The scenario event chain itself is composed
    /// synchronously inside SceneManager.SwitchScene before this event
    /// is published, and enqueued right after.
    /// </summary>
    [LifecycleEvent]
    [EventKey("scene_entered")]
    public class SceneEnteredEvent : ModEvent
    {
        /// <summary>Logical name of the entered scene.</summary>
        public string SceneName { get; init; }
        /// <summary>Root node of the freshly loaded scene, already in the tree.</summary>
        public Node Root { get; init; }
    }
}

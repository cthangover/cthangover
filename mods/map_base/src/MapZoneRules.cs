using Cthangover.Core.Scenes;
using Cthangover.Core.UI;

namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Shared rule that decides whether a map zone is actually enterable.
    /// Lives outside <see cref="MapWidget"/> because three views must agree on
    /// what "closed" means: the live map, the map editor's game-like preview
    /// and the map editor canvas (plus its availability hint).
    /// <para/>
    /// The authoring flag alone is not the whole story: a zone with an empty or
    /// unregistered target scene is closed in the running game no matter what
    /// the flag says, because authors routinely sketch regions before the
    /// target scene exists and travelling must stay blocked. Without this
    /// shared helper the editor showed such zones as open while the player
    /// blocked them — the editor must surface the same result the player gets.
    /// </summary>
    public static class MapZoneRules
    {
        /// <summary>
        /// Effective locked state of a zone. <paramref name="open"/> is the
        /// open state owned by the caller: the runtime override in the live map
        /// or the authoring <see cref="MapZone.Locked"/> flag in the editor. A
        /// zone is locked when it is missing, has no target scene, is closed by
        /// its state, or when its scene is not registered in the running game.
        /// </summary>
        public static bool IsLocked(MapZone zone, bool open)
        {
            if (zone == null || string.IsNullOrEmpty(zone.Scene))
                return true;

            if (!open)
                return true;

            var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
            return sceneManager != null && !sceneManager.HasScene(zone.Scene);
        }
    }
}

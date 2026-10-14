namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by <c>GodotSceneService</c> right before a scene switch
    /// resolves its route, carrying the route's music configuration.
    /// Lets subscribers (e.g. the audio engine mod) prepare playlists
    /// before the new scene enters. Not a lifecycle event: the scenario
    /// event chain does not gate on it.
    /// </summary>
    [EventKey("scene_route_activating")]
    public class SceneRouteActivatingEvent : ModEvent
    {
        /// <summary>Logical name of the scene being activated.</summary>
        public string LogicalName { get; init; }
        /// <summary>Resource path of the scene file being loaded.</summary>
        public string ScenePath { get; init; }
        /// <summary>Playlist key from the route definition (e.g. "town", "Battle").</summary>
        public string MusicScene { get; init; }
    }
}

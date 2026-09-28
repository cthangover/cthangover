using Cthangover.Core.Audio;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Declaration of a mod-provided scene route. The core knows nothing about
    /// gameplay-specific scene names (battle, shop, minigame); a mod registers an
    /// arbitrary logical name and the core routes <c>switch_scene</c> to the
    /// mod-relative tscn built by the runtime pipeline. The fade flag lets the
    /// mod request the shader transition that used to be hardcoded for battle,
    /// and the music pair tells the audio layer which playlist scene to select
    /// and which <see cref="MusicType"/> bucket to play (e.g. Combat).
    /// </summary>
    public class SceneRoute
    {
        /// <summary>Logical scene name used by scenarios, e.g. <c>"Arena"</c>.</summary>
        public string LogicalName { get; set; }

        /// <summary>Mod-relative path of the tscn, e.g. <c>scenes/arena/arena.tscn</c>.</summary>
        public string ScenePath { get; set; }

        /// <summary>When true, the scene is entered through the fade transition overlay.</summary>
        public bool Fade { get; set; }

        /// <summary>Scene name passed to playlist resolution; <c>null</c> keeps the current playlist.</summary>
        public string MusicScene { get; set; }

        /// <summary>Music bucket to switch to when the route is entered.</summary>
        public MusicType Music { get; set; } = MusicType.Ambient;
    }
}

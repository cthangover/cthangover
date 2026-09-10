namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Enumerates the built-in Godot .tscn scenes that can be directly loaded by
    /// <see cref="GodotSceneService"/>. Each value maps to a specific scene file
    /// path (e.g. <c>res://scenes/menu/main_menu.tscn</c>). Also used by music player
    /// behaviors to determine context-appropriate audio.
    /// </summary>
    public enum GodotSceneType
    {
        /// <summary>The main menu scene.</summary>
        MainMenu,
        /// <summary>The battle encounter scene.</summary>
        Battle,
        /// <summary>The base visual novel scene used for all scenario-driven content.</summary>
        BaseScene,
    };

}

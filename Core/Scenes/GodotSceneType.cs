namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Enumerates the built-in Godot .tscn scenes that can be directly loaded by
    /// <see cref="GodotSceneService"/>. Each value maps to a specific scene file
    /// path (e.g. <c>res://scenes/menu/main_menu.tscn</c>). Also used by music player
    /// behaviors to determine context-appropriate audio. Gameplay scenes such as
    /// arenas or minigames are not listed here — mods register them through
    /// <see cref="SceneRouteRegistry"/> instead.
    /// </summary>
    public enum GodotSceneType
    {
        /// <summary>The main menu scene.</summary>
        MainMenu,
        /// <summary>The 2D shell scene that shows every logical scene (scenario-driven content, RPG maps).</summary>
        BaseScene,
    };

}

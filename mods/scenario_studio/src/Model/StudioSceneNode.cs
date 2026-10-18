namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>A scene on the studio map with the mod that defines it; <see cref="Via"/> names the link (a command or a choice).</summary>
    public sealed class StudioSceneNode
    {
        public string Name { get; set; }
        public StudioNodeState State { get; set; }
        public StudioModScope Scope { get; set; }
        public StudioText Via { get; set; }
    }
}

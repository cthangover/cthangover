namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>A mod together with the way the studio may touch it; rendered as a scope badge.</summary>
    public sealed class StudioModScope
    {
        public string ModId { get; }
        public StudioScopeKind Kind { get; }

        public StudioModScope(string modId, StudioScopeKind kind)
        {
            ModId = modId;
            Kind = kind;
        }
    }
}

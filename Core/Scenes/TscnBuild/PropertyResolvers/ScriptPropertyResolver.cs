using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Handles <c>script</c> property. No-op — script is already applied at node creation via <c>Activator.CreateInstance</c>.</summary>
    public class ScriptPropertyResolver : IPropertyResolver
    {
        public bool CanResolve(string k, object v) => k == "script";
        public void Apply(Node n, string k, object v, PropertyContext ctx) { }
    }
}

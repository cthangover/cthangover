using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Handles <c>script</c> property. No-op — script is already applied at node creation via <c>Activator.CreateInstance</c>.</summary>
    public class ScriptPropertyResolver : IPropertyResolver
    {
        /// <summary>Claims the node-header <c>script</c> property so the default setter never applies it — the node's C# type was already chosen by <see cref="SceneBuilder.CreateNode"/> via <c>ScriptTypeRegistry</c>.</summary>
        public bool CanResolve(string k, object v) => k == "script";
        /// <summary>Intentionally empty: script assignment happens at node creation.</summary>
        public void Apply(Node n, string k, object v, PropertyContext ctx) { }
    }
}

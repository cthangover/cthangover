using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Attaches resources built during BuildAll to their node properties, including meshes, materials and collision shapes.</summary>
    public class SubResourcePropertyResolver : IPropertyResolver
    {
        /// <summary>Claims <c>SubResource(...)</c> property values so they are not written as raw refs by the default resolver.</summary>
        public bool CanResolve(string k, object v) => v is TscnPropertyValue pv && pv.IsSubResourceRef;
        /// <summary>Uses the shared per-scene resource instead of leaving native node properties at their defaults.</summary>
        public void Apply(Node n, string k, object v, PropertyContext ctx)
        {
            var reference = (TscnPropertyValue)v;
            var resource = ctx.SubResourceResolver?.Invoke(reference.RefId);
            if (resource != null) n.Set(k, Variant.From(resource));
        }
    }
}

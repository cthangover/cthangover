using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Handles <c>SubResource("id")</c>. No-op — sub-resources are resolved during BuildAll before nodes.</summary>
    public class SubResourcePropertyResolver : IPropertyResolver
    {
        public bool CanResolve(string k, object v) => v is TscnPropertyValue pv && pv.IsSubResourceRef;
        public void Apply(Node n, string k, object v, PropertyContext ctx) { }
    }
}

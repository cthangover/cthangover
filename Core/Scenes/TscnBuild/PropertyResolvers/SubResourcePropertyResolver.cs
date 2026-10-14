using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Handles <c>SubResource("id")</c>. No-op — sub-resources are resolved during BuildAll before nodes.</summary>
    public class SubResourcePropertyResolver : IPropertyResolver
    {
        /// <summary>Claims <c>SubResource(...)</c> property values so they are not written as raw refs by the default resolver.</summary>
        public bool CanResolve(string k, object v) => v is TscnPropertyValue pv && pv.IsSubResourceRef;
        /// <summary>Intentionally empty — sub_resources are attached to properties by the sub-resource builders before nodes are built.</summary>
        public void Apply(Node n, string k, object v, PropertyContext ctx) { }
    }
}

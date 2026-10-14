using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Catch-all: converts value via <see cref="SubResourceFactory.ToVariant"/> and sets it on the node. Always last in the resolver chain.</summary>
    public class DefaultPropertyResolver : IPropertyResolver
    {
        /// <summary>Catch-all: claims every property value that is not a resource reference. Must stay last in the resolver list.</summary>
        public bool CanResolve(string k, object v) => v is not TscnPropertyValue;
        /// <summary>Converts the parsed value to a Godot Variant and assigns it via <c>Node.Set</c>.</summary>
        public void Apply(Node n, string k, object v, PropertyContext ctx) => n.Set(k, SubResourceFactory.ToVariant(v));
    }
}

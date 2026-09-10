using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>Catch-all: converts value via <see cref="SubResourceFactory.ToVariant"/> and sets it on the node. Always last in the resolver chain.</summary>
    public class DefaultPropertyResolver : IPropertyResolver
    {
        public bool CanResolve(string k, object v) => v is not TscnPropertyValue;
        public void Apply(Node n, string k, object v, PropertyContext ctx) => n.Set(k, SubResourceFactory.ToVariant(v));
    }
}

using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="GradientTexture1D"/>. The <c>gradient</c> SubResource ref is resolved from already-built resources.</summary>
    public class GradientTexture1DBuilder : ISubResourceBuilder
    {
        public string ResourceType => "GradientTexture1D";
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx)
        {
            var t = new GradientTexture1D();
            foreach (var kvp in def.OrderedProperties)
            {
                if (kvp.Key == "gradient" && kvp.Value is TscnPropertyValue pv && pv.IsSubResourceRef) { var r = ctx.ResourceResolver?.Invoke(pv.RefId); if (r is Gradient g) t.Gradient = g; }
                else SubResourceFactory.SetProperty(t, kvp.Key, kvp.Value);
            }
            return t;
        }
    }
}

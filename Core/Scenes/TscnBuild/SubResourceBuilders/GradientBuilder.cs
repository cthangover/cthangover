using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="Gradient"/>. Handles <c>colors</c> (ParsedGodotValue[]→Color[]) and <c>offsets</c> (float[]) specially.</summary>
    public class GradientBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="Gradient"]</c>.</summary>
        public string ResourceType => "Gradient";
        /// <summary>Builds the gradient, converting the <c>colors</c> ParsedGodotValue array and <c>offsets</c> float array explicitly.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx)
        {
            var g = new Gradient();
            foreach (var kvp in def.OrderedProperties)
            {
                if (kvp.Key == "colors" && kvp.Value is ParsedGodotValue[] ca) { var cc = new Color[ca.Length]; for (var i = 0; i < ca.Length; i++) cc[i] = new Color(AsF(ca[i].Arg(0)), AsF(ca[i].Arg(1)), AsF(ca[i].Arg(2)), AsF(ca[i].Arg(3), 1f)); g.SetColors(cc); }
                else if (kvp.Key == "offsets" && kvp.Value is float[] oa) g.SetOffsets(oa);
                else SubResourceFactory.SetProperty(g, kvp.Key, kvp.Value);
            }
            return g;
        }
        private static float AsF(object v, float fb = 0f) => v is float f ? f : v is int i ? i : v is double d ? (float)d : fb;
    }
}

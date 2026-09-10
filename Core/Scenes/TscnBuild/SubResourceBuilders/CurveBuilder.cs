using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>
    /// Builds <see cref="Curve"/> from sub_resource properties.
    /// </summary>
    public class CurveBuilder : ISubResourceBuilder
    {
        public string ResourceType => "Curve";

        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx)
        {
            var c = new Curve();
            SubResourceFactory.ApplyProperties(c, def);
            return c;
        }
    }
}

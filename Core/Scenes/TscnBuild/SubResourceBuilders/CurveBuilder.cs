using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>
    /// Builds <see cref="Curve"/> from sub_resource properties.
    /// </summary>
    public class CurveBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="Curve"]</c>.</summary>
        public string ResourceType => "Curve";

        /// <summary>Creates a <see cref="Curve"/> and applies all parsed properties generically.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx)
        {
            var c = new Curve();
            SubResourceFactory.ApplyProperties(c, def);
            return c;
        }
    }
}

using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="ShaderMaterial"/>. The <c>shader</c> property is resolved via ExtResource ref; uniforms applied generically.</summary>
    public class ShaderMaterialBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="ShaderMaterial"]</c>.</summary>
        public string ResourceType => "ShaderMaterial";
        /// <summary>Builds the material, resolving the <c>shader</c> ExtResource ref via the resource resolver; uniforms apply generically.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx)
        {
            var m = new ShaderMaterial();
            foreach (var kvp in def.OrderedProperties)
            {
                if (kvp.Key == "shader" && kvp.Value is TscnPropertyValue pv && pv.IsExtResourceRef) { var r = ctx.ResourceResolver?.Invoke(pv.RefId); if (r is Shader s) m.Shader = s; }
                else SubResourceFactory.SetProperty(m, kvp.Key, kvp.Value);
            }
            return m;
        }
    }
}

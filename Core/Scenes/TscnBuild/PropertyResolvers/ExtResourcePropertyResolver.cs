using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>
    /// Resolves <c>ExtResource("id")</c> properties. Script → Type resolution (skipped, node already created).
    /// PackedScene → recursive build + Pack(). Other types → fallback extResourceResolver callback.
    /// </summary>
    public class ExtResourcePropertyResolver : IPropertyResolver
    {
        public bool CanResolve(string k, object v) => v is TscnPropertyValue pv && pv.IsExtResourceRef;

        public void Apply(Node node, string key, object value, PropertyContext ctx)
        {
            var pv = (TscnPropertyValue)value;
            var extRes = ctx.Scene.ExtResources.FirstOrDefault(r => r.Id == pv.RefId);
            if (extRes == null) return;

            if (extRes.Type == "Script")
            {
                var st = ScriptTypeRegistry.Resolve(extRes.Path);
                if (st != null && typeof(Node).IsAssignableFrom(st)) return;
            }

            if (extRes.Type == "PackedScene" && ctx.InstanceLoader != null)
            {
                var sub = ctx.InstanceLoader(extRes.Path);
                if (sub != null) { var packed = new PackedScene(); if (packed.Pack(sub) == Error.Ok) node.Set(key, packed); }
                return;
            }

            if (ctx.ExtResourceResolver != null)
            {
                var r = ctx.ExtResourceResolver(pv.RefId);
                if (r != null) node.Set(key, Variant.From(r));
            }
        }
    }
}

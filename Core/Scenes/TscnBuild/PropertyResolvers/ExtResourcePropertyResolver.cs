using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>
    /// Resolves <c>ExtResource("id")</c> properties. Script → Type resolution (skipped, node already created).
    /// PackedScene → recursive build + Pack(). Other types → fallback extResourceResolver callback.
    /// </summary>
    public class ExtResourcePropertyResolver : IPropertyResolver
    {
        /// <summary>Claims every property whose value is an <c>ExtResource(...)</c> reference.</summary>
        public bool CanResolve(string k, object v) => v is TscnPropertyValue pv && pv.IsExtResourceRef;

        /// <summary>PackedScene refs are built recursively and packed; other types go through the fallback resource resolver. Script refs on already-created nodes are ignored.</summary>
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
                if (sub != null)
                {
                    AssignSceneOwners(sub, sub);
                    var packed = new PackedScene();
                    if (packed.Pack(sub) == Error.Ok)
                        node.Set(key, packed);
                    else
                        GameLogger.Log("TSCN_BUILD", $"ExtResourcePropertyResolver: failed to pack '{extRes.Path}' for '{key}'", LogLevel.Warning);
                }
                return;
            }

            if (ctx.ExtResourceResolver != null)
            {
                var r = ctx.ExtResourceResolver(pv.RefId);
                if (r != null) node.Set(key, Variant.From(r));
            }
        }

        /// <summary>
        /// <see cref="PackedScene.Pack"/> serializes only nodes whose <c>Owner</c>
        /// is the packed root, while the runtime builder produces
        /// owner-less trees (Godot assigns owners only when loading native scenes).
        /// Without this, PackedScene-typed exports (e.g. <c>DialogBox.answerBoxScene</c>)
        /// instantiate as a bare root with no children. Owners are assigned on the
        /// throwaway instance built for packing, not on the cached resolver tree.
        /// </summary>
        private static void AssignSceneOwners(Node root, Node current)
        {
            foreach (var child in current.GetChildren())
            {
                child.Owner = root;
                AssignSceneOwners(root, child);
            }
        }
    }
}

using System;
using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>Context for <see cref="IPropertyResolver.Apply"/> — bundles the parsed scene, recursive instance loader, and fallback resource resolver.</summary>
    public class PropertyContext
    {
        /// <summary>The parsed scene being built; used to look up ext_resource entries by id.</summary>
        public ParsedScene Scene { get; set; }
        /// <summary>Callback that recursively builds a PackedScene reference into a Node tree.</summary>
        public Func<string, Node> InstanceLoader { get; set; }
        /// <summary>Fallback resolver for non-Script/non-PackedScene ext_resource types (textures, fonts, …).</summary>
        public Func<string, Resource> ExtResourceResolver { get; set; }
        /// <summary>Creates a context bundling the scene model and the two build callbacks.</summary>
        public PropertyContext(ParsedScene scene, Func<string, Node> instanceLoader, Func<string, Resource> extResourceResolver) { Scene = scene; InstanceLoader = instanceLoader; ExtResourceResolver = extResourceResolver; }
    }
}

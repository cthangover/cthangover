using System;
using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>Context for <see cref="IPropertyResolver.Apply"/> — bundles the parsed scene, recursive instance loader, and fallback resource resolver.</summary>
    public class PropertyContext
    {
        public ParsedScene Scene { get; set; }
        public Func<string, Node> InstanceLoader { get; set; }
        public Func<string, Resource> ExtResourceResolver { get; set; }
        public PropertyContext(ParsedScene scene, Func<string, Node> instanceLoader, Func<string, Resource> extResourceResolver) { Scene = scene; InstanceLoader = instanceLoader; ExtResourceResolver = extResourceResolver; }
    }
}

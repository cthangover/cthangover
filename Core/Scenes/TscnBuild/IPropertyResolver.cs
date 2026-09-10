using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Resolves a single node property during scene building. Each implementation handles one
    /// category of property value — Script refs, ExtResource refs, SubResource refs, or plain values.
    /// <para/>
    /// The <see cref="SceneBuilder"/> iterates all properties and delegates to
    /// <see cref="PropertyResolverRegistry.Find"/> which performs first-match-wins lookup.
    /// The <see cref="DefaultPropertyResolver"/> is always last (catch-all via <c>CanResolve → true</c>).
    /// <para/>
    /// Auto-discovered via <c>Reflections.FindAndCreate&lt;IPropertyResolver&gt;()</c>.
    /// Order in the registry list determines resolution priority — <see cref="ScriptPropertyResolver"/>
    /// is registered before <see cref="ExtResourcePropertyResolver"/>, etc.
    /// </summary>
    public interface IPropertyResolver
    {
        /// <summary>Non-destructive check: does this resolver handle the given property?</summary>
        bool CanResolve(string key, object value);

        /// <summary>Apply the property to the node. Called only if <see cref="CanResolve"/> returned true.</summary>
        void Apply(Node node, string key, object value, PropertyContext ctx);
    }
}


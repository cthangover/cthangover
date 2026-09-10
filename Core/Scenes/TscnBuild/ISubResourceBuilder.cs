using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Builds a Godot <see cref="Resource"/> from a <see cref="SubResourceDef"/> parsed from a [sub_resource] section.
    /// Each implementation handles exactly one resource type (e.g. "StyleBoxFlat", "Gradient").
    /// <para/>
    /// Godot native objects (Color, Vector2, etc.) are created here — this is build-time code
    /// that only runs inside Godot runtime. Unlike <see cref="TscnPropertyParser"/> which deliberately
    /// avoids creating Godot objects to remain testable outside the engine.
    /// <para/>
    /// Auto-discovered via <c>Reflections.FindAndCreate&lt;ISubResourceBuilder&gt;()</c>.
    /// </summary>
    public interface ISubResourceBuilder
    {
        /// <summary>Resource type name as it appears in the [sub_resource] header.</summary>
        string ResourceType { get; }

        /// <summary>Creates and configures the Godot resource. The <paramref name="ctx"/> provides cross-resource resolution (e.g. resolving a SubResource ref to a Gradient within GradientTexture1D).</summary>
        Resource Build(TscnParsing.SubResourceDef def, SubResourceBuildContext ctx);
    }

    /// <summary>
    /// Context passed to <see cref="ISubResourceBuilder.Build"/>, providing a callback to resolve
    /// resource references by ID. Used for cross-sub-resource dependencies
    /// (e.g. a GradientTexture1D referencing a Gradient sub-resource).
    /// </summary>
    public class SubResourceBuildContext
    {
        public System.Func<string, Resource> ResourceResolver { get; set; }
    }
}

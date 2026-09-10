using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Entry-point contract for scenes loaded via the TSCN runtime parser.
    /// Implementations are resolved by name (from the <c>__entry_point</c>
    /// property on the root node) and called after the entire node tree is built,
    /// properties applied, and signals connected.
    /// <para/>
    /// Resolution order: <see cref="Mods.ScriptTypeRegistry"/> first (mod-compiled types),
    /// then <c>Type.GetType()</c>, then all <c>AppDomain</c> assemblies as fallback.
    /// </summary>
    public interface ITscnSceneEntryPoint
    {
        void OnSceneBuilt(Node sceneRoot);
    }
}

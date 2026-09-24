using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Mod-aware replacement for <see cref="PackedScene"/> references held in code.
    /// Godot's <c>GD.Load&lt;PackedScene&gt;</c> cannot see files inside <c>mods/</c>
    /// (the folder is <c>.gdignore</c>d), so code that instantiates mod-provided
    /// scenes stores a mod-relative path and defers to <see cref="TscnScenes"/>.
    /// Each <see cref="Instantiate{T}"/> returns a fresh node built by the runtime
    /// tscn pipeline, mirroring PackedScene's instantiate-per-call contract.
    /// </summary>
    public sealed class ModScenePrefab
    {
        private readonly string path;

        /// <summary>Creates a prefab handle for a mod-relative scene path (e.g. "scenes/ui/foo.tscn").</summary>
        public ModScenePrefab(string modRelativePath)
        {
            path = modRelativePath;
        }

        /// <summary>Mod-relative path this prefab resolves, exposed for logging and diagnostics.</summary>
        public string Path => path;

        /// <summary>Builds a new instance and casts it to <typeparamref name="T"/>; null when unavailable.</summary>
        public T Instantiate<T>() where T : Node
        {
            return TscnScenes.LoadAndBuild(path) as T;
        }

        /// <summary>Builds a new untyped instance; null when the scene cannot be resolved.</summary>
        public Node Instantiate()
        {
            return TscnScenes.LoadAndBuild(path);
        }
    }
}

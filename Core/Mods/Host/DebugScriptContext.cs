namespace Cthangover.Core.Mods.Host
{
    using Godot;

    /// <summary>Game state and revision reporting scoped to a single debug script invocation.</summary>
    public sealed class DebugScriptContext
    {
        internal DebugScriptContext(SceneTree tree) => Tree = tree;

        /// <summary>The live game tree, available for game-thread operations while the script runs.</summary>
        public SceneTree Tree { get; }

        internal bool Changed { get; private set; }

        /// <summary>Marks a committed change so the debug host advances its state revision.</summary>
        public void MarkChanged() => Changed = true;
    }
}

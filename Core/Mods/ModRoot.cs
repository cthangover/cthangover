namespace Cthangover.Core.Mods
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Unload;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Adds service and test-driver nodes to the tree root one deferred step later — the way
    /// mod initializers must, because <c>OnModResourcesReady</c> fires while the root is still
    /// setting up its children and a direct <c>AddChild</c> is rejected.
    /// <para/>
    /// Use it instead of <c>root.CallDeferred("add_child", node)</c>: a synchronous
    /// <c>ModManager.Reload()</c> may unload the mod before the deferred step runs, and its
    /// sweep frees the waiting node. A raw deferred call then hands Godot a dead object
    /// (<c>Required object "rp_child" is null</c>, and on Windows heap corruption at exit).
    /// Here the unload drops the waiting nodes of the dying mod (<see cref="RemoveOwned"/>),
    /// and the deferred step skips a node that is gone, already has a parent, or whose name
    /// another root child already has (a reload re-running an initializer before the
    /// previous node arrived). Nodes are added in the order they were given.
    /// </summary>
    public static class ModRoot
    {
        private static readonly List<Node> pending = new();
        private static bool flushQueued;

        /// <summary>Adds <paramref name="node"/> to the tree root on the next deferred step.</summary>
        public static void AddDeferred(Node node)
        {
            if (node == null)
                return;
            pending.Add(node);
            if (flushQueued)
                return;
            flushQueued = true;
            Callable.From(Flush).CallDeferred();
        }

        /// <summary>Drops the waiting nodes whose type belongs to an unloading mod; returns how many.</summary>
        public static int RemoveOwned(AssemblyOwnership ownership)
            => pending.RemoveAll(node => ownership.Owns(node.GetType()));

        private static void Flush()
        {
            flushQueued = false;
            var nodes = new List<Node>(pending);
            pending.Clear();

            var root = (Engine.GetMainLoop() as SceneTree)?.Root;
            foreach (var node in nodes)
            {
                if (!GodotObject.IsInstanceValid(node) || node.GetParent() != null)
                    continue;
                if (root == null)
                {
                    node.QueueFree();
                    continue;
                }

                var name = node.Name.ToString();
                if (!string.IsNullOrEmpty(name) && root.GetNodeOrNull(name) != null)
                {
                    GameLogger.Log("MODS", $"ModRoot: '{name}' is already under the root; the new one is dropped", LogLevel.Warning);
                    node.QueueFree();
                    continue;
                }

                root.AddChild(node);
            }
        }
    }
}

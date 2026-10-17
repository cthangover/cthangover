namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Releases every engine-side hold on a dying mod assembly before its
    /// load context unloads. A live Godot object whose script is a mod type
    /// keeps a strong GC handle to its managed instance (and so to the mod
    /// assembly) for as long as the native object lives; a signal connected
    /// to a mod delegate keeps the delegate alive inside the native
    /// connection. Neither is visible from managed statics, so this pass
    /// walks the engine side:
    /// <list type="number">
    /// <item>live objects: every managed wrapper GodotSharp tracks
    /// (<c>DisposablesTracker</c> — this includes nodes outside the tree,
    /// cached UI and timers) plus the whole scene tree;</item>
    /// <item>connections to owned delegates are disconnected on every
    /// surviving object;</item>
    /// <item>owned objects are freed: nodes leave the tree and are freed
    /// immediately (callers run this outside signal emission, from a
    /// deferred step), plain objects are freed, reference-counted ones lose
    /// their script so the native object lives on as its engine base class
    /// while the mod instance dies.</item>
    /// </list>
    /// </summary>
    public static class GodotObjectSweeper
    {
        /// <summary>Runs the pass; returns one report line per freed group / disconnected signal.</summary>
        public static List<string> Release(SceneTree tree, AssemblyOwnership ownership)
        {
            var report = new List<string>();
            if (ownership == null || ownership.IsEmpty || !GameEnvironment.IsRuntime)
                return report;

            var objects = new List<GodotObject>();
            var seen = new HashSet<ulong>();
            foreach (var tracked in TrackedObjects())
                AddLive(tracked, objects, seen);
            if (tree?.Root != null)
                CollectTree(tree.Root, objects, seen);

            var disconnected = 0;
            var owned = new List<GodotObject>();
            foreach (var obj in objects)
            {
                if (ownership.Owns(obj.GetType()))
                {
                    owned.Add(obj);
                    continue;
                }
                disconnected += DisconnectOwned(obj, ownership);
            }
            if (disconnected > 0)
                report.Add($"disconnected {disconnected} signal connection(s) to mod code");

            var freedNodes = 0;
            var freedObjects = 0;
            var detached = 0;
            // Ancestors first: freeing a parent frees its subtree, so the
            // depth-sorted walk sees each surviving node exactly once.
            foreach (var node in owned.OfType<Node>().OrderBy(Depth).ToList())
            {
                if (!GodotObject.IsInstanceValid(node))
                    continue;
                node.GetParent()?.RemoveChild(node);
                node.Free();
                freedNodes++;
            }
            foreach (var obj in owned)
            {
                if (obj is Node || !GodotObject.IsInstanceValid(obj))
                    continue;
                if (obj is RefCounted)
                {
                    obj.SetScript(default(Variant));
                    obj.Dispose();
                    detached++;
                }
                else
                {
                    obj.Free();
                    freedObjects++;
                }
            }
            if (freedNodes + freedObjects + detached > 0)
                report.Add($"freed {freedNodes} node(s) and {freedObjects} object(s) of mod types, detached {detached} reference-counted instance(s)");
            return report;
        }

        private static int DisconnectOwned(GodotObject obj, AssemblyOwnership ownership)
        {
            var count = 0;
            try
            {
                foreach (var signal in obj.GetSignalList())
                {
                    var name = signal["name"].AsStringName();
                    foreach (var connection in obj.GetSignalConnectionList(name))
                    {
                        var callable = connection["callable"].AsCallable();
                        var target = callable.Target;
                        var owned = (callable.Delegate != null && ownership.Owns(callable.Delegate))
                                    || (target != null && GodotObject.IsInstanceValid(target) && ownership.Owns(target.GetType()));
                        if (!owned)
                            continue;
                        obj.Disconnect(name, callable);
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY", $"GodotObjectSweeper: signal scan of '{obj.GetType().Name}' failed: {ex.Message}", LogLevel.Debug);
            }
            return count;
        }

        private static void CollectTree(Node node, List<GodotObject> objects, HashSet<ulong> seen)
        {
            AddLive(node, objects, seen);
            foreach (var child in node.GetChildren(includeInternal: true))
                CollectTree(child, objects, seen);
        }

        private static void AddLive(GodotObject obj, List<GodotObject> objects, HashSet<ulong> seen)
        {
            if (obj == null || !GodotObject.IsInstanceValid(obj))
                return;
            if (seen.Add(obj.GetInstanceId()))
                objects.Add(obj);
        }

        private static int Depth(Node node)
        {
            var depth = 0;
            for (var parent = node.GetParent(); parent != null; parent = parent.GetParent())
                depth++;
            return depth;
        }

        /// <summary>Managed wrappers GodotSharp tracks for disposal at shutdown (internal <c>DisposablesTracker</c>).</summary>
        private static IEnumerable<GodotObject> TrackedObjects()
        {
            var tracker = typeof(GodotObject).Assembly.GetType("Godot.DisposablesTracker");
            var property = tracker?.GetProperty("GodotObjectInstances", BindingFlags.NonPublic | BindingFlags.Static);
            if (property?.GetValue(null) is not IDictionary instances)
            {
                GameLogger.Log("MODS_ASSEMBLY", "GodotObjectSweeper: DisposablesTracker not found; only scene-tree nodes are released", LogLevel.Warning);
                return Array.Empty<GodotObject>();
            }

            var result = new List<GodotObject>();
            foreach (DictionaryEntry entry in instances)
            {
                if (entry.Key is WeakReference<GodotObject> weak && weak.TryGetTarget(out var target))
                    result.Add(target);
            }
            return result;
        }
    }
}

namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods.Consumers;
    using Cthangover.Core.Utils;
    using Godot;
    using Cthangover.Core.Scenes.Parsing;

    /// <summary>
    /// The unload protocol for a set of mods, run as one pass so references
    /// between the dying mods never block each other. Order matters:
    /// <list type="number">
    /// <item><b>Mod-side teardown, types still alive</b> — the reverse consumer
    /// fan-out per assembly (initializers get <c>OnModUnloading</c>, registries
    /// drop what they took) and the script-type registry entry.</item>
    /// <item><b>Engine side</b> — Godot objects of mod types are freed and
    /// signal connections into mod code disconnected
    /// (<see cref="GodotObjectSweeper"/>).</item>
    /// <item><b>Managed leftovers</b> — event-bus subscriptions by code
    /// ownership, mod-contributed assembly consumers, then the generic
    /// <see cref="StaticReferenceSweeper"/> over the surviving assemblies and
    /// autoload nodes, then the System.Text.Json caches.</item>
    /// <item><b>Release</b> — each context unloads; GodotSharp drops the mod
    /// script types on the context's <c>Unloading</c> event, the manual purge
    /// covers anything registered before tracking was enabled.</item>
    /// </list>
    /// Collection itself happens later; the returned
    /// <see cref="ModUnloadTicket"/> verifies it. Everything is logged under
    /// <c>MODS_UNLOAD</c>: a non-empty sweep report names the subsystem that
    /// should unregister itself properly.
    /// </summary>
    public static class ModUnloader
    {
        /// <summary>
        /// Unloads <paramref name="modIdsInUnloadOrder"/> (dependents first).
        /// <paramref name="tree"/> is the live scene tree (null outside Godot).
        /// <paramref name="hostAssemblies"/> overrides the assemblies whose
        /// statics are swept (default: every surviving mod, plus the kernel
        /// inside the engine).
        /// Unknown ids are ignored.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ModUnloadTicket Unload(IReadOnlyList<string> modIdsInUnloadOrder, SceneTree tree, IEnumerable<Assembly> hostAssemblies = null)
        {
            var records = ModAssemblyLoader.Detach(modIdsInUnloadOrder);
            if (records.Count == 0)
                return new ModUnloadTicket(null);

            var ownership = new AssemblyOwnership(records.SelectMany(OwnedAssemblies));
            foreach (var record in records)
                Unregister(record);

            var report = new List<string>();
            report.AddRange(GodotObjectSweeper.Release(tree, ownership));

            var subscriptions = ModEventBus.RemoveOwned(ownership);
            foreach (var record in records)
                ModEventBus.Instance?.ClearForMod(record.ModId);
            if (subscriptions > 0)
                report.Add($"removed {subscriptions} event bus subscription(s) of mod code");

            var parsers = LeafParserRegistry.RemoveOwned(ownership, records.Select(r => r.ModId));
            if (parsers > 0)
                report.Add($"removed {parsers} condition parser(s) of mod code");

            var consumers = AssemblyConsumerRegistry.RemoveOwned(ownership);
            if (consumers > 0)
                report.Add($"removed {consumers} mod-contributed assembly consumer(s)");

            var hosts = hostAssemblies ?? DefaultHosts();
            report.AddRange(StaticReferenceSweeper.Sweep(ownership, hosts, AutoloadNodes(tree, ownership)));

            if (!JsonCacheReset.Clear())
                report.Add("System.Text.Json cache reset unavailable: serialized mod types may stay pinned");

            var tickets = new List<(string, WeakReference)>();
            foreach (var record in records)
            {
                if (record.Context == null)
                {
                    report.Add($"'{record.ModId}' was loaded in the default context and stays in memory");
                    continue;
                }
                tickets.Add((record.ModId, new WeakReference(record.Context)));
                record.Context.TryUnload();
            }

            var purged = GodotScriptBridgeUnload.PurgeOwnedTypes(ownership);
            if (purged > 0)
                report.Add($"purged {purged} script type entr(ies) GodotSharp did not release itself");

            var ids = new List<string>();
            foreach (var record in records)
                ids.Add(record.ModId);
            GameLogger.Log("MODS_UNLOAD", $"Unloaded {records.Count} mod(s): {string.Join(", ", ids)}");
            foreach (var line in report)
                GameLogger.Log("MODS_UNLOAD", "  " + line);
            return new ModUnloadTicket(tickets);
        }

        /// <summary>
        /// Reverse fan-out for mods that stay loaded but re-register (a reload
        /// rebuilds every registry; the assembly and its static state survive).
        /// </summary>
        public static void UnregisterKept(IReadOnlyList<string> modIdsInUnloadOrder)
        {
            foreach (var record in ModAssemblyLoader.Records(modIdsInUnloadOrder))
                Unregister(record);
        }

        private static void Unregister(LoadedModAssembly record)
        {
            for (var i = record.Assemblies.Count - 1; i >= 0; i--)
                AssemblyConsumerRegistry.FanOutUnloading(record.Assemblies[i], record.ModId);
            ScriptTypeRegistry.RemoveMod(record.ModId);
        }

        private static IEnumerable<Assembly> OwnedAssemblies(LoadedModAssembly record)
        {
            // Lazily resolved assemblies of the context (mod-shipped libraries)
            // die with it too, even if they never went through the fan-out.
            return record.Context != null
                ? record.Assemblies.Concat(record.Context.Assemblies).Distinct()
                : record.Assemblies;
        }

        private static IEnumerable<Assembly> DefaultHosts()
        {
            // The kernel's statics are read only inside the engine: outside it
            // (unit tests) reading one may run a static constructor that calls
            // into Godot, which kills the process instead of throwing.
            if (GameEnvironment.IsRuntime)
                yield return typeof(ModUnloader).Assembly;
            foreach (var record in ModAssemblyLoader.Records(ModAssemblyLoader.LoadedModIds))
            {
                foreach (var assembly in record.Assemblies)
                    yield return assembly;
            }
        }

        private static List<object> AutoloadNodes(SceneTree tree, AssemblyOwnership ownership)
        {
            var nodes = new List<object>();
            if (tree?.Root == null)
                return nodes;
            foreach (var node in tree.Root.GetChildren())
            {
                if (GodotObject.IsInstanceValid(node) && !ownership.Owns(node.GetType()))
                    nodes.Add(node);
            }
            return nodes;
        }
    }
}

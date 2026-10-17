using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Ordered registry of <see cref="IAssemblyConsumer"/> implementations -
    /// the single point the assembly loader fans every mod assembly out
    /// through, and the single point unload passes run through in reverse.
    ///
    /// Kernel consumers are discovered once via reflection; mod assemblies may
    /// contribute additional consumers (deduplicated by
    /// <see cref="IAssemblyConsumer.Id"/> - a mod-provided consumer never
    /// shadows a kernel one). Ordering is by <see cref="IAssemblyConsumer.Order"/>,
    /// ties broken by id so the sequence is deterministic regardless of
    /// discovery order.
    ///
    /// Failure isolation mirrors the rest of the mod pipeline: one consumer's
    /// exception is logged and never prevents the others from running.
    /// </summary>
    public static class AssemblyConsumerRegistry
    {
        private static readonly List<IAssemblyConsumer> consumers = new();
        private static readonly HashSet<string> consumerIds = new(StringComparer.OrdinalIgnoreCase);
        private static bool initialized;

        /// <summary>All consumers sorted by execution order. Snapshot copy.</summary>
        public static IReadOnlyList<IAssemblyConsumer> Consumers
        {
            get { EnsureInitialized(); return consumers.ToArray(); }
        }

        /// <summary>
        /// Discovers kernel <see cref="IAssemblyConsumer"/> implementations once.
        /// Deliberately not tied to the mod boot pipeline: the loader needs the
        /// fan-out in every context, including unit tests that never boot.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;
            RegisterFromAssembly(typeof(AssemblyConsumerRegistry).Assembly);
        }

        /// <summary>Test/setup helper: forgets every consumer, kernel ones included.</summary>
        public static void Clear()
        {
            consumers.Clear();
            consumerIds.Clear();
            initialized = false;
        }

        /// <summary>
        /// Discovers consumers in one assembly (the kernel scan, and every mod
        /// assembly through the loader). Returns the consumers that actually
        /// joined — duplicates by <see cref="IAssemblyConsumer.Id"/> are ignored —
        /// so the loader can replay the already registered assemblies to them.
        /// </summary>
        public static List<IAssemblyConsumer> RegisterFromAssembly(Assembly assembly)
        {
            var joined = new List<IAssemblyConsumer>();
            if (assembly == null)
                return joined;
            foreach (var consumer in Reflections.FindAndCreate<IAssemblyConsumer>(assembly))
            {
                if (Register(consumer))
                    joined.Add(consumer);
            }
            return joined;
        }

        /// <summary>
        /// Registers one consumer; the first registration of an id wins, later
        /// duplicates (a mod re-contributing a kernel consumer, a kept mod
        /// re-registering during a reload) are ignored. Re-sorts after insertion.
        /// Returns whether the consumer joined.
        /// </summary>
        public static bool Register(IAssemblyConsumer consumer)
        {
            if (consumer == null)
                return false;

            if (!consumerIds.Add(consumer.Id))
            {
                GameLogger.Log("MODS_ASSEMBLY", $"AssemblyConsumerRegistry: duplicate consumer id '{consumer.Id}' ignored", LogLevel.Debug);
                return false;
            }

            consumers.Add(consumer);
            consumers.Sort(CompareConsumers);
            return true;
        }

        /// <summary>
        /// Delivers one already registered assembly to a consumer that joined
        /// later (the catch-up of <see cref="ModAssemblyLoader.RegisterAssembly"/>),
        /// isolated like the regular fan-out.
        /// </summary>
        public static void CatchUp(IAssemblyConsumer consumer, Assembly assembly, string modId)
        {
            try
            {
                consumer.OnAssemblyLoaded(assembly, modId);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY",
                    $"AssemblyConsumer '{consumer.Id}' failed on catch-up of mod '{modId}': {ex.Message}",
                    LogLevel.Error);
            }
        }

        /// <summary>
        /// Drops consumers contributed by dying mod assemblies (a kept consumer
        /// instance pins its mod's load context). Returns how many were removed.
        /// </summary>
        public static int RemoveOwned(Unload.AssemblyOwnership ownership)
        {
            if (ownership == null || ownership.IsEmpty)
                return 0;
            var removed = 0;
            for (var i = consumers.Count - 1; i >= 0; i--)
            {
                if (!ownership.Owns(consumers[i].GetType()))
                    continue;
                consumerIds.Remove(consumers[i].Id);
                consumers.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        private static int CompareConsumers(IAssemblyConsumer a, IAssemblyConsumer b)
        {
            var byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        }

        /// <summary>
        /// Forward pass: delivers one assembly to every consumer in order.
        /// One consumer's failure is logged and isolated. Does not trigger
        /// kernel discovery: a cleared registry stays cleared (test isolation);
        /// production callers reach here only after
        /// <see cref="EnsureInitialized"/> ran through the load path.
        /// </summary>
        public static void FanOutLoaded(Assembly assembly, string modId)
        {
            foreach (var consumer in consumers.ToArray())
                RunConsumer(consumer, modId, c => c.OnAssemblyLoaded(assembly, modId), "load");
        }

        /// <summary>
        /// Reverse pass: asks every consumer to release what it took from one
        /// assembly, in reverse consumer order. Never throws: unload continues
        /// past a failing consumer so the remaining ones still get a chance to
        /// release their references. Does not trigger kernel discovery (see
        /// <see cref="FanOutLoaded"/>).
        /// </summary>
        public static void FanOutUnloading(Assembly assembly, string modId)
        {
            for (var i = consumers.Count - 1; i >= 0; i--)
            {
                var consumer = consumers[i];
                RunConsumer(consumer, modId, c => c.OnAssemblyUnloading(assembly, modId), "unload");
            }
        }

        private static void RunConsumer(IAssemblyConsumer consumer, string modId, Action<IAssemblyConsumer> call, string direction)
        {
            try
            {
                call(consumer);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY",
                    $"AssemblyConsumer '{consumer.Id}' failed on {direction} of mod '{modId}': {ex.Message}",
                    LogLevel.Error);
            }
        }
    }
}

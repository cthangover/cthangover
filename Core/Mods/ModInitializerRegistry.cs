using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Lifecycle orchestrator for <c>IModInitializer</c> implementations.
    /// When an assembly is registered, two things happen:
    ///
    /// 1. Every <b>existing</b> initializer receives
    ///    <c>OnModLoaded(newModId)</c> — so initializers loaded earlier
    ///    become aware of the new mod.
    ///
    /// 2. Every <b>new</b> initializer receives
    ///    <c>OnModLoaded(alreadyLoadedModId)</c> for each mod that was
    ///    loaded before it — so a late-arriving initializer sees the
    ///    complete mod set, not just the mods loaded after itself.
    ///
    /// This two-way catch-up guarantees that initializer state is
    /// consistent regardless of mod load order, which is essential
    /// since mod loading order depends on filesystem enumeration and
    /// dependency graphs, both of which are non-deterministic across
    /// platforms.
    /// </summary>
    public static class ModInitializerRegistry
    {
        private static readonly List<IModInitializer> _initializers = new();
        private static readonly List<string> _loadedModIds = new();

        /// <summary>
        /// Discovers <c>IModInitializer</c> implementations in the
        /// assembly, creates instances, and performs the two-way
        /// catch-up notification so that every initializer sees every
        /// loaded mod regardless of registration order.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            var newInitializers = new List<IModInitializer>();
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IModInitializer).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    try
                    {
                        newInitializers.Add((IModInitializer)Activator.CreateInstance(type));
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODS_INIT", $"Failed to create initializer for '{type.FullName}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            foreach (var init in _initializers)
            {
                try { init.OnModLoaded(modId); }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_INIT", $"OnModLoaded failed for existing initializer on mod '{modId}': {ex.Message}", LogLevel.Error);
                }
            }

            foreach (var loadedId in _loadedModIds)
            {
                foreach (var init in newInitializers)
                {
                    try { init.OnModLoaded(loadedId); }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODS_INIT", $"OnModLoaded failed for new initializer on mod '{loadedId}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            _initializers.AddRange(newInitializers);
            _loadedModIds.Add(modId);
        }

        /// <summary>
        /// Notifies every registered <c>IModInitializer</c> that all
        /// mods and their JSON resources are fully loaded. Call this
        /// once, after <c>ModRegistry.Initialize()</c> has completed
        /// and factories can read mod data.
        /// </summary>
        public static void NotifyResourcesReady()
        {
            foreach (var init in _initializers)
            {
                try { init.OnModResourcesReady(); }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_INIT", $"OnModResourcesReady failed: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }
}

namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Lets GodotSharp forget the script types of an unloaded mod.
    /// <c>ScriptManagerBridge</c> keeps two static maps (script pointer ↔ type,
    /// <c>res://</c> path ↔ type); a type in them pins its assembly's load
    /// context. GodotSharp already knows how to drop them — on
    /// <see cref="AssemblyLoadContext.Unloading"/> it removes every type it
    /// recorded for that context — but records types only while
    /// <c>AlcReloadCfg.IsAlcReloadingEnabled</c> is set, and GodotPlugins
    /// configures that flag to "editor only" at startup. A running game (from
    /// the editor or exported) therefore never released mod script types.
    ///
    /// <see cref="EnableTracking"/> turns the flag on before the first mod
    /// assembly loads, so GodotSharp tracks mod types itself;
    /// <see cref="PurgeOwnedTypes"/> is the fallback for types registered
    /// before that point (and for a GodotSharp that renamed its internals):
    /// it removes owned types from both maps directly. Both touch private
    /// GodotSharp state through reflection; any mismatch is logged and the
    /// unload verification reports the resulting leak.
    /// </summary>
    public static class GodotScriptBridgeUnload
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static bool trackingEnabled;

        /// <summary>
        /// Enables GodotSharp's per-context script type tracking. Idempotent;
        /// only meaningful inside the Godot runtime.
        /// </summary>
        public static void EnableTracking()
        {
            if (trackingEnabled || !GameEnvironment.IsRuntime)
                return;
            trackingEnabled = true;
            var flag = typeof(Godot.Bridge.AlcReloadCfg).GetField("IsAlcReloadingEnabled", Private);
            if (flag == null)
            {
                GameLogger.Log("MODS_ASSEMBLY", "GodotScriptBridgeUnload: AlcReloadCfg.IsAlcReloadingEnabled not found; mod script types are purged manually on unload", LogLevel.Warning);
                return;
            }
            flag.SetValue(null, true);
        }

        /// <summary>Registers a mod context with GodotSharp so its script types are dropped when it unloads.</summary>
        public static void Track(AssemblyLoadContext context)
        {
            if (context == null || !GameEnvironment.IsRuntime)
                return;
            EnableTracking();
            Godot.Bridge.ScriptManagerBridge.TrackAlcForUnloading(context);
        }

        /// <summary>
        /// Removes owned types still present in the script maps (after the
        /// context's <c>Unloading</c> already ran). Returns how many entries
        /// were removed; zero is the healthy case.
        /// </summary>
        public static int PurgeOwnedTypes(AssemblyOwnership ownership)
        {
            if (ownership == null || ownership.IsEmpty || !GameEnvironment.IsRuntime)
                return 0;
            try
            {
                var bridge = typeof(Godot.Bridge.ScriptManagerBridge);
                var removed = 0;

                var scriptMap = bridge.GetField("_scriptTypeBiMap", Private)?.GetValue(null);
                if (scriptMap != null)
                {
                    var rwLock = scriptMap.GetType().GetField("ReadWriteLock")?.GetValue(scriptMap) as System.Threading.ReaderWriterLockSlim;
                    rwLock?.EnterWriteLock();
                    try
                    {
                        removed += RemoveOwned(scriptMap, "_scriptTypeMap", ownership, byValue: true);
                        removed += RemoveOwned(scriptMap, "_typeScriptMap", ownership, byValue: false);
                    }
                    finally
                    {
                        rwLock?.ExitWriteLock();
                    }
                }

                var pathMap = bridge.GetField("_pathTypeBiMap", Private)?.GetValue(null);
                if (pathMap != null)
                {
                    removed += RemoveOwned(pathMap, "_pathTypeMap", ownership, byValue: true);
                    removed += RemoveOwned(pathMap, "_typePathMap", ownership, byValue: false);
                }

                return removed;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY", $"GodotScriptBridgeUnload.PurgeOwnedTypes failed: {ex.Message}", LogLevel.Warning);
                return 0;
            }
        }

        private static int RemoveOwned(object map, string fieldName, AssemblyOwnership ownership, bool byValue)
        {
            if (map.GetType().GetField(fieldName, PrivateInstance)?.GetValue(map) is not IDictionary dictionary)
                return 0;
            var doomed = new List<object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var type = (byValue ? entry.Value : entry.Key) as Type;
                if (ownership.Owns(type))
                    doomed.Add(entry.Key);
            }
            foreach (var key in doomed)
                dictionary.Remove(key);
            return doomed.Count;
        }
    }
}

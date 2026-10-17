using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Registry of <see cref="IModSaveable"/> implementations. When a
    /// mod assembly is loaded via
    /// <see cref="ModAssemblyLoader.RegisterAssembly"/>, types are
    /// scanned and instances created.
    ///
    /// On save, <see cref="CollectSaveData"/> creates a pre-filled
    /// <see cref="ModSlot"/> for each registered mod — the core sets
    /// <c>ModId</c>, <c>ModName</c>, and <c>ModVersion</c> so the mod
    /// does not need to — then calls <see cref="IModSaveable.OnSave"/>.
    ///
    /// On load, <see cref="DispatchLoad"/> distributes slots from the
    /// save file via <see cref="IModSaveable.OnLoad"/>, preferring the
    /// explicit <c>bool OnLoad(ModSlot, out string)</c> overload and
    /// falling back to exception-based <c>OnLoadVoid</c>.
    /// </summary>
    public static class ModSaveableRegistry
    {
        private static readonly List<RegisteredSaveable> _saveables = new();

        private class RegisteredSaveable
        {
            public IModSaveable Instance;
            public string ModId;
        }

        /// <summary>
        /// Discovers <see cref="IModSaveable"/> implementations in the
        /// assembly and registers them under the given modId.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            foreach (var instance in Reflections.FindAndCreate<IModSaveable>(assembly))
            {
                try
                {
                    _saveables.Add(new RegisteredSaveable { Instance = instance, ModId = modId });
                    GameLogger.Log("MODS_SAVE", $"Registered IModSaveable '{instance.GetType().FullName}' for mod '{modId}'", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_SAVE", $"Failed to create IModSaveable '{instance.GetType().FullName}': {ex.Message}", LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// Removes every saveable registered for <paramref name="modId"/> from
        /// the given assembly — the unload pass of the assembly consumer
        /// pipeline. Matching by both mod id and declaring assembly keeps the
        /// removal exact even when a mod ships multiple assemblies or another
        /// mod overrode the same saveable contract.
        /// </summary>
        public static void UnregisterAssembly(Assembly assembly, string modId)
        {
            _saveables.RemoveAll(e => e.ModId == modId && e.Instance.GetType().Assembly == assembly);
        }

        /// <summary>
        /// Collects save data from all registered mods. For each one,
        /// creates a <see cref="ModSlot"/> with
        /// <c>Metadata.ModId</c>, <c>Metadata.ModName</c>, and
        /// <c>Metadata.ModVersion</c> pre-filled from
        /// <see cref="IModInfo"/>, then calls
        /// <see cref="IModSaveable.OnSave"/> and stores the slot keyed
        /// by ModId.
        /// </summary>
        public static Dictionary<string, ModSlot> CollectSaveData()
        {
            var result = new Dictionary<string, ModSlot>();

            foreach (var entry in _saveables)
            {
                try
                {
                    var modInfo = ModRegistry.Instance.GetMod(entry.ModId);

                    var slot = new ModSlot
                    {
                        Metadata = new ModMetadata
                        {
                            ModId      = entry.ModId,
                            ModName    = modInfo?.DisplayTitle ?? entry.ModId,
                            ModVersion = modInfo?.Version ?? "0.0.0",
                        }
                    };

                    entry.Instance.OnSave(slot);
                    result[entry.ModId] = slot;
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_SAVE", $"OnSave failed for mod '{entry.ModId}': {ex.Message}", LogLevel.Error);
                }
            }

            return result;
        }

        /// <summary>
        /// Dispatches data slots to mods on load. For each registered
        /// mod whose ID is present in the loaded slot dictionary, calls
        /// <see cref="IModSaveable.OnLoad(Cthangover.Core.Settings.ModSlot, out string)"/>.
        /// If the mod does not override the bool overload, the default
        /// implementation delegates to
        /// <see cref="IModSaveable.OnLoadVoid"/> — exceptions from
        /// either path are caught and returned as errors. Returns an
        /// empty list on success.
        /// </summary>
        public static List<string> DispatchLoad(Dictionary<string, ModSlot> modData, ICollection<string> failedModIds = null)
        {
            var errors = new List<string>();

            if (modData == null)
                return errors;

            foreach (var entry in _saveables)
            {
                if (!modData.TryGetValue(entry.ModId, out var slot))
                    continue;

                try
                {
                    if (entry.Instance.OnLoad(slot, out var error))
                    {
                        GameLogger.Log("MODS_SAVE", $"OnLoad succeeded for mod '{entry.ModId}'");
                    }
                    else
                    {
                        var modName = slot.Metadata?.ModName ?? entry.ModId;
                        errors.Add($"Mod '{entry.ModId}' ({modName}): {error ?? "unspecified error"}");
                        failedModIds?.Add(entry.ModId);
                    }
                }
                catch (Exception ex)
                {
                    var modName = slot.Metadata?.ModName ?? entry.ModId;
                    var errorMsg = $"Mod '{entry.ModId}' ({modName}): {ex.Message}";
                    GameLogger.Log("MODS_SAVE", $"OnLoad failed: {errorMsg}", LogLevel.Error);
                    errors.Add(errorMsg);
                    failedModIds?.Add(entry.ModId);
                }
            }

            return errors;
        }

        /// <summary>
        /// Tells every saveable whose mod had no restored slot that it joined
        /// an existing game (<see cref="IModSaveable.OnJoinedExistingGame"/>).
        /// </summary>
        public static void NotifyJoined(IEnumerable<string> restoredModIds)
        {
            var restored = new HashSet<string>(restoredModIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (var entry in _saveables)
            {
                if (restored.Contains(entry.ModId))
                    continue;

                try
                {
                    entry.Instance.OnJoinedExistingGame();
                    GameLogger.Log("MODS_SAVE", $"Mod '{entry.ModId}' joined an existing game (no data in the save)");
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_SAVE", $"OnJoinedExistingGame failed for mod '{entry.ModId}': {ex.Message}", LogLevel.Error);
                }
            }
        }
    }
}

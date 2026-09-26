using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Data half of the main-menu extension pipeline: collects, validates and
    /// orders the <c>main_menu</c> declarations from every loaded mod manifest.
    /// It is the title-screen counterpart of <see cref="UiRegistry"/> — same
    /// manifest-driven contract, but no host and no persistent cache: the native
    /// main menu is rebuilt on every entry, and <see cref="MainMenuBuilder"/>
    /// materializes the buttons straight into <c>MenuContainer/ModMenuButtons</c>.
    /// <para/>
    /// Only mods present in <c>ModManager.Mods</c> contribute (disabled mods and
    /// mods that failed dependency validation give no button). Validation mirrors
    /// <c>UiRegistry</c>: every problem is logged with the mod id and the entry is
    /// skipped while the other mods still load. <see cref="Clear"/> is called on
    /// mod reload so a hot-reloaded mod set is reflected without a restart.
    /// </summary>
    public static class MainMenuRegistry
    {
        private static readonly List<MainMenuEntry> entries = new();
        private static readonly Dictionary<string, MainMenuEntry> byId = new(StringComparer.OrdinalIgnoreCase);
        private static bool initialized;

        /// <summary>All valid main menu entries in mod load order (then button index, then id).</summary>
        public static IReadOnlyList<MainMenuEntry> Entries => entries;

        /// <summary>
        /// Collects and validates the <c>main_menu</c> arrays of all loaded mods.
        /// Safe to call multiple times; later calls are no-ops until <see cref="Clear"/>.
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            Collect();
        }

        /// <summary>
        /// Drops all collected entries so the next <see cref="Initialize"/> re-reads
        /// the manifests. Called by <see cref="ModManager"/> on reload/clear.
        /// </summary>
        public static void Clear()
        {
            entries.Clear();
            byId.Clear();
            initialized = false;
        }

        /// <summary>Returns the entry with the given id, or null when unknown.</summary>
        public static MainMenuEntry Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return byId.TryGetValue(id, out var entry) ? entry : null;
        }

        private static void Collect()
        {
            var manager = ModManager.Instance;
            if (manager?.Mods == null)
            {
                GameLogger.Log("MAIN_MENU_REGISTRY", "Collect: mods not loaded", LogLevel.Error);
                return;
            }

            var orderedIds = ModRegistry.Instance.GetOrderedModIds();
            for (var order = 0; order < orderedIds.Count; order++)
            {
                var modId = orderedIds[order];
                if (!manager.Mods.TryGetValue(modId, out var info) || info is not ModInfo modInfo)
                    continue;

                var declarations = modInfo.Manifest?.MainMenu;
                if (declarations == null || declarations.Count == 0)
                    continue;

                var accepted = new List<MainMenuEntry>();
                foreach (var raw in declarations)
                {
                    if (raw == null)
                        continue;
                    raw.ModId = modId;
                    raw.ModOrderIndex = order;
                    if (Validate(raw, modInfo))
                        accepted.Add(raw);
                }

                foreach (var entry in accepted)
                    AddEntry(entry);
            }

            entries.Sort(CompareButtons);
            GameLogger.Log("MAIN_MENU_REGISTRY", $"Collect → {entries.Count} button(s)");
        }

        private static int CompareButtons(MainMenuEntry a, MainMenuEntry b)
        {
            var index = a.Button.Index.CompareTo(b.Button.Index);
            if (index != 0)
                return index;
            var modOrder = a.ModOrderIndex.CompareTo(b.ModOrderIndex);
            if (modOrder != 0)
                return modOrder;
            return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddEntry(MainMenuEntry entry)
        {
            entries.Add(entry);
            byId[entry.Id] = entry;
        }

        private static bool Validate(MainMenuEntry entry, ModInfo modInfo)
        {
            var label = $"{modInfo.Id}/{entry.Id ?? "?"}";

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                GameLogger.Log("MAIN_MENU_REGISTRY", $"invalid main_menu entry in mod '{modInfo.Id}': missing 'id'", LogLevel.Error);
                return false;
            }

            if (byId.ContainsKey(entry.Id))
            {
                GameLogger.Log("MAIN_MENU_REGISTRY", $"invalid main_menu entry '{label}': duplicate id", LogLevel.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                GameLogger.Log("MAIN_MENU_REGISTRY", $"invalid main_menu entry '{label}': missing 'scene_path'", LogLevel.Error);
                return false;
            }

            if (entry.Button == null || string.IsNullOrWhiteSpace(entry.Button.Locale))
            {
                GameLogger.Log("MAIN_MENU_REGISTRY", $"invalid main_menu entry '{label}': 'button.locale' is required", LogLevel.Error);
                return false;
            }

            var provider = modInfo.FileProvider;
            if (provider != null && !provider.FileExists(entry.ScenePath))
                GameLogger.Log("MAIN_MENU_REGISTRY", $"main_menu entry '{label}': scene_path '{entry.ScenePath}' not found in mod '{modInfo.Id}'", LogLevel.Warning);

            return true;
        }
    }
}

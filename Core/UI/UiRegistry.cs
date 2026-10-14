using System;
using System.Collections.Generic;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Data half of the mod GUI pipeline: collects, validates and orders the
    /// <c>ui</c> declarations from every loaded mod manifest. Creates no nodes —
    /// <see cref="UiBuilder"/> reads <see cref="PanelEntries"/> to build the toolbar
    /// once per shell, and <see cref="UiHost"/> resolves entries by id at runtime.
    /// <para/>
    /// Validation is mandatory and runs once per build: every problem is logged
    /// with its mod id, the offending entry is skipped, and the remaining mods
    /// still load. <see cref="IGameRegistry.Clear"/> is called on mod reload so a
    /// hot-reloaded mod set is reflected without restarting the game.
    /// <para/>
    /// The registry instance lives in <see cref="AppServices.Current"/>; the
    /// static members below are a facade delegating to it, so existing callers
    /// keep working unchanged.
    /// </summary>
    public class UiRegistry : IGameRegistry
    {
        private readonly List<UiEntry> entries = new();
        private readonly List<UiEntry> panels = new();
        private readonly Dictionary<string, UiEntry> byId = new(StringComparer.OrdinalIgnoreCase);
        private bool built;

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static UiRegistry Owned => AppServices.Current.Ui;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract. <see cref="Rebuild"/> fills the data.
        /// </summary>
        internal UiRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "ui";

        /// <summary>
        /// Drops all collected entries, then re-collects and re-validates the
        /// <c>ui</c> arrays of all loaded mods. Forced: safe to call repeatedly.
        /// </summary>
        public void Rebuild()
        {
            Reset();
            built = true;
            Collect();
        }

        /// <summary>
        /// Drops all collected entries so the next build re-reads the manifests.
        /// Called on mod reload through AppServices.ClearRegistries.
        /// </summary>
        void IGameRegistry.Clear()
        {
            Reset();
        }

        /// <summary>
        /// Collects and validates the <c>ui</c> arrays of all loaded mods unless
        /// already built. Keeps the old Initialize idempotence: later calls are
        /// no-ops until the registry is cleared.
        /// </summary>
        public void EnsureBuilt()
        {
            if (!built)
                Rebuild();
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public IReadOnlyList<UiEntry> AllEntries => entries;

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public IReadOnlyList<UiEntry> AllPanels => panels;

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>All valid UI entries in mod load order (then priority, then id).</summary>
        public static IReadOnlyList<UiEntry> Entries => Owned.entries;

        /// <summary>Panel-kind entries, pre-sorted for toolbar construction.</summary>
        public static IReadOnlyList<UiEntry> PanelEntries => Owned.panels;

        /// <summary>
        /// Collects and validates the <c>ui</c> arrays of all loaded mods. Safe to
        /// call multiple times; later calls are no-ops until <see cref="Clear"/>.
        /// </summary>
        public static void Initialize() => Owned.EnsureBuilt();

        /// <summary>
        /// Drops all collected entries so the next <see cref="Initialize"/> re-reads
        /// the manifests. Called by <see cref="ModManager"/> on reload/clear.
        /// </summary>
        public static void Clear() => Owned.Reset();

        /// <summary>Returns the entry with the given id, or null when unknown.</summary>
        public static UiEntry Get(string id) => Owned.GetById(id);

        /// <summary>
        /// Shows or hides the GUI element with the given id through <see cref="UiHost"/>.
        /// Panels are toggled this way; overlays may be toggled by their own actions.
        /// </summary>
        public static void Toggle(string id) => Owned.ToggleEntry(id);

        /// <summary>True when the entry represents a button-toggled panel.</summary>
        public static bool IsPanel(UiEntry entry)
            => entry != null && entry.Kind == UiEntry.KindPanel;

        // ---- Instance implementation --------------------------------------

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public void Reset()
        {
            entries.Clear();
            panels.Clear();
            byId.Clear();
            built = false;
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public UiEntry GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return byId.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public void ToggleEntry(string id)
        {
            var host = UiHost.Instance;
            if (host == null)
            {
                GameLogger.Log("UI_REGISTRY", $"Toggle('{id}'): UiHost not available", LogLevel.Error);
                return;
            }
            host.Toggle(id);
        }

        private void Collect()
        {
            var manager = ModManager.Instance;
            if (manager?.Mods == null)
            {
                GameLogger.Log("UI_REGISTRY", "Collect: mods not loaded", LogLevel.Error);
                return;
            }

            var orderedIds = ModRegistry.Instance.GetOrderedModIds();
            for (var order = 0; order < orderedIds.Count; order++)
            {
                var modId = orderedIds[order];
                if (!manager.Mods.TryGetValue(modId, out var info) || info is not ModInfo modInfo)
                    continue;

                var declarations = modInfo.Manifest?.Ui;
                if (declarations == null || declarations.Count == 0)
                    continue;

                var accepted = new List<UiEntry>();
                foreach (var raw in declarations)
                {
                    if (raw == null)
                        continue;
                    raw.ModId = modId;
                    raw.ModOrderIndex = order;
                    if (Validate(raw, modInfo))
                        accepted.Add(raw);
                }

                accepted.Sort(CompareEntries);
                foreach (var entry in accepted)
                    AddEntry(entry);
            }

            BuildPanelIndex();
            GameLogger.Log("UI_REGISTRY", $"Collect → {entries.Count} entry(ies), {panels.Count} panel(s)");
        }

        private static int CompareEntries(UiEntry a, UiEntry b)
        {
            var priority = a.Priority.CompareTo(b.Priority);
            if (priority != 0)
                return priority;
            return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }

        private void AddEntry(UiEntry entry)
        {
            entries.Add(entry);
            byId[entry.Id] = entry;
        }

        private void BuildPanelIndex()
        {
            panels.Clear();
            foreach (var entry in entries)
            {
                if (!IsPanel(entry))
                    continue;

                entry.Once = true;
                panels.Add(entry);
            }

            panels.Sort(ComparePanelButtons);
        }

        private static int ComparePanelButtons(UiEntry a, UiEntry b)
        {
            var index = a.Button.Index.CompareTo(b.Button.Index);
            if (index != 0)
                return index;
            var modOrder = a.ModOrderIndex.CompareTo(b.ModOrderIndex);
            if (modOrder != 0)
                return modOrder;
            return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }

        private bool Validate(UiEntry entry, ModInfo modInfo)
        {
            var label = $"{modInfo.Id}/{entry.Id ?? "?"}";

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                GameLogger.Log("UI_REGISTRY", $"invalid ui entry in mod '{modInfo.Id}': missing 'id'", LogLevel.Error);
                return false;
            }

            if (byId.ContainsKey(entry.Id))
            {
                GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': duplicate id", LogLevel.Error);
                return false;
            }

            if (entry.Kind != UiEntry.KindPanel && entry.Kind != UiEntry.KindOverlay)
            {
                GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': kind must be '{UiEntry.KindPanel}' or '{UiEntry.KindOverlay}'", LogLevel.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.Slot))
            {
                GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': missing 'slot'", LogLevel.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': missing 'scene_path'", LogLevel.Error);
                return false;
            }

            if (entry.Kind == UiEntry.KindPanel)
            {
                if (entry.Button == null)
                {
                    GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': kind=panel requires 'button'", LogLevel.Error);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(entry.Button.Icon) || string.IsNullOrWhiteSpace(entry.Button.Locale))
                {
                    GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': button requires 'icon' and 'locale'", LogLevel.Error);
                    return false;
                }
            }
            else if (entry.Button != null)
            {
                GameLogger.Log("UI_REGISTRY", $"invalid ui entry '{label}': kind=overlay must not declare 'button'", LogLevel.Error);
                return false;
            }

            var provider = modInfo.FileProvider;
            if (provider != null && !provider.FileExists(entry.ScenePath))
                GameLogger.Log("UI_REGISTRY", $"ui entry '{label}': scene_path '{entry.ScenePath}' not found in mod '{modInfo.Id}'", LogLevel.Warning);

            return true;
        }
    }
}
